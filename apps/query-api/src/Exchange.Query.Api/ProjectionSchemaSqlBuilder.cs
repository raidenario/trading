using System.Collections.ObjectModel;

namespace Exchange.Query.Api;

public static class ProjectionSchemaSqlBuilder
{
    private static readonly ReadOnlyCollection<string> ExtendedOrderColumns =
        Array.AsReadOnly(
        [
            "instrument_id",
            "trading_account_id",
            "source_system",
            "execution_instructions",
            "stop_price"
        ]);

    public static bool SupportsExtendedOrders(IReadOnlyCollection<string> orderColumns) =>
        ExtendedOrderColumns.All(column => orderColumns.Contains(column, StringComparer.OrdinalIgnoreCase));

    public static string BuildUpsertOrderSql(ProjectionSchemaCapabilities capabilities) =>
        capabilities.SupportsExtendedOrders
            ? """
              INSERT INTO orders (
                  order_id, account_id, symbol, side, order_type, time_in_force,
                  quantity, limit_price, filled_quantity, remaining_quantity,
                  status, rejection_reason, client_order_id, instrument_id,
                  trading_account_id, source_system, execution_instructions, stop_price,
                  created_at, updated_at
              )
              VALUES (
                  @order_id, @account_id, @symbol, @side, @order_type, @time_in_force,
                  @quantity, @limit_price, @filled_quantity, @remaining_quantity,
                  @status, @rejection_reason, @client_order_id, @instrument_id,
                  @trading_account_id, @source_system, @execution_instructions::jsonb, @stop_price,
                  @created_at, @updated_at
              )
              ON CONFLICT (order_id) DO UPDATE
              SET account_id = EXCLUDED.account_id,
                  symbol = EXCLUDED.symbol,
                  side = EXCLUDED.side,
                  order_type = EXCLUDED.order_type,
                  time_in_force = EXCLUDED.time_in_force,
                  quantity = EXCLUDED.quantity,
                  limit_price = EXCLUDED.limit_price,
                  filled_quantity = EXCLUDED.filled_quantity,
                  remaining_quantity = EXCLUDED.remaining_quantity,
                  status = EXCLUDED.status,
                  rejection_reason = EXCLUDED.rejection_reason,
                  client_order_id = EXCLUDED.client_order_id,
                  instrument_id = EXCLUDED.instrument_id,
                  trading_account_id = EXCLUDED.trading_account_id,
                  source_system = EXCLUDED.source_system,
                  execution_instructions = EXCLUDED.execution_instructions,
                  stop_price = EXCLUDED.stop_price,
                  updated_at = EXCLUDED.updated_at;
              """
            : """
              INSERT INTO orders (
                  order_id, account_id, symbol, side, order_type, time_in_force,
                  quantity, limit_price, filled_quantity, remaining_quantity,
                  status, rejection_reason, client_order_id, created_at, updated_at
              )
              VALUES (
                  @order_id, @account_id, @symbol, @side, @order_type, @time_in_force,
                  @quantity, @limit_price, @filled_quantity, @remaining_quantity,
                  @status, @rejection_reason, @client_order_id, @created_at, @updated_at
              )
              ON CONFLICT (order_id) DO UPDATE
              SET account_id = EXCLUDED.account_id,
                  symbol = EXCLUDED.symbol,
                  side = EXCLUDED.side,
                  order_type = EXCLUDED.order_type,
                  time_in_force = EXCLUDED.time_in_force,
                  quantity = EXCLUDED.quantity,
                  limit_price = EXCLUDED.limit_price,
                  filled_quantity = EXCLUDED.filled_quantity,
                  remaining_quantity = EXCLUDED.remaining_quantity,
                  status = EXCLUDED.status,
                  rejection_reason = EXCLUDED.rejection_reason,
                  client_order_id = EXCLUDED.client_order_id,
                  updated_at = EXCLUDED.updated_at;
              """;
}
