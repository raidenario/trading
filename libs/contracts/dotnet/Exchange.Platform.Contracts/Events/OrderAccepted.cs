namespace Exchange.Platform.Contracts.Events;

public sealed record OrderAccepted(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    OrderStatus Status,
    decimal RemainingQuantity,
    DateTimeOffset AcceptedAt,
    int SchemaVersion = 1);
