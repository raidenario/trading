namespace Exchange.Platform.Contracts.Events;

public sealed record CandleUpdated(
    string Symbol,
    string Interval,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime);
