namespace Exchange.Platform.Contracts.Events;

public sealed record AccountFunded(
    Guid AccountId,
    string Asset,
    decimal Amount,
    decimal NewAvailableBalance,
    DateTimeOffset FundedAt);
