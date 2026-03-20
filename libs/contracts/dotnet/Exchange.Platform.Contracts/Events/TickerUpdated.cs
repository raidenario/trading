namespace Exchange.Platform.Contracts.Events;

public sealed record TickerUpdated(
    string Symbol,
    decimal LastPrice,
    decimal BestBid,
    decimal BestAsk,
    decimal Volume24H,
    decimal Change24H,
    DateTimeOffset AsOf,
    int SchemaVersion = 1);
