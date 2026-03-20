using Exchange.Trading.Domain.ValueObjects;

namespace Exchange.Trading.Domain.Entities;

public sealed class PriceLevel
{
    public PriceLevel(Price price, Quantity totalQuantity, int orderCount)
    {
        Price = price;
        TotalQuantity = totalQuantity;
        OrderCount = orderCount;
    }

    public Price Price { get; }

    public Quantity TotalQuantity { get; }

    public int OrderCount { get; }
}
