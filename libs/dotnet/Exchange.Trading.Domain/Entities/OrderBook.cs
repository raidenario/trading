using Exchange.Trading.Domain.ValueObjects;

namespace Exchange.Trading.Domain.Entities;

public sealed class OrderBook
{
    public OrderBook(Symbol symbol, IReadOnlyCollection<PriceLevel> bids, IReadOnlyCollection<PriceLevel> asks, DateTimeOffset asOf)
    {
        Symbol = symbol;
        Bids = bids;
        Asks = asks;
        AsOf = asOf;
    }

    public Symbol Symbol { get; }

    public IReadOnlyCollection<PriceLevel> Bids { get; }

    public IReadOnlyCollection<PriceLevel> Asks { get; }

    public DateTimeOffset AsOf { get; }
}
