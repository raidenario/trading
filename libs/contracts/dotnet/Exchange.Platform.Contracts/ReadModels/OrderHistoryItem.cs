namespace Exchange.Platform.Contracts.ReadModels;

public sealed record OrderHistoryItem(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    OrderStatus Status,
    decimal Quantity,
    decimal FilledQuantity,
    decimal? Price,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
