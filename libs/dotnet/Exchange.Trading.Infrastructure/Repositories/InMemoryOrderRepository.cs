using System.Collections.Concurrent;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Domain.Entities;

namespace Exchange.Trading.Infrastructure.Repositories;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public Task UpsertAsync(Order order, CancellationToken cancellationToken)
    {
        _orders[order.OrderId] = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        _orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task<IReadOnlyCollection<Order>> ListByAccountAsync(Guid? accountId, CancellationToken cancellationToken)
    {
        var result = accountId.HasValue
            ? _orders.Values.Where(order => order.AccountId == accountId.Value).OrderByDescending(order => order.CreatedAt).ToArray()
            : _orders.Values.OrderByDescending(order => order.CreatedAt).ToArray();

        return Task.FromResult<IReadOnlyCollection<Order>>(result);
    }
}
