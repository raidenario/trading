using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.Enums;

namespace Exchange.Trading.Application.Models;

public sealed record OrderSubmissionResult(
    bool Accepted,
    OrderStatus Status,
    IReadOnlyCollection<Trade> Trades,
    OrderBook? Book,
    string? RejectionReason);
