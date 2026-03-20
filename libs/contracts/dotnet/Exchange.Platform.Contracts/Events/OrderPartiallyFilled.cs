namespace Exchange.Platform.Contracts.Events;

public sealed record OrderPartiallyFilled(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    decimal FilledQuantity,
    decimal RemainingQuantity,
    decimal LastTradePrice,
    DateTimeOffset UpdatedAt);
