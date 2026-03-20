namespace Exchange.Platform.Contracts.Events;

public sealed record FundsReserved(
    Guid AccountId,
    Guid OrderId,
    string Asset,
    decimal Amount,
    DateTimeOffset ReservedAt);
