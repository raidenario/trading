using ContractOrderSource = Exchange.Platform.Contracts.OrderSource;
using Exchange.Trading.Domain.Enums;
using Exchange.Trading.Domain.ValueObjects;

namespace Exchange.Trading.Domain.Entities;

public sealed class Order
{
    private Order(
        Guid orderId,
        Guid accountId,
        Symbol symbol,
        OrderSide side,
        OrderType type,
        Quantity originalQuantity,
        Price? limitPrice,
        TimeInForce timeInForce,
        string? clientOrderId,
        Guid? instrumentId,
        Guid? tradingAccountId,
        ContractOrderSource sourceSystem,
        IReadOnlyDictionary<string, string>? executionInstructions,
        DateTimeOffset createdAt)
    {
        OrderId = orderId;
        AccountId = accountId;
        Symbol = symbol;
        Side = side;
        Type = type;
        OriginalQuantity = originalQuantity;
        RemainingQuantity = originalQuantity.Value;
        LimitPrice = limitPrice;
        TimeInForce = timeInForce;
        ClientOrderId = clientOrderId;
        InstrumentId = instrumentId;
        TradingAccountId = tradingAccountId;
        SourceSystem = sourceSystem;
        ExecutionInstructions = executionInstructions;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OrderId { get; }

    public Guid AccountId { get; }

    public Symbol Symbol { get; }

    public OrderSide Side { get; }

    public OrderType Type { get; }

    public Quantity OriginalQuantity { get; }

    public decimal RemainingQuantity { get; private set; }

    public Price? LimitPrice { get; }

    public TimeInForce TimeInForce { get; }

    public string? ClientOrderId { get; }

    public Guid? InstrumentId { get; }

    public Guid? TradingAccountId { get; }

    public ContractOrderSource SourceSystem { get; }

    public IReadOnlyDictionary<string, string>? ExecutionInstructions { get; }

    public OrderStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public decimal FilledQuantity => decimal.Round(OriginalQuantity.Value - RemainingQuantity, 8, MidpointRounding.ToZero);

    public decimal OpenQuantity => RemainingQuantity;

    public static Order Create(
        Guid orderId,
        Guid accountId,
        Symbol symbol,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Price? limitPrice,
        TimeInForce timeInForce,
        string? clientOrderId,
        Guid? instrumentId,
        Guid? tradingAccountId,
        ContractOrderSource sourceSystem,
        IReadOnlyDictionary<string, string>? executionInstructions,
        DateTimeOffset createdAt)
    {
        if (type == OrderType.Limit && limitPrice is null)
        {
            throw new ArgumentException("Limit orders require a price.", nameof(limitPrice));
        }

        return new Order(orderId, accountId, symbol, side, type, quantity, limitPrice, timeInForce, clientOrderId, instrumentId, tradingAccountId, sourceSystem, executionInstructions, createdAt);
    }

    public void Accept(DateTimeOffset acceptedAt)
    {
        if (Status == OrderStatus.Rejected)
        {
            throw new InvalidOperationException("Rejected orders cannot be accepted.");
        }

        Status = OrderStatus.Accepted;
        AcceptedAt = acceptedAt;
        UpdatedAt = acceptedAt;
    }

    public void Reject(string reason, DateTimeOffset rejectedAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(reason));
        }

        Status = OrderStatus.Rejected;
        RejectionReason = reason.Trim();
        UpdatedAt = rejectedAt;
    }

    public void ApplyTrade(decimal executedQuantity, DateTimeOffset executedAt)
    {
        if (executedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(executedQuantity), "Executed quantity must be greater than zero.");
        }

        if (executedQuantity > RemainingQuantity)
        {
            throw new InvalidOperationException("Executed quantity exceeds remaining quantity.");
        }

        RemainingQuantity = decimal.Round(RemainingQuantity - executedQuantity, 8, MidpointRounding.ToZero);
        Status = RemainingQuantity == 0 ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        UpdatedAt = executedAt;
    }

    public void MarkFilled(DateTimeOffset filledAt)
    {
        RemainingQuantity = 0;
        Status = OrderStatus.Filled;
        UpdatedAt = filledAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected)
        {
            return;
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = cancelledAt;
        UpdatedAt = cancelledAt;
    }
}
