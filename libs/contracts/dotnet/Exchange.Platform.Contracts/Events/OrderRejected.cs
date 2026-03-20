namespace Exchange.Platform.Contracts.Events;

public sealed record OrderRejected(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    string Reason,
    DateTimeOffset RejectedAt,
    int SchemaVersion = 1);
