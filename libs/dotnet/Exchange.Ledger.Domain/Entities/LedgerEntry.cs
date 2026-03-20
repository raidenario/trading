using Exchange.Ledger.Domain.Enums;

namespace Exchange.Ledger.Domain.Entities;

public sealed class LedgerEntry
{
    public LedgerEntry(Guid entryId, Guid accountId, string asset, decimal amount, LedgerEntryType type, string reference, DateTimeOffset recordedAt)
    {
        EntryId = entryId;
        AccountId = accountId;
        Asset = asset.Trim().ToUpperInvariant();
        Amount = decimal.Round(amount, 8, MidpointRounding.ToZero);
        Type = type;
        Reference = reference;
        RecordedAt = recordedAt;
    }

    public Guid EntryId { get; }

    public Guid AccountId { get; }

    public string Asset { get; }

    public decimal Amount { get; }

    public LedgerEntryType Type { get; }

    public string Reference { get; }

    public DateTimeOffset RecordedAt { get; }
}
