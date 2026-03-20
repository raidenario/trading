namespace Exchange.Ledger.Domain.Entities;

public sealed class LedgerAccount
{
    public LedgerAccount(Guid accountId, IReadOnlyCollection<LedgerBalance> balances, IReadOnlyCollection<LedgerEntry> entries)
    {
        AccountId = accountId;
        Balances = balances;
        Entries = entries;
    }

    public Guid AccountId { get; }

    public IReadOnlyCollection<LedgerBalance> Balances { get; }

    public IReadOnlyCollection<LedgerEntry> Entries { get; }
}
