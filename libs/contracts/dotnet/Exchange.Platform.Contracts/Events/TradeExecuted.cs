namespace Exchange.Platform.Contracts.Events;

public sealed record TradeExecuted(
    Guid TradeId,
    Guid BuyOrderId,
    Guid SellOrderId,
    string Symbol,
    decimal Price,
    decimal Quantity,
    DateTimeOffset ExecutedAt,
    int SchemaVersion = 1);
