using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Domain.Entities;

namespace Exchange.Trading.Application.Services;

public interface IOrderCommandService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken);

    Task<OrderCancellationResult> CancelAsync(CancelOrderCommand command, CancellationToken cancellationToken);

    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Order>> ListAsync(Guid? accountId, CancellationToken cancellationToken);
}
