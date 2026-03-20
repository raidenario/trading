using Exchange.Trading.Domain.ValueObjects;

namespace Exchange.Trading.Domain.Entities;

public sealed class Trade
{
    public Trade(
        Guid tradeId,
        Guid buyOrderId,
        Guid sellOrderId,
        Symbol symbol,
        Price price,
        Quantity quantity,
        DateTimeOffset executedAt)
    {
        TradeId = tradeId;
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Symbol = symbol;
        Price = price;
        Quantity = quantity;
        ExecutedAt = executedAt;
    }

    public Guid TradeId { get; }

    public Guid BuyOrderId { get; }

    public Guid SellOrderId { get; }

    public Symbol Symbol { get; }

    public Price Price { get; }

    public Quantity Quantity { get; }

    public DateTimeOffset ExecutedAt { get; }
}
