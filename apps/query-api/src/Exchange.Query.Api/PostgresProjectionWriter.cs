using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Npgsql;

namespace Exchange.Query.Api;

public sealed class PostgresProjectionWriter : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresProjectionWriter> _logger;
    private readonly Dictionary<Guid, PendingOrderMutation> _pendingOrderMutations = [];
    private readonly Dictionary<string, TradeExecuted> _pendingTrades = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, HashSet<string>> _pendingTradesByOrder = [];
    private readonly HashSet<Guid> _knownOrders = [];
    private ProjectionSchemaCapabilities? _schemaCapabilities;

    public PostgresProjectionWriter(IConfiguration configuration, ILogger<PostgresProjectionWriter> logger)
    {
        _logger = logger;
        var connectionString =
            configuration.GetConnectionString("Postgres") ??
            configuration["ConnectionStrings:Postgres"] ??
            "Host=localhost;Port=5432;Database=exchange;Username=exchange;Password=exchange";

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task UpsertAccountAsync(AccountCreated accountCreated, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO accounts (account_id, display_name, email, status, created_at, updated_at)
            VALUES (@account_id, @display_name, @email, 'Active', @created_at, @created_at)
            ON CONFLICT (account_id) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                email = EXCLUDED.email,
                updated_at = EXCLUDED.updated_at;
            """);

        command.Parameters.AddWithValue("account_id", accountCreated.AccountId);
        command.Parameters.AddWithValue("display_name", accountCreated.DisplayName);
        command.Parameters.AddWithValue("email", accountCreated.Email);
        command.Parameters.AddWithValue("created_at", accountCreated.CreatedAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var capabilities = await EnsureSchemaCapabilitiesAsync(cancellationToken);
        var mutation = _pendingOrderMutations.TryGetValue(command.OrderId, out var pending)
            ? pending
            : null;

        var filledQuantity = 0m;
        var remainingQuantity = mutation?.RemainingQuantity ?? command.Quantity;
        var status = mutation?.Status?.ToString() ?? "Pending";
        var updatedAt = mutation?.UpdatedAt ?? command.SubmittedAt;
        var rejectionReason = mutation?.RejectionReason;

        await using var commandDb = _dataSource.CreateCommand(ProjectionSchemaSqlBuilder.BuildUpsertOrderSql(capabilities));

        commandDb.Parameters.AddWithValue("order_id", command.OrderId);
        commandDb.Parameters.AddWithValue("account_id", command.AccountId);
        commandDb.Parameters.AddWithValue("symbol", command.Symbol);
        commandDb.Parameters.AddWithValue("side", command.Side.ToString());
        commandDb.Parameters.AddWithValue("order_type", command.Type.ToString());
        commandDb.Parameters.AddWithValue("time_in_force", command.TimeInForce.ToString());
        commandDb.Parameters.AddWithValue("quantity", command.Quantity);
        commandDb.Parameters.AddWithValue("limit_price", (object?)command.Price ?? DBNull.Value);
        commandDb.Parameters.AddWithValue("filled_quantity", filledQuantity);
        commandDb.Parameters.AddWithValue("remaining_quantity", remainingQuantity);
        commandDb.Parameters.AddWithValue("status", status);
        commandDb.Parameters.AddWithValue("rejection_reason", (object?)rejectionReason ?? DBNull.Value);
        commandDb.Parameters.AddWithValue("client_order_id", (object?)command.ClientOrderId ?? DBNull.Value);
        if (capabilities.SupportsExtendedOrders)
        {
            commandDb.Parameters.AddWithValue("instrument_id", (object?)command.InstrumentId ?? DBNull.Value);
            commandDb.Parameters.AddWithValue("trading_account_id", (object?)command.TradingAccountId ?? DBNull.Value);
            commandDb.Parameters.AddWithValue("source_system", command.SourceSystem.ToString());
            commandDb.Parameters.AddWithValue("execution_instructions", command.ExecutionInstructions is null ? DBNull.Value : JsonSerializer.Serialize(command.ExecutionInstructions));
            commandDb.Parameters.AddWithValue("stop_price", (object?)command.StopPrice ?? DBNull.Value);
        }

        commandDb.Parameters.AddWithValue("created_at", command.SubmittedAt.UtcDateTime);
        commandDb.Parameters.AddWithValue("updated_at", updatedAt.UtcDateTime);
        await commandDb.ExecuteNonQueryAsync(cancellationToken);

        _knownOrders.Add(command.OrderId);
        _pendingOrderMutations.Remove(command.OrderId);

        await FlushPendingTradesForOrderAsync(command.OrderId, cancellationToken);
    }

    public async Task ApplyOrderAcceptedAsync(OrderAccepted orderAccepted, CancellationToken cancellationToken)
    {
        if (!await TryUpdateOrderStatusAsync(
                orderAccepted.OrderId,
                orderAccepted.Status.ToString(),
                orderAccepted.RemainingQuantity,
                orderAccepted.AcceptedAt,
                null,
                cancellationToken))
        {
            var pending = GetPendingMutation(orderAccepted.OrderId);
            pending.Status = orderAccepted.Status;
            pending.RemainingQuantity = orderAccepted.RemainingQuantity;
            pending.UpdatedAt = orderAccepted.AcceptedAt;
        }
    }

    public async Task ApplyOrderRejectedAsync(OrderRejected orderRejected, CancellationToken cancellationToken)
    {
        if (!await TryUpdateOrderStatusAsync(
                orderRejected.OrderId,
                "Rejected",
                null,
                orderRejected.RejectedAt,
                orderRejected.Reason,
                cancellationToken))
        {
            var pending = GetPendingMutation(orderRejected.OrderId);
            pending.Status = Exchange.Platform.Contracts.OrderStatus.Rejected;
            pending.RejectionReason = orderRejected.Reason;
            pending.UpdatedAt = orderRejected.RejectedAt;
        }
    }

    public async Task ApplyTradeExecutedAsync(TradeExecuted tradeExecuted, CancellationToken cancellationToken)
    {
        var buyKnown = await EnsureKnownOrderAsync(tradeExecuted.BuyOrderId, cancellationToken);
        var sellKnown = await EnsureKnownOrderAsync(tradeExecuted.SellOrderId, cancellationToken);

        if (!buyKnown || !sellKnown)
        {
            QueuePendingTrade(tradeExecuted);
            return;
        }

        await PersistTradeAsync(tradeExecuted, cancellationToken);
    }

    private async Task PersistTradeAsync(TradeExecuted tradeExecuted, CancellationToken cancellationToken)
    {
        var capabilities = await EnsureSchemaCapabilitiesAsync(cancellationToken);
        var tradeId = DeterministicGuid($"trade:{tradeExecuted.TradeId}");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var tradeCommand = new NpgsqlCommand(
                         """
                         INSERT INTO trades (trade_id, symbol, buy_order_id, sell_order_id, price, quantity, executed_at)
                         VALUES (@trade_id, @symbol, @buy_order_id, @sell_order_id, @price, @quantity, @executed_at)
                         ON CONFLICT (trade_id) DO NOTHING;
                         """,
                         connection,
                         transaction))
        {
            tradeCommand.Parameters.AddWithValue("trade_id", tradeId);
            tradeCommand.Parameters.AddWithValue("symbol", tradeExecuted.Symbol);
            tradeCommand.Parameters.AddWithValue("buy_order_id", tradeExecuted.BuyOrderId);
            tradeCommand.Parameters.AddWithValue("sell_order_id", tradeExecuted.SellOrderId);
            tradeCommand.Parameters.AddWithValue("price", tradeExecuted.Price);
            tradeCommand.Parameters.AddWithValue("quantity", tradeExecuted.Quantity);
            tradeCommand.Parameters.AddWithValue("executed_at", tradeExecuted.ExecutedAt.UtcDateTime);
            await tradeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (capabilities.SupportsTradeExecutions &&
            tradeExecuted.InstrumentId.HasValue &&
            tradeExecuted.BuyTradingAccountId.HasValue &&
            tradeExecuted.SellTradingAccountId.HasValue)
        {
            await using var tradeExecutionCommand = new NpgsqlCommand(
                """
                INSERT INTO trade_executions (
                    trade_execution_id, trade_execution_code, instrument_id,
                    buy_order_id, sell_order_id, buy_trading_account_id, sell_trading_account_id,
                    quantity, price, executed_at, aggressor_side, trade_source, exchange_execution_id, metadata
                )
                VALUES (
                    @trade_execution_id, @trade_execution_code, @instrument_id,
                    @buy_order_id, @sell_order_id, @buy_trading_account_id, @sell_trading_account_id,
                    @quantity, @price, @executed_at, @aggressor_side, @trade_source, @exchange_execution_id, @metadata::jsonb
                )
                ON CONFLICT (trade_execution_id) DO NOTHING;
                """,
                connection,
                transaction);

            tradeExecutionCommand.Parameters.AddWithValue("trade_execution_id", tradeId);
            tradeExecutionCommand.Parameters.AddWithValue("trade_execution_code", tradeExecuted.TradeId);
            tradeExecutionCommand.Parameters.AddWithValue("instrument_id", tradeExecuted.InstrumentId.Value);
            tradeExecutionCommand.Parameters.AddWithValue("buy_order_id", tradeExecuted.BuyOrderId);
            tradeExecutionCommand.Parameters.AddWithValue("sell_order_id", tradeExecuted.SellOrderId);
            tradeExecutionCommand.Parameters.AddWithValue("buy_trading_account_id", tradeExecuted.BuyTradingAccountId.Value);
            tradeExecutionCommand.Parameters.AddWithValue("sell_trading_account_id", tradeExecuted.SellTradingAccountId.Value);
            tradeExecutionCommand.Parameters.AddWithValue("quantity", tradeExecuted.Quantity);
            tradeExecutionCommand.Parameters.AddWithValue("price", tradeExecuted.Price);
            tradeExecutionCommand.Parameters.AddWithValue("executed_at", tradeExecuted.ExecutedAt.UtcDateTime);
            tradeExecutionCommand.Parameters.AddWithValue("aggressor_side", (object?)tradeExecuted.AggressorSide?.ToString() ?? DBNull.Value);
            tradeExecutionCommand.Parameters.AddWithValue("trade_source", tradeExecuted.TradeSource);
            tradeExecutionCommand.Parameters.AddWithValue("exchange_execution_id", (object?)tradeExecuted.ExchangeExecutionId ?? DBNull.Value);
            tradeExecutionCommand.Parameters.AddWithValue("metadata", tradeExecuted.Metadata is null ? "{}" : JsonSerializer.Serialize(tradeExecuted.Metadata));
            await tradeExecutionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpdateOrderFillAsync(connection, transaction, tradeExecuted.BuyOrderId, tradeExecuted.Quantity, tradeExecuted.ExecutedAt, cancellationToken);
        await UpdateOrderFillAsync(connection, transaction, tradeExecuted.SellOrderId, tradeExecuted.Quantity, tradeExecuted.ExecutedAt, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Query persisted TradeId={TradeId} BuyOrderId={BuyOrderId} SellOrderId={SellOrderId} into PostgreSQL.",
            tradeExecuted.TradeId,
            tradeExecuted.BuyOrderId,
            tradeExecuted.SellOrderId);
    }

    private async Task UpdateOrderFillAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        decimal quantity,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE orders
            SET filled_quantity = filled_quantity + @quantity,
                remaining_quantity = GREATEST(remaining_quantity - @quantity, 0),
                status = CASE
                    WHEN GREATEST(remaining_quantity - @quantity, 0) = 0 THEN 'Filled'
                    ELSE 'PartiallyFilled'
                END,
                updated_at = @updated_at
            WHERE order_id = @order_id;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("updated_at", executedAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> TryUpdateOrderStatusAsync(
        Guid orderId,
        string status,
        decimal? remainingQuantity,
        DateTimeOffset updatedAt,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE orders
            SET status = @status,
                remaining_quantity = COALESCE(@remaining_quantity, remaining_quantity),
                rejection_reason = COALESCE(@rejection_reason, rejection_reason),
                updated_at = @updated_at
            WHERE order_id = @order_id;
            """);

        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("remaining_quantity", (object?)remainingQuantity ?? DBNull.Value);
        command.Parameters.AddWithValue("rejection_reason", (object?)rejectionReason ?? DBNull.Value);
        command.Parameters.AddWithValue("updated_at", updatedAt.UtcDateTime);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private async Task FlushPendingTradesForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!_pendingTradesByOrder.TryGetValue(orderId, out var tradeIds))
        {
            return;
        }

        foreach (var tradeId in tradeIds.ToArray())
        {
            if (!_pendingTrades.TryGetValue(tradeId, out var trade))
            {
                continue;
            }

            var buyKnown = await EnsureKnownOrderAsync(trade.BuyOrderId, cancellationToken);
            var sellKnown = await EnsureKnownOrderAsync(trade.SellOrderId, cancellationToken);
            if (!buyKnown || !sellKnown)
            {
                continue;
            }

            await PersistTradeAsync(trade, cancellationToken);
            _pendingTrades.Remove(tradeId);
            RemovePendingTradeReference(trade.BuyOrderId, tradeId);
            RemovePendingTradeReference(trade.SellOrderId, tradeId);
        }
    }

    private void QueuePendingTrade(TradeExecuted tradeExecuted)
    {
        _pendingTrades[tradeExecuted.TradeId] = tradeExecuted;
        AddPendingTradeReference(tradeExecuted.BuyOrderId, tradeExecuted.TradeId);
        AddPendingTradeReference(tradeExecuted.SellOrderId, tradeExecuted.TradeId);
    }

    private void AddPendingTradeReference(Guid orderId, string tradeId)
    {
        if (!_pendingTradesByOrder.TryGetValue(orderId, out var tradeIds))
        {
            tradeIds = [];
            _pendingTradesByOrder[orderId] = tradeIds;
        }

        tradeIds.Add(tradeId);
    }

    private void RemovePendingTradeReference(Guid orderId, string tradeId)
    {
        if (!_pendingTradesByOrder.TryGetValue(orderId, out var tradeIds))
        {
            return;
        }

        tradeIds.Remove(tradeId);
        if (tradeIds.Count == 0)
        {
            _pendingTradesByOrder.Remove(orderId);
        }
    }

    private PendingOrderMutation GetPendingMutation(Guid orderId)
    {
        if (!_pendingOrderMutations.TryGetValue(orderId, out var mutation))
        {
            mutation = new PendingOrderMutation();
            _pendingOrderMutations[orderId] = mutation;
        }

        return mutation;
    }

    private async Task<bool> EnsureKnownOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (_knownOrders.Contains(orderId))
        {
            return true;
        }

        await using var command = _dataSource.CreateCommand("SELECT 1 FROM orders WHERE order_id = @order_id LIMIT 1;");
        command.Parameters.AddWithValue("order_id", orderId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not null)
        {
            _knownOrders.Add(orderId);
            return true;
        }

        return false;
    }

    private async Task<ProjectionSchemaCapabilities> EnsureSchemaCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (_schemaCapabilities is not null)
        {
            return _schemaCapabilities;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var orderColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT column_name
                         FROM information_schema.columns
                         WHERE table_schema = 'public' AND table_name = 'orders';
                         """,
                         connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                orderColumns.Add(reader.GetString(0));
            }
        }

        var supportsTradeExecutions = false;
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT EXISTS (
                             SELECT 1
                             FROM information_schema.tables
                             WHERE table_schema = 'public' AND table_name = 'trade_executions'
                         );
                         """,
                         connection))
        {
            supportsTradeExecutions = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }

        _schemaCapabilities = new ProjectionSchemaCapabilities(
            ProjectionSchemaSqlBuilder.SupportsExtendedOrders(orderColumns),
            supportsTradeExecutions);

        if (!_schemaCapabilities.SupportsExtendedOrders)
        {
            _logger.LogWarning("Query API detected legacy orders schema in PostgreSQL. B3 runtime columns will be skipped until migration 002 is applied.");
        }

        if (!_schemaCapabilities.SupportsTradeExecutions)
        {
            _logger.LogWarning("Query API detected missing trade_executions table in PostgreSQL. Enriched trade execution persistence will be skipped until migration 002 is applied.");
        }

        return _schemaCapabilities;
    }

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private sealed class PendingOrderMutation
    {
        public Exchange.Platform.Contracts.OrderStatus? Status { get; set; }
        public decimal? RemainingQuantity { get; set; }
        public string? RejectionReason { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
