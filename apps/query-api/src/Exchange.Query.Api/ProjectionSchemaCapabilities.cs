namespace Exchange.Query.Api;

public sealed record ProjectionSchemaCapabilities(
    bool SupportsExtendedOrders,
    bool SupportsTradeExecutions);
