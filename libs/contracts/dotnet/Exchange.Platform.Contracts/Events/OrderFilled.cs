namespace Exchange.Platform.Contracts.Events;

public sealed record OrderFilled(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    decimal FilledQuantity,
    decimal AveragePrice,
    DateTimeOffset FilledAt);
