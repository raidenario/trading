namespace Exchange.Trading.Domain.ValueObjects;

public readonly record struct Quantity
{
    public Quantity(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity must be greater than zero.");
        }

        Value = decimal.Round(value, 8, MidpointRounding.ToZero);
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString("0.########");
}
