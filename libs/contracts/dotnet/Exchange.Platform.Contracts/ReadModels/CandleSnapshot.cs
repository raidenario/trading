namespace Exchange.Platform.Contracts.ReadModels;

public sealed record CandleSnapshot(
    string Symbol,
    string Interval,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt);
