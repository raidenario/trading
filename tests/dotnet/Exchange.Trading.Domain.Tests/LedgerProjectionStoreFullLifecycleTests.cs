using Exchange.Ledger.Api;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;

namespace Exchange.Trading.Domain.Tests;

public sealed class LedgerProjectionStoreFullLifecycleTests
{
    private static readonly Guid BuyerAccountId = DemoSeed.Accounts.First().AccountId;
    private static readonly Guid SellerAccountId = DemoSeed.Accounts.Skip(1).First().AccountId;
    private static readonly Guid BuyerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == BuyerAccountId).TradingAccountId;
    private static readonly Guid SellerTradingAccountId = DemoSeed.TradingAccounts.First(x => x.AccountId == SellerAccountId).TradingAccountId;
    private static readonly Instrument BtcInstrument = DemoSeed.Instruments.First(x => x.Symbol == "BTC-USD");
    private static readonly Instrument EthInstrument = DemoSeed.Instruments.First(x => x.Symbol == "ETH-USD");
    private static readonly Guid BuyOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000001");
    private static readonly Guid SellOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000002");

    [Fact]
    public void Full_Buy_Sell_lifecycle_produces_correct_positions_and_ledger_entries()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;
        var executedAt = submittedAt.AddSeconds(1);

        store.Apply(new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        var adjustments = store.Apply(new TradeExecuted(
            "trade-001", BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 1m, executedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        Assert.Equal(4, adjustments.Count);

        var buyerPosition = store.GetPositions(BuyerTradingAccountId);
        var sellerPosition = store.GetPositions(SellerTradingAccountId);

        Assert.Contains(buyerPosition, p => p.InstrumentId == BtcInstrument.InstrumentId && p.NetQuantity == 1m);
        Assert.Contains(sellerPosition, p => p.InstrumentId == BtcInstrument.InstrumentId && p.NetQuantity == -1m);

        var buyerLedger = store.GetAccount(BuyerAccountId);
        var sellerLedger = store.GetAccount(SellerAccountId);

        Assert.Contains(buyerLedger!.Entries, e => e.ReferenceType == ReferenceType.TradeExecution && e.ReferenceId == "trade-001");
        Assert.Contains(sellerLedger!.Entries, e => e.ReferenceType == ReferenceType.TradeExecution && e.ReferenceId == "trade-001");
    }

    [Fact]
    public void Partial_fills_accumulate_positions_correctly()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        store.Apply(new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-partial-1", BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.3m, submittedAt.AddSeconds(1),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPos = store.GetPositions(BuyerTradingAccountId);
        Assert.Contains(buyerPos, p => p.InstrumentId == BtcInstrument.InstrumentId && p.NetQuantity == 0.3m);

        store.Apply(new TradeExecuted(
            "trade-partial-2", BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.7m, submittedAt.AddSeconds(2),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        buyerPos = store.GetPositions(BuyerTradingAccountId);
        Assert.Contains(buyerPos, p => p.InstrumentId == BtcInstrument.InstrumentId && p.NetQuantity == 1m);
    }

    [Fact]
    public void Order_reject_releases_reserved_funds()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        var buyerBalancesBefore = store.GetBalances(BuyerAccountId);
        var usdBefore = buyerBalancesBefore.First(b => b.Asset == "USD");

        store.Apply(new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        var buyerBalancesAfter = store.GetBalances(BuyerAccountId);
        var usdAfter = buyerBalancesAfter.First(b => b.Asset == "USD");

        Assert.Equal(usdBefore.Available - 50000m, usdAfter.Available);

        store.Apply(new OrderRejected(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            "Insufficient funds", submittedAt.AddSeconds(1)));

        var buyerBalancesFinal = store.GetBalances(BuyerAccountId);
        var usdFinal = buyerBalancesFinal.First(b => b.Asset == "USD");

        Assert.Equal(usdBefore.Available, usdFinal.Available);
    }

    [Fact]
    public void Reject_after_partial_fill_releases_remaining_reserved()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        store.Apply(new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-partial-reject", BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 0.5m, submittedAt.AddSeconds(1),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPos = store.GetPositions(BuyerTradingAccountId);
        Assert.Contains(buyerPos, p => p.InstrumentId == BtcInstrument.InstrumentId && p.NetQuantity == 0.5m);

        store.Apply(new OrderRejected(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            "Cancelled after partial fill", submittedAt.AddSeconds(2)));
    }

    [Fact]
    public void Multiple_instruments_produce_independent_positions()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        var buyEthOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000003");
        var sellEthOrderId = Guid.Parse("aaaaaaaa-aaaa-4000-8000-000000000004");

        store.Apply(new CreateOrderCommand(
            buyEthOrderId, BuyerAccountId, "ETH-USD",
            OrderSide.Buy, OrderType.Limit, 2m, 3000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: EthInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            sellEthOrderId, SellerAccountId, "ETH-USD",
            OrderSide.Sell, OrderType.Limit, 2m, 3000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: EthInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-eth", buyEthOrderId, sellEthOrderId,
            BuyerAccountId, SellerAccountId,
            "ETH-USD", 3000m, 2m, submittedAt.AddSeconds(1),
            InstrumentId: EthInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerPositions = store.GetPositions(BuyerTradingAccountId);

        Assert.Contains(buyerPositions, p =>
            p.InstrumentId == EthInstrument.InstrumentId && p.NetQuantity == 2m);
    }

    [Fact]
    public void Ledger_entries_contain_trading_account_references()
    {
        var store = new LedgerProjectionStore();
        var submittedAt = DateTimeOffset.UtcNow;

        store.Apply(new CreateOrderCommand(
            BuyOrderId, BuyerAccountId, "BTC-USD",
            OrderSide.Buy, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: BuyerTradingAccountId));

        store.Apply(new CreateOrderCommand(
            SellOrderId, SellerAccountId, "BTC-USD",
            OrderSide.Sell, OrderType.Limit, 1m, 50000m,
            TimeInForce.Gtc, null, submittedAt,
            InstrumentId: BtcInstrument.InstrumentId,
            TradingAccountId: SellerTradingAccountId));

        store.Apply(new TradeExecuted(
            "trade-ref", BuyOrderId, SellOrderId,
            BuyerAccountId, SellerAccountId,
            "BTC-USD", 50000m, 1m, submittedAt.AddSeconds(1),
            InstrumentId: BtcInstrument.InstrumentId,
            BuyTradingAccountId: BuyerTradingAccountId,
            SellTradingAccountId: SellerTradingAccountId));

        var buyerLedger = store.GetAccount(BuyerAccountId);
        var tradeEntries = buyerLedger!.Entries
            .Where(e => e.ReferenceType == ReferenceType.TradeExecution)
            .ToList();

        Assert.NotEmpty(tradeEntries);
        Assert.All(tradeEntries, entry => Assert.Equal(BuyerTradingAccountId, entry.TradingAccountId));
    }
}
