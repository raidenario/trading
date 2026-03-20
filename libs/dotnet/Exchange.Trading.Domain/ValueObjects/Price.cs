namespace Exchange.Trading.Domain.ValueObjects;

public readonly record struct Price
{
    public Price(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Price must be greater than zero.");
        }

        Value = decimal.Round(value, 8, MidpointRounding.ToZero);
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString("0.########");
}
