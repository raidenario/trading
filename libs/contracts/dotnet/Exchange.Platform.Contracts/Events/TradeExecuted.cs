namespace Exchange.Platform.Contracts.Events;

public sealed record TradeExecuted(
    string TradeId,
    Guid BuyOrderId,
    Guid SellOrderId,
    Guid BuyAccountId,
    Guid SellAccountId,
    string Symbol,
    decimal Price,
    decimal Quantity,
    DateTimeOffset ExecutedAt,
    int SchemaVersion = 1);
