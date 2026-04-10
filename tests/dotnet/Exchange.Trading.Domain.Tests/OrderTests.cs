using ContractOrderSource = Exchange.Platform.Contracts.OrderSource;
using Exchange.Trading.Domain.Entities;
using Exchange.Trading.Domain.Enums;
using Exchange.Trading.Domain.ValueObjects;
using Xunit;

namespace Exchange.Trading.Domain.Tests;

public sealed class OrderTests
{
    [Fact]
    public void ApplyTrade_Marks_Order_As_Filled_When_Remaining_Quantity_Reaches_Zero()
    {
        var order = Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Symbol("BTC-USD"),
            OrderSide.Buy,
            OrderType.Limit,
            new Quantity(1.5m),
            new Price(50000m),
            TimeInForce.Gtc,
            null,
            null,
            null,
            ContractOrderSource.Api,
            null,
            DateTimeOffset.UtcNow);

        order.Accept(DateTimeOffset.UtcNow);
        order.ApplyTrade(1.5m, DateTimeOffset.UtcNow);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(1.5m, order.FilledQuantity);
    }
}
