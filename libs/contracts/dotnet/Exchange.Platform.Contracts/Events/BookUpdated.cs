namespace Exchange.Platform.Contracts.Events;

public sealed record BookUpdated(
    string Symbol,
    IReadOnlyCollection<BookLevelDto> Bids,
    IReadOnlyCollection<BookLevelDto> Asks,
    DateTimeOffset AsOf,
    int SchemaVersion = 1);
