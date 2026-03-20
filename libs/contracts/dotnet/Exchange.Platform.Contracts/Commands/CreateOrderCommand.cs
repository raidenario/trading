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
    int SchemaVersion = 1);
