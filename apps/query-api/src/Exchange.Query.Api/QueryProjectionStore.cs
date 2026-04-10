using System.Collections.Concurrent;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Query.Api;

public sealed class QueryProjectionStore
{
    private readonly ConcurrentDictionary<Guid, AccountSummary> _accounts = new();
    private readonly ConcurrentDictionary<string, AccountBalanceView> _balances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, OrderHistoryItem> _orders = new();
    private readonly ConcurrentDictionary<Guid, EnrichedOrderView> _enrichedOrders = new();
    private readonly ConcurrentDictionary<string, InstrumentSnapshot> _instruments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PositionSnapshot> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TickerSnapshot> _tickers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, PendingOrderProjection> _pendingOrders = new();
    private readonly List<RecentTradeView> _trades = [];
    private readonly List<EnrichedTradeView> _enrichedTrades = [];
    private readonly object _tradeLock = new();

    public QueryProjectionStore()
    {
        var seededAt = DateTimeOffset.UtcNow;

        foreach (var account in DemoSeed.Accounts)
        {
            _accounts[account.AccountId] = new AccountSummary(account.AccountId, account.DisplayName, account.Email, seededAt);
        }

        foreach (var balance in DemoSeed.Balances)
        {
            _balances[BalanceKey(balance.AccountId, balance.Asset)] = new AccountBalanceView(
                balance.AccountId,
                balance.Asset,
                balance.Available,
                balance.Reserved,
                balance.Available + balance.Reserved,
                seededAt);
        }

        foreach (var instrument in DemoSeed.Instruments)
        {
            _instruments[instrument.Symbol] = new InstrumentSnapshot(
                instrument.InstrumentId,
                instrument.Symbol,
                instrument.AssetClass,
                instrument.Segment,
                instrument.Market,
                instrument.BaseAsset,
                instrument.QuoteAsset,
                instrument.TradingStatus,
                instrument.TickSize,
                instrument.LotSize);
        }
    }

    public void Apply(AccountCreated accountCreated)
    {
        _accounts[accountCreated.AccountId] = new AccountSummary(
            accountCreated.AccountId,
            accountCreated.DisplayName,
            accountCreated.Email,
            accountCreated.CreatedAt);
    }

    public void Apply(CreateOrderCommand command)
    {
        var order = new OrderHistoryItem(
            command.OrderId,
            command.AccountId,
            command.Symbol.ToUpperInvariant(),
            command.Side,
            command.Type,
            OrderStatus.Pending,
            command.Quantity,
            0m,
            command.Price,
            command.SubmittedAt,
            command.SubmittedAt);

        var tradingAccountId = ResolveTradingAccountId(command.AccountId, command.TradingAccountId);
        var instrumentId = ResolveInstrumentId(command.Symbol, command.InstrumentId);
        var enrichedOrder = new EnrichedOrderView(
            command.OrderId,
            command.AccountId,
            tradingAccountId,
            instrumentId,
            command.Symbol.ToUpperInvariant(),
            command.Side,
            command.Type,
            OrderStatus.Pending,
            command.Quantity,
            0m,
            command.Quantity,
            command.Price,
            command.SourceSystem,
            command.SubmittedAt,
            command.SubmittedAt);

        var pending = _pendingOrders.TryRemove(command.OrderId, out var existingPending)
            ? existingPending
            : null;

        _orders[command.OrderId] = ApplyPending(order, pending);
        _enrichedOrders[command.OrderId] = ApplyPending(enrichedOrder, pending);
    }

    public void Apply(OrderAccepted orderAccepted)
    {
        if (!UpdateOrder(orderAccepted.OrderId, current => current with
        {
            Status = orderAccepted.Status,
            UpdatedAt = orderAccepted.AcceptedAt
        }))
        {
            var pending = _pendingOrders.GetOrAdd(orderAccepted.OrderId, _ => new PendingOrderProjection());
            pending.Status = orderAccepted.Status;
            pending.UpdatedAt = orderAccepted.AcceptedAt;
        }

        UpdateEnrichedOrder(orderAccepted.OrderId, current => current with
        {
            Status = orderAccepted.Status,
            UpdatedAt = orderAccepted.AcceptedAt
        });
    }

    public void Apply(OrderRejected orderRejected)
    {
        if (!UpdateOrder(orderRejected.OrderId, current => current with
        {
            Status = OrderStatus.Rejected,
            UpdatedAt = orderRejected.RejectedAt
        }))
        {
            var pending = _pendingOrders.GetOrAdd(orderRejected.OrderId, _ => new PendingOrderProjection());
            pending.Status = OrderStatus.Rejected;
            pending.UpdatedAt = orderRejected.RejectedAt;
        }

        UpdateEnrichedOrder(orderRejected.OrderId, current => current with
        {
            Status = OrderStatus.Rejected,
            UpdatedAt = orderRejected.RejectedAt
        });
    }

    public void Apply(TradeExecuted tradeExecuted)
    {
        lock (_tradeLock)
        {
            _trades.Insert(0, new RecentTradeView(
                tradeExecuted.TradeId,
                tradeExecuted.Symbol.ToUpperInvariant(),
                tradeExecuted.Price,
                tradeExecuted.Quantity,
                $"{ShortAccount(tradeExecuted.BuyAccountId)}->{ShortAccount(tradeExecuted.SellAccountId)}",
                tradeExecuted.ExecutedAt));

            _enrichedTrades.Insert(0, new EnrichedTradeView(
                tradeExecuted.TradeId,
                ResolveInstrumentId(tradeExecuted.Symbol, tradeExecuted.InstrumentId),
                tradeExecuted.Symbol.ToUpperInvariant(),
                tradeExecuted.BuyOrderId,
                tradeExecuted.SellOrderId,
                ResolveTradingAccountId(tradeExecuted.BuyAccountId, tradeExecuted.BuyTradingAccountId),
                ResolveTradingAccountId(tradeExecuted.SellAccountId, tradeExecuted.SellTradingAccountId),
                tradeExecuted.Price,
                tradeExecuted.Quantity,
                tradeExecuted.ExecutedAt));

            if (_trades.Count > 500)
            {
                _trades.RemoveRange(500, _trades.Count - 500);
            }

            if (_enrichedTrades.Count > 500)
            {
                _enrichedTrades.RemoveRange(500, _enrichedTrades.Count - 500);
            }
        }

        UpdatePosition(
            ResolveTradingAccountId(tradeExecuted.BuyAccountId, tradeExecuted.BuyTradingAccountId),
            ResolveInstrumentId(tradeExecuted.Symbol, tradeExecuted.InstrumentId),
            tradeExecuted.Symbol,
            tradeExecuted.Quantity,
            tradeExecuted.Price,
            tradeExecuted.ExecutedAt);
        UpdatePosition(
            ResolveTradingAccountId(tradeExecuted.SellAccountId, tradeExecuted.SellTradingAccountId),
            ResolveInstrumentId(tradeExecuted.Symbol, tradeExecuted.InstrumentId),
            tradeExecuted.Symbol,
            -tradeExecuted.Quantity,
            tradeExecuted.Price,
            tradeExecuted.ExecutedAt);

        ApplyFill(tradeExecuted.BuyOrderId, tradeExecuted.Quantity, tradeExecuted.Price, tradeExecuted.ExecutedAt);
        ApplyFill(tradeExecuted.SellOrderId, tradeExecuted.Quantity, tradeExecuted.Price, tradeExecuted.ExecutedAt);
    }

    public void Apply(BalanceAdjusted balanceAdjusted)
    {
        var key = BalanceKey(balanceAdjusted.AccountId, balanceAdjusted.Asset);
        var current = _balances.GetOrAdd(
            key,
            _ => new AccountBalanceView(balanceAdjusted.AccountId, balanceAdjusted.Asset.ToUpperInvariant(), 0m, 0m, 0m, balanceAdjusted.OccurredAt));

        var updated = current with
        {
            Available = current.Available + balanceAdjusted.AvailableDelta,
            Reserved = current.Reserved + balanceAdjusted.ReservedDelta,
            Total = current.Total + balanceAdjusted.AvailableDelta + balanceAdjusted.ReservedDelta,
            AsOf = balanceAdjusted.OccurredAt
        };

        _balances[key] = updated;
    }

    public void Apply(TickerUpdated tickerUpdated)
    {
        var existing = _tickers.TryGetValue(tickerUpdated.Symbol, out var current) ? current : null;
        var high24H = existing is null ? tickerUpdated.LastPrice : Math.Max(existing.High24H, tickerUpdated.LastPrice);
        var low24H = existing is null ? tickerUpdated.LastPrice : Math.Min(existing.Low24H, tickerUpdated.LastPrice);

        _tickers[tickerUpdated.Symbol] = new TickerSnapshot(
            tickerUpdated.Symbol,
            tickerUpdated.LastPrice,
            tickerUpdated.BestBid,
            tickerUpdated.BestAsk,
            high24H,
            low24H,
            (existing?.Volume24H ?? 0m) + tickerUpdated.Volume24H,
            tickerUpdated.Change24H,
            tickerUpdated.AsOf);
    }

    public IReadOnlyCollection<OrderHistoryItem> GetOrderHistory(Guid? accountId) =>
        _orders.Values
            .Where(order => !accountId.HasValue || order.AccountId == accountId.Value)
            .OrderByDescending(order => order.UpdatedAt)
            .ToArray();

    public IReadOnlyCollection<EnrichedOrderView> GetEnrichedOrders(Guid? accountId) =>
        _enrichedOrders.Values
            .Where(order => !accountId.HasValue || order.AccountId == accountId.Value)
            .OrderByDescending(order => order.UpdatedAt)
            .ToArray();

    public IReadOnlyCollection<InstrumentSnapshot> GetInstruments() =>
        _instruments.Values.OrderBy(item => item.Symbol).ToArray();

    public IReadOnlyCollection<PositionSnapshot> GetPositions(Guid? tradingAccountId) =>
        _positions.Values
            .Where(position => !tradingAccountId.HasValue || position.TradingAccountId == tradingAccountId.Value)
            .OrderBy(position => position.Symbol)
            .ToArray();

    public IReadOnlyCollection<BalanceSnapshot> GetBalances(Guid accountId) =>
        _balances.Values
            .Where(balance => balance.AccountId == accountId)
            .OrderBy(balance => balance.Asset)
            .Select(balance => new BalanceSnapshot(balance.AccountId, balance.Asset, balance.Available, balance.Reserved, balance.AsOf))
            .ToArray();

    public object GetTickerWithCandle(string symbol)
    {
        var normalized = symbol.ToUpperInvariant();
        if (!_tickers.TryGetValue(normalized, out var ticker))
        {
            ticker = new TickerSnapshot(normalized, 0m, 0m, 0m, 0m, 0m, 0m, 0m, DateTimeOffset.UtcNow);
        }

        CandleSnapshot candle;
        lock (_tradeLock)
        {
            var window = _trades
                .Where(trade => trade.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .Where(trade => trade.ExecutedAt >= DateTimeOffset.UtcNow.AddMinutes(-1))
                .OrderBy(trade => trade.ExecutedAt)
                .ToArray();

            candle = window.Length == 0
                ? new CandleSnapshot(normalized, "1m", ticker.LastPrice, ticker.LastPrice, ticker.LastPrice, ticker.LastPrice, 0m, DateTimeOffset.UtcNow.AddMinutes(-1), ticker.AsOf)
                : new CandleSnapshot(
                    normalized,
                    "1m",
                    window.First().Price,
                    window.Max(trade => trade.Price),
                    window.Min(trade => trade.Price),
                    window.Last().Price,
                    window.Sum(trade => trade.Quantity),
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    ticker.AsOf);
        }

        return new { ticker, candle };
    }

    public IReadOnlyCollection<RecentTradeView> GetRecentTrades(string? symbol, int? limit)
    {
        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);
        lock (_tradeLock)
        {
            return _trades
                .Where(trade => string.IsNullOrWhiteSpace(symbol) || trade.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                .Take(effectiveLimit)
                .ToArray();
        }
    }

    public IReadOnlyCollection<EnrichedTradeView> GetEnrichedTrades(string? symbol, int? limit)
    {
        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);
        lock (_tradeLock)
        {
            return _enrichedTrades
                .Where(trade => string.IsNullOrWhiteSpace(symbol) || trade.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                .Take(effectiveLimit)
                .ToArray();
        }
    }

    public IReadOnlyCollection<MarketOverviewItem> GetMarketOverview() =>
        _tickers.Values
            .OrderBy(ticker => ticker.Symbol)
            .Select(ticker => new MarketOverviewItem(
                ticker.Symbol,
                ticker.LastPrice,
                ticker.Change24H,
                ticker.LastPrice == 0 ? 0 : decimal.Round((ticker.Change24H / ticker.LastPrice) * 100m, 4),
                ticker.Volume24H,
                ticker.High24H,
                ticker.Low24H,
                ticker.AsOf))
            .ToArray();

    private void ApplyFill(Guid orderId, decimal quantity, decimal lastTradePrice, DateTimeOffset executedAt)
    {
        if (!UpdateOrder(orderId, current =>
            {
                var filledQuantity = current.FilledQuantity + quantity;
                var status = filledQuantity >= current.Quantity ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
                return current with
                {
                    FilledQuantity = filledQuantity,
                    Status = status,
                    UpdatedAt = executedAt,
                    Price = current.Price ?? lastTradePrice
                };
            }))
        {
            var pending = _pendingOrders.GetOrAdd(orderId, _ => new PendingOrderProjection());
            pending.FilledQuantity += quantity;
            pending.LastTradePrice = lastTradePrice;
            pending.UpdatedAt = executedAt;
        }

        UpdateEnrichedOrder(orderId, current =>
        {
            var filledQuantity = current.FilledQuantity + quantity;
            var status = filledQuantity >= current.Quantity ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
            return current with
            {
                FilledQuantity = filledQuantity,
                OpenQuantity = Math.Max(current.Quantity - filledQuantity, 0m),
                Status = status,
                UpdatedAt = executedAt,
                Price = current.Price ?? lastTradePrice
            };
        });
    }

    private bool UpdateOrder(Guid orderId, Func<OrderHistoryItem, OrderHistoryItem> apply)
    {
        if (_orders.TryGetValue(orderId, out var current))
        {
            _orders[orderId] = apply(current);
            return true;
        }

        return false;
    }

    private bool UpdateEnrichedOrder(Guid orderId, Func<EnrichedOrderView, EnrichedOrderView> apply)
    {
        if (_enrichedOrders.TryGetValue(orderId, out var current))
        {
            _enrichedOrders[orderId] = apply(current);
            return true;
        }

        return false;
    }

    private static OrderHistoryItem ApplyPending(OrderHistoryItem current, PendingOrderProjection? pending)
    {
        if (pending is null)
        {
            return current;
        }

        var filledQuantity = current.FilledQuantity + pending.FilledQuantity;
        var status = pending.Status
            ?? (filledQuantity >= current.Quantity ? OrderStatus.Filled
                : filledQuantity > 0 ? OrderStatus.PartiallyFilled
                : current.Status);

        return current with
        {
            FilledQuantity = filledQuantity,
            Status = status,
            Price = current.Price ?? pending.LastTradePrice,
            UpdatedAt = pending.UpdatedAt ?? current.UpdatedAt
        };
    }

    private static EnrichedOrderView ApplyPending(EnrichedOrderView current, PendingOrderProjection? pending)
    {
        if (pending is null)
        {
            return current;
        }

        var filledQuantity = current.FilledQuantity + pending.FilledQuantity;
        var status = pending.Status
            ?? (filledQuantity >= current.Quantity ? OrderStatus.Filled
                : filledQuantity > 0 ? OrderStatus.PartiallyFilled
                : current.Status);

        return current with
        {
            FilledQuantity = filledQuantity,
            OpenQuantity = Math.Max(current.Quantity - filledQuantity, 0m),
            Status = status,
            Price = current.Price ?? pending.LastTradePrice,
            UpdatedAt = pending.UpdatedAt ?? current.UpdatedAt
        };
    }

    private void UpdatePosition(Guid tradingAccountId, Guid instrumentId, string symbol, decimal netDelta, decimal tradePrice, DateTimeOffset executedAt)
    {
        var positionDate = DateOnly.FromDateTime(executedAt.UtcDateTime);
        var key = PositionKey(tradingAccountId, instrumentId, positionDate);
        var existing = _positions.TryGetValue(key, out var current)
            ? current
            : new PositionSnapshot(Guid.NewGuid(), tradingAccountId, instrumentId, symbol.ToUpperInvariant(), positionDate, 0m, null, 0m, 0m, executedAt);

        var netQuantity = decimal.Round(existing.NetQuantity + netDelta, 8, MidpointRounding.ToZero);
        _positions[key] = existing with
        {
            NetQuantity = netQuantity,
            AverageOpenPrice = tradePrice,
            LongQuantity = netQuantity > 0 ? netQuantity : 0m,
            ShortQuantity = netQuantity < 0 ? Math.Abs(netQuantity) : 0m,
            UpdatedAt = executedAt
        };
    }

    private static Guid ResolveTradingAccountId(Guid accountId, Guid? tradingAccountId) =>
        tradingAccountId ?? DemoSeed.TradingAccounts.First(account => account.AccountId == accountId).TradingAccountId;

    private static Guid ResolveInstrumentId(string symbol, Guid? instrumentId) =>
        instrumentId ?? DemoSeed.Instruments.First(instrument => instrument.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).InstrumentId;

    private sealed class PendingOrderProjection
    {
        public OrderStatus? Status { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal? LastTradePrice { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    private static string ShortAccount(Guid accountId) =>
        accountId.ToString("N")[..6];

    private static string BalanceKey(Guid accountId, string asset) =>
        $"{accountId}:{asset.ToUpperInvariant()}";

    private static string PositionKey(Guid tradingAccountId, Guid instrumentId, DateOnly positionDate) =>
        $"{tradingAccountId:N}:{instrumentId:N}:{positionDate:yyyyMMdd}";
}
