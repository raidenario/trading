namespace Exchange.Platform.Contracts.Events;

public sealed record BookLevelDto(decimal Price, decimal Quantity, int OrderCount);
