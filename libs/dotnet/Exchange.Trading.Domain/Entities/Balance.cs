namespace Exchange.Trading.Domain.Entities;

public sealed class Balance
{
    public Balance(string asset, decimal available, decimal locked)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            throw new ArgumentException("Asset is required.", nameof(asset));
        }

        if (available < 0 || locked < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(available), "Balance amounts cannot be negative.");
        }

        Asset = asset.Trim().ToUpperInvariant();
        Available = decimal.Round(available, 8, MidpointRounding.ToZero);
        Locked = decimal.Round(locked, 8, MidpointRounding.ToZero);
    }

    public string Asset { get; }

    public decimal Available { get; private set; }

    public decimal Locked { get; private set; }
}
