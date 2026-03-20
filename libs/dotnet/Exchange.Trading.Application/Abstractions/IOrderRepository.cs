using Exchange.Trading.Domain.Entities;

namespace Exchange.Trading.Application.Abstractions;

public interface IOrderRepository
{
    Task UpsertAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Order>> ListByAccountAsync(Guid? accountId, CancellationToken cancellationToken);
}
