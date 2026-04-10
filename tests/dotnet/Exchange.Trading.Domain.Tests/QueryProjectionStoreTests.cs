using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Query.Api;

namespace Exchange.Trading.Domain.Tests;

public sealed class QueryProjectionStoreTests
{
    [Fact]
    public void Projects_instruments_positions_and_enriched_trades_without_breaking_existing_history()
    {
        var store = new QueryProjectionStore();
        var buyer = DemoSeed.Accounts.First().AccountId;
        var seller = DemoSeed.Accounts.Skip(1).First().AccountId;
        var instrument = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");
        var buyTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == buyer).TradingAccountId;
        var sellTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == seller).TradingAccountId;
        var buyOrderId = Guid.NewGuid();
        var sellOrderId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;

        store.Apply(new CreateOrderCommand(
            buyOrderId,
            buyer,
            "BTC-USD",
            OrderSide.Buy,
            OrderType.Limit,
            0.5m,
            50000m,
            TimeInForce.Gtc,
            "buy-1",
            submittedAt,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: buyTradingAccount));

        store.Apply(new CreateOrderCommand(
            sellOrderId,
            seller,
            "BTC-USD",
            OrderSide.Sell,
            OrderType.Limit,
            0.5m,
            50000m,
            TimeInForce.Gtc,
            "sell-1",
            submittedAt,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: sellTradingAccount));

        store.Apply(new TradeExecuted(
            "trade-1",
            buyOrderId,
            sellOrderId,
            buyer,
            seller,
            "BTC-USD",
            50000m,
            0.5m,
            submittedAt.AddSeconds(1),
            InstrumentId: instrument.InstrumentId,
            BuyTradingAccountId: buyTradingAccount,
            SellTradingAccountId: sellTradingAccount));

        Assert.Contains(store.GetOrderHistory(buyer), item => item.OrderId == buyOrderId);
        Assert.Contains(store.GetInstruments(), item => item.Symbol == "BTC-USD");
        Assert.Contains(store.GetPositions(buyTradingAccount), item => item.NetQuantity == 0.5m && item.InstrumentId == instrument.InstrumentId);
        Assert.Contains(store.GetEnrichedTrades("BTC-USD", 10), item => item.InstrumentId == instrument.InstrumentId && item.BuyTradingAccountId == buyTradingAccount);
        Assert.Contains(store.GetEnrichedOrders(buyer), item => item.TradingAccountId == buyTradingAccount && item.InstrumentId == instrument.InstrumentId);
    }
}
