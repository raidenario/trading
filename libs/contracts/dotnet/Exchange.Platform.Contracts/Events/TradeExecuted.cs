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
    Guid? InstrumentId = null,
    Guid? BuyTradingAccountId = null,
    Guid? SellTradingAccountId = null,
    OrderSide? AggressorSide = null,
    string TradeSource = "MatchingEngine",
    string? ExchangeExecutionId = null,
    Dictionary<string, string>? Metadata = null,
    int SchemaVersion = 1);
