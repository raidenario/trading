namespace Exchange.Platform.Contracts.ReadModels;

public sealed record TickerSnapshot(
    string Symbol,
    decimal LastPrice,
    decimal BestBid,
    decimal BestAsk,
    decimal High24H,
    decimal Low24H,
    decimal Volume24H,
    decimal Change24H,
    DateTimeOffset AsOf);
