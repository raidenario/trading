using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.Enums;

namespace Exchange.Trading.Application.Models;

public sealed record CreateOrderResult(
    Guid OrderId,
    OrderStatus Status,
    string? RejectionReason,
    IReadOnlyCollection<Trade> Trades,
    OrderBook? Book);
