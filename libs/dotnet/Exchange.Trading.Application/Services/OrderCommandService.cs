using Exchange.Platform.Contracts.Commands;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.ValueObjects;
using ContractOrderSide = Exchange.Platform.Contracts.OrderSide;
using ContractOrderStatus = Exchange.Platform.Contracts.OrderStatus;
using ContractOrderType = Exchange.Platform.Contracts.OrderType;
using ContractTimeInForce = Exchange.Platform.Contracts.TimeInForce;
using DomainOrderSide = Exchange.Trading.Domain.Enums.OrderSide;
using DomainOrderStatus = Exchange.Trading.Domain.Enums.OrderStatus;
using DomainOrderType = Exchange.Trading.Domain.Enums.OrderType;
using DomainTimeInForce = Exchange.Trading.Domain.Enums.TimeInForce;

namespace Exchange.Trading.Application.Services;

public sealed class OrderCommandService(
    IOrderRepository orderRepository,
    IMatchingEngineClient matchingEngineClient) : IOrderCommandService
{
    public async Task<CreateOrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(
            command.OrderId,
            command.AccountId,
            new Symbol(command.Symbol),
            Map(command.Side),
            Map(command.Type),
            new Quantity(command.Quantity),
            command.Price.HasValue ? new Price(command.Price.Value) : null,
            Map(command.TimeInForce),
            command.ClientOrderId,
            command.SubmittedAt);

        var submission = await matchingEngineClient.SubmitAsync(order, cancellationToken);

        if (!submission.Accepted)
        {
            order.Reject(submission.RejectionReason ?? "Rejected by matching engine.", DateTimeOffset.UtcNow);
            await orderRepository.UpsertAsync(order, cancellationToken);

            return new CreateOrderResult(order.OrderId, order.Status, order.RejectionReason, Array.Empty<Trade>(), submission.Book);
        }

        await orderRepository.UpsertAsync(order, cancellationToken);
        return new CreateOrderResult(order.OrderId, submission.Status, null, submission.Trades, submission.Book);
    }

    public async Task<OrderCancellationResult> CancelAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return new OrderCancellationResult(false, "Order was not found.");
        }

        var matchingResult = await matchingEngineClient.CancelAsync(command.OrderId, command.Symbol, cancellationToken);
        if (!matchingResult.Cancelled)
        {
            return matchingResult;
        }

        order.Cancel(command.RequestedAt);
        await orderRepository.UpsertAsync(order, cancellationToken);

        return new OrderCancellationResult(true, null);
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        orderRepository.GetByIdAsync(orderId, cancellationToken);

    public Task<IReadOnlyCollection<Order>> ListAsync(Guid? accountId, CancellationToken cancellationToken) =>
        orderRepository.ListByAccountAsync(accountId, cancellationToken);

    private static DomainOrderSide Map(ContractOrderSide side) =>
        side == ContractOrderSide.Buy ? DomainOrderSide.Buy : DomainOrderSide.Sell;

    private static DomainOrderType Map(ContractOrderType type) =>
        type == ContractOrderType.Limit ? DomainOrderType.Limit : DomainOrderType.Market;

    private static DomainTimeInForce Map(ContractTimeInForce timeInForce) => timeInForce switch
    {
        ContractTimeInForce.Ioc => DomainTimeInForce.Ioc,
        ContractTimeInForce.Fok => DomainTimeInForce.Fok,
        _ => DomainTimeInForce.Gtc
    };
}
