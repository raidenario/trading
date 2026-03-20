using Exchange.Trading.Application.Models;
using Exchange.Trading.Domain.Entities;

namespace Exchange.Trading.Application.Abstractions;

public interface IMatchingEngineClient
{
    Task<OrderSubmissionResult> SubmitAsync(Order order, CancellationToken cancellationToken);

    Task<OrderCancellationResult> CancelAsync(Guid orderId, string symbol, CancellationToken cancellationToken);
}
