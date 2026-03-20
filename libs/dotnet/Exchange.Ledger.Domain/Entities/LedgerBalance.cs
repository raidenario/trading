namespace Exchange.Ledger.Domain.Entities;

public sealed class LedgerBalance
{
    public LedgerBalance(string asset, decimal available, decimal locked)
    {
        Asset = asset.Trim().ToUpperInvariant();
        Available = decimal.Round(available, 8, MidpointRounding.ToZero);
        Locked = decimal.Round(locked, 8, MidpointRounding.ToZero);
    }

    public string Asset { get; }

    public decimal Available { get; }

    public decimal Locked { get; }
}
