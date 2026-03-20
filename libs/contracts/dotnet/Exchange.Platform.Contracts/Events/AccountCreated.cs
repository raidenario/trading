namespace Exchange.Platform.Contracts.Events;

public sealed record AccountCreated(
    Guid AccountId,
    string DisplayName,
    string Email,
    DateTimeOffset CreatedAt);
