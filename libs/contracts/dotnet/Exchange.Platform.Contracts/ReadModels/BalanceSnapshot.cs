namespace Exchange.Platform.Contracts.ReadModels;

public sealed record BalanceSnapshot(
    Guid AccountId,
    string Asset,
    decimal Available,
    decimal Locked,
    DateTimeOffset AsOf);
