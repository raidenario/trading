namespace Exchange.Platform.Contracts.Commands;

public sealed record CancelOrderCommand(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    DateTimeOffset RequestedAt,
    int SchemaVersion = 1);
