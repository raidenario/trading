namespace Exchange.Platform.Contracts.Commands;

public sealed record CreateAccountCommand(
    Guid AccountId,
    string DisplayName,
    string Email,
    DateTimeOffset RequestedAt,
    int SchemaVersion = 1);
