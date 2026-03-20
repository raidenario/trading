namespace Exchange.Trading.Application.Models;

public sealed record OrderCancellationResult(bool Cancelled, string? Reason);
