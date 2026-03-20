namespace Exchange.Trading.Domain.Entities;

public sealed class Account
{
    public Account(Guid accountId, IReadOnlyCollection<Balance>? balances = null)
    {
        AccountId = accountId;
        Balances = balances ?? Array.Empty<Balance>();
    }

    public Guid AccountId { get; }

    public IReadOnlyCollection<Balance> Balances { get; }
}
