using Exchange.Ledger.Api;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;

namespace Exchange.Trading.Domain.Tests;

public sealed class LedgerProjectionStoreTests
{
    [Fact]
    public void Apply_trade_execution_keeps_balances_and_updates_positions()
    {
        var store = new LedgerProjectionStore();
        var buyer = DemoSeed.Accounts.First().AccountId;
        var seller = DemoSeed.Accounts.Skip(1).First().AccountId;
        var instrument = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");
        var buyTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == buyer).TradingAccountId;
        var sellTradingAccount = DemoSeed.TradingAccounts.First(x => x.AccountId == seller).TradingAccountId;
        var buyOrderId = Guid.NewGuid();
        var sellOrderId = Guid.NewGuid();

        store.Apply(new CreateOrderCommand(
            buyOrderId,
            buyer,
            "BTC-USD",
            OrderSide.Buy,
            OrderType.Limit,
            1m,
            50000m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.UtcNow,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: buyTradingAccount));

        store.Apply(new CreateOrderCommand(
            sellOrderId,
            seller,
            "BTC-USD",
            OrderSide.Sell,
            OrderType.Limit,
            1m,
            50000m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.UtcNow,
            InstrumentId: instrument.InstrumentId,
            TradingAccountId: sellTradingAccount));

        var adjustments = store.Apply(new TradeExecuted(
            "trade-0001",
            buyOrderId,
            sellOrderId,
            buyer,
            seller,
            "BTC-USD",
            50000m,
            1m,
            DateTimeOffset.UtcNow,
            InstrumentId: instrument.InstrumentId,
            BuyTradingAccountId: buyTradingAccount,
            SellTradingAccountId: sellTradingAccount));

        var buyerPositions = store.GetPositions(buyTradingAccount);
        var sellerPositions = store.GetPositions(sellTradingAccount);
        var buyerLedger = store.GetAccount(buyer);

        Assert.Equal(4, adjustments.Count);
        Assert.Contains(buyerPositions, position => position.InstrumentId == instrument.InstrumentId && position.NetQuantity == 1m);
        Assert.Contains(sellerPositions, position => position.InstrumentId == instrument.InstrumentId && position.NetQuantity == -1m);
        Assert.Contains(buyerLedger!.Entries, entry => entry.ReferenceType == ReferenceType.TradeExecution && entry.ReferenceId == "trade-0001");
    }

    [Fact]
    public void Apply_create_order_resolves_reserved_asset_from_instrument_catalog_for_new_books()
    {
        var store = new LedgerProjectionStore();
        var accountId = DemoSeed.Accounts.First().AccountId;
        var tradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == accountId).TradingAccountId;
        var equity = DemoSeed.Instruments.First(x => x.Symbol == "PETR4");
        var bdr = DemoSeed.Instruments.First(x => x.Symbol == "MSFT34");

        store.Apply(new CreateOrderCommand(
            Guid.NewGuid(),
            accountId,
            equity.Symbol,
            OrderSide.Buy,
            OrderType.Limit,
            100m,
            37.10m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.UtcNow,
            InstrumentId: equity.InstrumentId,
            TradingAccountId: tradingAccountId));

        store.Apply(new CreateOrderCommand(
            Guid.NewGuid(),
            accountId,
            bdr.Symbol,
            OrderSide.Sell,
            OrderType.Limit,
            10m,
            61.70m,
            TimeInForce.Gtc,
            null,
            DateTimeOffset.UtcNow,
            InstrumentId: bdr.InstrumentId,
            TradingAccountId: tradingAccountId));

        var balances = store.GetBalances(accountId);

        Assert.Contains(balances, balance => balance.Asset == "BRL" && balance.Locked >= 3710m);
        Assert.Contains(balances, balance => balance.Asset == "MSFT" && balance.Locked >= 10m);
        Assert.DoesNotContain(balances, balance => balance.Asset == "MSFT34" && balance.Locked > 0m);
    }
}
