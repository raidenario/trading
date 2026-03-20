namespace Exchange.Platform.Contracts.Commands;

public sealed record FundAccountCommand(
    Guid AccountId,
    string Asset,
    decimal Amount,
    string? ReferenceId,
    DateTimeOffset RequestedAt,
    int SchemaVersion = 1);
