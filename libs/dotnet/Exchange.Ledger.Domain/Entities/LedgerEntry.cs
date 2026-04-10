using Exchange.Ledger.Domain.Enums;
using Exchange.Platform.Contracts;

namespace Exchange.Ledger.Domain.Entities;

public sealed class LedgerEntry
{
    public LedgerEntry(
        Guid entryId,
        Guid accountId,
        string assetCode,
        decimal amount,
        LedgerEntryType type,
        BalanceBucket balanceBucket,
        EntryDirection direction,
        ReferenceType referenceType,
        string referenceId,
        DateTimeOffset recordedAt,
        Guid? tradingAccountId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        EntryId = entryId;
        AccountId = accountId;
        TradingAccountId = tradingAccountId;
        AssetCode = assetCode.Trim().ToUpperInvariant();
        Amount = decimal.Round(amount, 8, MidpointRounding.ToZero);
        Type = type;
        BalanceBucket = balanceBucket;
        Direction = direction;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        RecordedAt = recordedAt;
        Metadata = metadata;
    }

    public Guid EntryId { get; }

    public Guid AccountId { get; }

    public Guid? TradingAccountId { get; }

    public string AssetCode { get; }

    public decimal Amount { get; }

    public LedgerEntryType Type { get; }

    public BalanceBucket BalanceBucket { get; }

    public EntryDirection Direction { get; }

    public ReferenceType ReferenceType { get; }

    public string ReferenceId { get; }

    public IReadOnlyDictionary<string, string>? Metadata { get; }

    public DateTimeOffset RecordedAt { get; }
}
