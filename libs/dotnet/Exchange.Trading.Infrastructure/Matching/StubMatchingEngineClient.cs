using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.Enums;
using Exchange.Trading.Domain.ValueObjects;

namespace Exchange.Trading.Infrastructure.Matching;

public sealed class StubMatchingEngineClient : IMatchingEngineClient
{
    public Task<OrderSubmissionResult> SubmitAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Type == OrderType.Market && order.TimeInForce == TimeInForce.Fok)
        {
            return Task.FromResult(new OrderSubmissionResult(
                false,
                OrderStatus.Rejected,
                Array.Empty<Trade>(),
                null,
                "FOK market orders are not supported in the initial scaffold."));
        }

        var trades = new List<Trade>();
        var bestBid = new PriceLevel(new Price(49950m), new Quantity(1.25m), 2);
        var bestAsk = new PriceLevel(new Price(50025m), new Quantity(1.40m), 2);

        if (order.Type == OrderType.Limit &&
            order.LimitPrice is not null &&
            order.Side == OrderSide.Buy &&
            order.LimitPrice.Value.Value >= bestAsk.Price.Value)
        {
            var fillQuantity = Math.Min(order.OriginalQuantity.Value, 0.50m);
            trades.Add(new Trade(
                Guid.NewGuid(),
                order.OrderId,
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                order.InstrumentId,
                order.TradingAccountId,
                null,
                order.Symbol,
                bestAsk.Price,
                new Quantity(fillQuantity),
                DateTimeOffset.UtcNow));
        }

        if (order.Type == OrderType.Limit &&
            order.LimitPrice is not null &&
            order.Side == OrderSide.Sell &&
            order.LimitPrice.Value.Value <= bestBid.Price.Value)
        {
            var fillQuantity = Math.Min(order.OriginalQuantity.Value, 0.50m);
            trades.Add(new Trade(
                Guid.NewGuid(),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                order.OrderId,
                order.InstrumentId,
                null,
                order.TradingAccountId,
                order.Symbol,
                bestBid.Price,
                new Quantity(fillQuantity),
                DateTimeOffset.UtcNow));
        }

        var filledQuantity = trades.Sum(trade => trade.Quantity.Value);
        var status = trades.Count == 0
            ? OrderStatus.Accepted
            : filledQuantity >= order.OriginalQuantity.Value
                ? OrderStatus.Filled
                : OrderStatus.PartiallyFilled;

        var book = new OrderBook(
            order.Symbol,
            new[] { bestBid },
            new[] { bestAsk },
            DateTimeOffset.UtcNow);

        return Task.FromResult(new OrderSubmissionResult(true, status, trades, book, null));
    }

    public Task<OrderCancellationResult> CancelAsync(Guid orderId, string symbol, CancellationToken cancellationToken) =>
        Task.FromResult(new OrderCancellationResult(true, null));
}
