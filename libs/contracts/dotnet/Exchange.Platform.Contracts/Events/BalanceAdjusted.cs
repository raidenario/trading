namespace Exchange.Platform.Contracts.Events;

public sealed record BalanceAdjusted(
    Guid AccountId,
    string Asset,
    decimal AvailableDelta,
    decimal ReservedDelta,
    string Reason,
    DateTimeOffset OccurredAt,
    Guid? OrderId = null,
    string? TradeId = null,
    int SchemaVersion = 1);
