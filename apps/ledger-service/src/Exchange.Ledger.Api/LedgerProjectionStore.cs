using System.Collections.Concurrent;
using Exchange.Ledger.Domain.Entities;
using Exchange.Ledger.Domain.Enums;
using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Ledger.Api;

public sealed class LedgerProjectionStore
{
    private readonly ConcurrentDictionary<Guid, LedgerAccountState> _accounts = new();
    private readonly ConcurrentDictionary<Guid, ReservedOrderState> _orders = new();
    private readonly ConcurrentDictionary<Guid, List<PendingLedgerEvent>> _pendingEvents = new();
    private readonly ConcurrentDictionary<string, PositionState> _positions = new(StringComparer.OrdinalIgnoreCase);

    public LedgerProjectionStore()
    {
        foreach (var account in DemoSeed.Accounts)
        {
            _accounts[account.AccountId] = new LedgerAccountState(account.AccountId);
        }

        foreach (var balance in DemoSeed.Balances)
        {
            var state = _accounts.GetOrAdd(balance.AccountId, accountId => new LedgerAccountState(accountId));
            state.Balances[balance.Asset] = new LedgerBalanceState(balance.Available, balance.Reserved);
        }
    }

    public void Apply(AccountCreated accountCreated)
    {
        _accounts.GetOrAdd(accountCreated.AccountId, accountId => new LedgerAccountState(accountId));
    }

    public BalanceAdjusted Apply(AccountFunded accountFunded)
    {
        var account = _accounts.GetOrAdd(accountFunded.AccountId, accountId => new LedgerAccountState(accountId));
        ApplyDelta(accountFunded.AccountId, accountFunded.Asset, accountFunded.Amount, 0m);
        account.Entries.Insert(0, new LedgerEntry(
            Guid.NewGuid(),
            accountFunded.AccountId,
            accountFunded.Asset,
            accountFunded.Amount,
            LedgerEntryType.Deposit,
            BalanceBucket.Available,
            EntryDirection.Credit,
            ReferenceType.Funding,
            "funding",
            accountFunded.FundedAt));

        return new BalanceAdjusted(
            accountFunded.AccountId,
            accountFunded.Asset,
            accountFunded.Amount,
            0m,
            "Funding",
            accountFunded.FundedAt);
    }

    public IReadOnlyCollection<BalanceAdjusted> Apply(CreateOrderCommand command)
    {
        if (command.Quantity <= 0)
        {
            return [];
        }

        var account = _accounts.GetOrAdd(command.AccountId, accountId => new LedgerAccountState(accountId));
        var instrument = ResolveInstrument(command.Symbol, command.InstrumentId);
        var baseAsset = instrument.BaseAsset.ToUpperInvariant();
        var quoteAsset = instrument.QuoteAsset.ToUpperInvariant();
        var reservedAsset = command.Side == OrderSide.Buy ? quoteAsset : baseAsset;
        var reservedAmount = command.Side == OrderSide.Buy
            ? decimal.Round((command.Price ?? 0m) * command.Quantity, 8, MidpointRounding.ToZero)
            : decimal.Round(command.Quantity, 8, MidpointRounding.ToZero);

        if (reservedAmount <= 0)
        {
            return [];
        }

        var orderState = new ReservedOrderState(
            command.OrderId,
            command.AccountId,
            ResolveTradingAccountId(command.AccountId, command.TradingAccountId),
            instrument.InstrumentId,
            command.Symbol,
            baseAsset,
            quoteAsset,
            reservedAsset,
            reservedAmount,
            command.Quantity,
            command.Side);

        _orders[command.OrderId] = orderState;

        ApplyDelta(command.AccountId, reservedAsset, -reservedAmount, reservedAmount);
        account.Entries.Insert(0, new LedgerEntry(
            Guid.NewGuid(),
            command.AccountId,
            reservedAsset,
            -reservedAmount,
            LedgerEntryType.Hold,
            BalanceBucket.Available,
            EntryDirection.Debit,
            ReferenceType.Order,
            command.OrderId.ToString(),
            command.SubmittedAt,
            orderState.TradingAccountId,
            new Dictionary<string, string> { ["symbol"] = command.Symbol }));

        var adjustments = new List<BalanceAdjusted>
        {
            new BalanceAdjusted(
                command.AccountId,
                reservedAsset,
                -reservedAmount,
                reservedAmount,
                "OrderReserved",
                command.SubmittedAt,
                command.OrderId)
        };

        adjustments.AddRange(ApplyPendingEvents(command.OrderId));
        return adjustments;
    }

    public BalanceAdjusted? Apply(OrderRejected orderRejected)
    {
        if (!_orders.TryRemove(orderRejected.OrderId, out var order))
        {
            EnqueuePending(orderRejected.OrderId, PendingLedgerEvent.ForReject(orderRejected));
            return null;
        }

        ApplyDelta(order.AccountId, order.ReservedAsset, order.OutstandingReserved, -order.OutstandingReserved);
        _accounts[order.AccountId].Entries.Insert(0, new LedgerEntry(
            Guid.NewGuid(),
            order.AccountId,
            order.ReservedAsset,
            order.OutstandingReserved,
            LedgerEntryType.Release,
            BalanceBucket.Available,
            EntryDirection.Credit,
            ReferenceType.Order,
            order.OrderId.ToString(),
            orderRejected.RejectedAt,
            order.TradingAccountId,
            new Dictionary<string, string> { ["symbol"] = order.Symbol }));

        return new BalanceAdjusted(
            order.AccountId,
            order.ReservedAsset,
            order.OutstandingReserved,
            -order.OutstandingReserved,
            "OrderRejectedRelease",
            orderRejected.RejectedAt,
            order.OrderId);
    }

    public IReadOnlyCollection<BalanceAdjusted> Apply(TradeExecuted tradeExecuted)
    {
        var adjustments = new List<BalanceAdjusted>();

        if (_orders.TryGetValue(tradeExecuted.BuyOrderId, out var buyOrder))
        {
            adjustments.AddRange(ApplyTradeToOrder(buyOrder, tradeExecuted, isBuyer: true));
        }
        else
        {
            EnqueuePending(tradeExecuted.BuyOrderId, PendingLedgerEvent.ForTrade(tradeExecuted, isBuyer: true));
        }

        if (_orders.TryGetValue(tradeExecuted.SellOrderId, out var sellOrder))
        {
            adjustments.AddRange(ApplyTradeToOrder(sellOrder, tradeExecuted, isBuyer: false));
        }
        else
        {
            EnqueuePending(tradeExecuted.SellOrderId, PendingLedgerEvent.ForTrade(tradeExecuted, isBuyer: false));
        }

        return adjustments;
    }

    public LedgerAccount? GetAccount(Guid accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var state))
        {
            return null;
        }

        var balances = state.Balances
            .OrderBy(item => item.Key)
            .Select(item => new LedgerBalance(item.Key, item.Value.Available, item.Value.Reserved))
            .ToArray();

        return new LedgerAccount(accountId, balances, state.Entries.Take(100).ToArray());
    }

    public IReadOnlyCollection<BalanceSnapshot> GetBalances(Guid accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var state))
        {
            return [];
        }

        return state.Balances
            .OrderBy(item => item.Key)
            .Select(item => new BalanceSnapshot(accountId, item.Key, item.Value.Available, item.Value.Reserved, DateTimeOffset.UtcNow))
            .ToArray();
    }

    public IReadOnlyCollection<PositionSnapshot> GetPositions(Guid tradingAccountId) =>
        _positions.Values
            .Where(position => position.TradingAccountId == tradingAccountId)
            .OrderBy(position => position.Symbol)
            .Select(position => new PositionSnapshot(
                position.PositionId,
                position.TradingAccountId,
                position.InstrumentId,
                position.Symbol,
                position.PositionDate,
                position.NetQuantity,
                position.AverageOpenPrice,
                position.LongQuantity,
                position.ShortQuantity,
                position.UpdatedAt))
            .ToArray();

    private IReadOnlyCollection<BalanceAdjusted> ApplyTradeToOrder(ReservedOrderState order, TradeExecuted tradeExecuted, bool isBuyer)
    {
        var effectiveAccountId = isBuyer ? tradeExecuted.BuyAccountId : tradeExecuted.SellAccountId;
        var effectiveTradingAccountId = isBuyer
            ? tradeExecuted.BuyTradingAccountId ?? order.TradingAccountId
            : tradeExecuted.SellTradingAccountId ?? order.TradingAccountId;
        var account = _accounts.GetOrAdd(effectiveAccountId, accountId => new LedgerAccountState(accountId));
        var adjustments = new List<BalanceAdjusted>();
        var notional = decimal.Round(tradeExecuted.Price * tradeExecuted.Quantity, 8, MidpointRounding.ToZero);
        var quantity = decimal.Round(tradeExecuted.Quantity, 8, MidpointRounding.ToZero);

        if (isBuyer)
        {
            ApplyDelta(effectiveAccountId, order.QuoteAsset, 0m, -notional);
            ApplyDelta(effectiveAccountId, order.BaseAsset, quantity, 0m);
            order.OutstandingReserved = decimal.Round(order.OutstandingReserved - notional, 8, MidpointRounding.ToZero);
            order.FilledQuantity = decimal.Round(order.FilledQuantity + quantity, 8, MidpointRounding.ToZero);
            UpdatePosition(effectiveTradingAccountId, order.InstrumentId, order.Symbol, quantity, tradeExecuted.Price, tradeExecuted.ExecutedAt);

            account.Entries.Insert(0, new LedgerEntry(Guid.NewGuid(), effectiveAccountId, order.QuoteAsset, -notional, LedgerEntryType.TradeSettlement, BalanceBucket.Reserved, EntryDirection.Debit, ReferenceType.TradeExecution, tradeExecuted.TradeId, tradeExecuted.ExecutedAt, effectiveTradingAccountId));
            account.Entries.Insert(0, new LedgerEntry(Guid.NewGuid(), effectiveAccountId, order.BaseAsset, quantity, LedgerEntryType.TradeSettlement, BalanceBucket.Available, EntryDirection.Credit, ReferenceType.TradeExecution, tradeExecuted.TradeId, tradeExecuted.ExecutedAt, effectiveTradingAccountId));

            adjustments.Add(new BalanceAdjusted(effectiveAccountId, order.QuoteAsset, 0m, -notional, "TradeSettlementDebit", tradeExecuted.ExecutedAt, order.OrderId, tradeExecuted.TradeId));
            adjustments.Add(new BalanceAdjusted(effectiveAccountId, order.BaseAsset, quantity, 0m, "TradeSettlementCredit", tradeExecuted.ExecutedAt, order.OrderId, tradeExecuted.TradeId));
        }
        else
        {
            ApplyDelta(effectiveAccountId, order.BaseAsset, 0m, -quantity);
            ApplyDelta(effectiveAccountId, order.QuoteAsset, notional, 0m);
            order.OutstandingReserved = decimal.Round(order.OutstandingReserved - quantity, 8, MidpointRounding.ToZero);
            order.FilledQuantity = decimal.Round(order.FilledQuantity + quantity, 8, MidpointRounding.ToZero);
            UpdatePosition(effectiveTradingAccountId, order.InstrumentId, order.Symbol, -quantity, tradeExecuted.Price, tradeExecuted.ExecutedAt);

            account.Entries.Insert(0, new LedgerEntry(Guid.NewGuid(), effectiveAccountId, order.BaseAsset, -quantity, LedgerEntryType.TradeSettlement, BalanceBucket.Reserved, EntryDirection.Debit, ReferenceType.TradeExecution, tradeExecuted.TradeId, tradeExecuted.ExecutedAt, effectiveTradingAccountId));
            account.Entries.Insert(0, new LedgerEntry(Guid.NewGuid(), effectiveAccountId, order.QuoteAsset, notional, LedgerEntryType.TradeSettlement, BalanceBucket.Available, EntryDirection.Credit, ReferenceType.TradeExecution, tradeExecuted.TradeId, tradeExecuted.ExecutedAt, effectiveTradingAccountId));

            adjustments.Add(new BalanceAdjusted(effectiveAccountId, order.BaseAsset, 0m, -quantity, "TradeSettlementDebit", tradeExecuted.ExecutedAt, order.OrderId, tradeExecuted.TradeId));
            adjustments.Add(new BalanceAdjusted(effectiveAccountId, order.QuoteAsset, notional, 0m, "TradeSettlementCredit", tradeExecuted.ExecutedAt, order.OrderId, tradeExecuted.TradeId));
        }

        if (order.FilledQuantity >= order.Quantity && order.OutstandingReserved > 0)
        {
            ApplyDelta(effectiveAccountId, order.ReservedAsset, order.OutstandingReserved, -order.OutstandingReserved);
            account.Entries.Insert(0, new LedgerEntry(Guid.NewGuid(), effectiveAccountId, order.ReservedAsset, order.OutstandingReserved, LedgerEntryType.Release, BalanceBucket.Available, EntryDirection.Credit, ReferenceType.Order, order.OrderId.ToString(), tradeExecuted.ExecutedAt, effectiveTradingAccountId));
            adjustments.Add(new BalanceAdjusted(effectiveAccountId, order.ReservedAsset, order.OutstandingReserved, -order.OutstandingReserved, "OrderCompletedRelease", tradeExecuted.ExecutedAt, order.OrderId, tradeExecuted.TradeId));
            order.OutstandingReserved = 0m;
        }

        if (order.FilledQuantity >= order.Quantity || order.OutstandingReserved <= 0m)
        {
            _orders.TryRemove(order.OrderId, out _);
        }

        return adjustments;
    }

    private void ApplyDelta(Guid accountId, string asset, decimal availableDelta, decimal reservedDelta)
    {
        var account = _accounts.GetOrAdd(accountId, id => new LedgerAccountState(id));
        var balance = account.Balances.GetOrAdd(asset.ToUpperInvariant(), _ => new LedgerBalanceState(0m, 0m));
        balance.Available = decimal.Round(balance.Available + availableDelta, 8, MidpointRounding.ToZero);
        balance.Reserved = decimal.Round(balance.Reserved + reservedDelta, 8, MidpointRounding.ToZero);
    }

    private static Guid ResolveTradingAccountId(Guid accountId, Guid? tradingAccountId) =>
        tradingAccountId ?? DemoSeed.TradingAccounts.First(account => account.AccountId == accountId).TradingAccountId;

    private static Instrument ResolveInstrument(string symbol, Guid? instrumentId)
    {
        if (instrumentId.HasValue)
        {
            return DemoSeed.Instruments.First(instrument => instrument.InstrumentId == instrumentId.Value);
        }

        return DemoSeed.Instruments.First(instrument => instrument.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdatePosition(Guid tradingAccountId, Guid instrumentId, string symbol, decimal netDelta, decimal tradePrice, DateTimeOffset updatedAt)
    {
        var positionDate = DateOnly.FromDateTime(updatedAt.UtcDateTime);
        var key = PositionKey(tradingAccountId, instrumentId, positionDate);
        var position = _positions.GetOrAdd(key, _ => new PositionState(Guid.NewGuid(), tradingAccountId, instrumentId, symbol, positionDate, updatedAt));
        position.NetQuantity = decimal.Round(position.NetQuantity + netDelta, 8, MidpointRounding.ToZero);
        position.LongQuantity = position.NetQuantity > 0 ? position.NetQuantity : 0m;
        position.ShortQuantity = position.NetQuantity < 0 ? Math.Abs(position.NetQuantity) : 0m;
        position.AverageOpenPrice = tradePrice;
        position.UpdatedAt = updatedAt;
    }

    private static string PositionKey(Guid tradingAccountId, Guid instrumentId, DateOnly positionDate) =>
        $"{tradingAccountId:N}:{instrumentId:N}:{positionDate:yyyyMMdd}";

    private sealed class LedgerAccountState(Guid accountId)
    {
        public Guid AccountId { get; } = accountId;
        public ConcurrentDictionary<string, LedgerBalanceState> Balances { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<LedgerEntry> Entries { get; } = [];
    }

    private sealed class LedgerBalanceState(decimal available, decimal reserved)
    {
        public decimal Available { get; set; } = available;
        public decimal Reserved { get; set; } = reserved;
    }

    private sealed class ReservedOrderState(
        Guid orderId,
        Guid accountId,
        Guid tradingAccountId,
        Guid instrumentId,
        string symbol,
        string baseAsset,
        string quoteAsset,
        string reservedAsset,
        decimal outstandingReserved,
        decimal quantity,
        OrderSide side)
    {
        public Guid OrderId { get; } = orderId;
        public Guid AccountId { get; } = accountId;
        public Guid TradingAccountId { get; } = tradingAccountId;
        public Guid InstrumentId { get; } = instrumentId;
        public string Symbol { get; } = symbol;
        public string BaseAsset { get; } = baseAsset;
        public string QuoteAsset { get; } = quoteAsset;
        public string ReservedAsset { get; } = reservedAsset;
        public decimal OutstandingReserved { get; set; } = outstandingReserved;
        public decimal Quantity { get; } = quantity;
        public decimal FilledQuantity { get; set; }
        public OrderSide Side { get; } = side;
    }

    private sealed class PositionState(Guid positionId, Guid tradingAccountId, Guid instrumentId, string symbol, DateOnly positionDate, DateTimeOffset updatedAt)
    {
        public Guid PositionId { get; } = positionId;
        public Guid TradingAccountId { get; } = tradingAccountId;
        public Guid InstrumentId { get; } = instrumentId;
        public string Symbol { get; } = symbol;
        public DateOnly PositionDate { get; } = positionDate;
        public decimal NetQuantity { get; set; }
        public decimal? AverageOpenPrice { get; set; }
        public decimal LongQuantity { get; set; }
        public decimal ShortQuantity { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = updatedAt;
    }

    private IReadOnlyCollection<BalanceAdjusted> ApplyPendingEvents(Guid orderId)
    {
        if (!_pendingEvents.TryRemove(orderId, out var pendingEvents) || !_orders.TryGetValue(orderId, out var order))
        {
            return [];
        }

        var adjustments = new List<BalanceAdjusted>();
        foreach (var pending in pendingEvents.OrderBy(item => item.Sequence))
        {
            switch (pending)
            {
                case PendingRejectEvent rejected:
                    var rejectAdjustment = Apply(rejected.Event);
                    if (rejectAdjustment is not null)
                    {
                        adjustments.Add(rejectAdjustment);
                    }
                    break;
                case PendingTradeEvent trade:
                    adjustments.AddRange(ApplyTradeToOrder(order, trade.Event, trade.IsBuyer));
                    break;
            }
        }

        return adjustments;
    }

    private void EnqueuePending(Guid orderId, PendingLedgerEvent pendingEvent)
    {
        var list = _pendingEvents.GetOrAdd(orderId, _ => []);
        lock (list)
        {
            list.Add(pendingEvent);
        }
    }

    private abstract record PendingLedgerEvent(long Sequence)
    {
        private static long _sequence;

        public static PendingLedgerEvent ForReject(OrderRejected orderRejected) =>
            new PendingRejectEvent(Interlocked.Increment(ref _sequence), orderRejected);

        public static PendingLedgerEvent ForTrade(TradeExecuted tradeExecuted, bool isBuyer) =>
            new PendingTradeEvent(Interlocked.Increment(ref _sequence), tradeExecuted, isBuyer);
    }

    private sealed record PendingRejectEvent(long Sequence, OrderRejected Event) : PendingLedgerEvent(Sequence);

    private sealed record PendingTradeEvent(long Sequence, TradeExecuted Event, bool IsBuyer) : PendingLedgerEvent(Sequence);
}
