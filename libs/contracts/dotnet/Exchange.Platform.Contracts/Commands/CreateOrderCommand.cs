namespace Exchange.Platform.Contracts.Commands;

public sealed record CreateOrderCommand(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price,
    TimeInForce TimeInForce,
    string? ClientOrderId,
    DateTimeOffset SubmittedAt,
    Guid? InstrumentId = null,
    Guid? TradingAccountId = null,
    OrderSource SourceSystem = OrderSource.Api,
    Dictionary<string, string>? ExecutionInstructions = null,
    decimal? StopPrice = null,
    int SchemaVersion = 1);
