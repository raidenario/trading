using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Trading.Application.Services;

public sealed class InMemoryAccountService : IAccountService
{
    private readonly Dictionary<Guid, AccountSummary> _accounts = new();
    private readonly Dictionary<string, AccountBalanceView> _balances = new();

    public InMemoryAccountService()
    {
        SeedDemoAccounts();
    }

    public Task<CreateAccountResult> CreateAsync(CreateAccountCommand command, CancellationToken ct)
    {
        if (_accounts.ContainsKey(command.AccountId))
        {
            return Task.FromResult(new CreateAccountResult(false, command.AccountId, null, null, null, "Account already exists."));
        }

        var now = DateTimeOffset.UtcNow;
        var account = new AccountSummary(command.AccountId, command.DisplayName, command.Email, now);
        _accounts[command.AccountId] = account;

        return Task.FromResult(new CreateAccountResult(true, command.AccountId, command.DisplayName, command.Email, now, null));
    }

    public Task<AccountSummary?> GetByIdAsync(Guid accountId, CancellationToken ct)
    {
        _accounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }

    public Task<IReadOnlyCollection<AccountSummary>> ListAsync(CancellationToken ct)
    {
        IReadOnlyCollection<AccountSummary> list = _accounts.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<FundAccountResult> FundAsync(FundAccountCommand command, CancellationToken ct)
    {
        if (!_accounts.ContainsKey(command.AccountId))
        {
            return Task.FromResult(new FundAccountResult(false, 0, null, "Account not found."));
        }

        var key = BalanceKey(command.AccountId, command.Asset);
        if (_balances.TryGetValue(key, out var existing))
        {
            var newAvailable = existing.Available + command.Amount;
            _balances[key] = existing with { Available = newAvailable, Total = newAvailable + existing.Reserved, AsOf = DateTimeOffset.UtcNow };
        }
        else
        {
            _balances[key] = new AccountBalanceView(command.AccountId, command.Asset.ToUpperInvariant(), command.Amount, 0, command.Amount, DateTimeOffset.UtcNow);
        }

        return Task.FromResult(new FundAccountResult(true, _balances[key].Available, DateTimeOffset.UtcNow, null));
    }

    public Task<IReadOnlyCollection<AccountBalanceView>> GetBalancesAsync(Guid accountId, CancellationToken ct)
    {
        IReadOnlyCollection<AccountBalanceView> result = _balances.Values
            .Where(b => b.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    private static string BalanceKey(Guid accountId, string asset) =>
        $"{accountId}:{asset.ToUpperInvariant()}";

    private void SeedDemoAccounts()
    {
        var alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var bob   = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var charlie = Guid.Parse("33333333-3333-3333-3333-333333333333");

        _accounts[alice]   = new AccountSummary(alice,   "Alice Trader",  "alice@exchange.local",   DateTimeOffset.UtcNow);
        _accounts[bob]     = new AccountSummary(bob,     "Bob Market",    "bob@exchange.local",     DateTimeOffset.UtcNow);
        _accounts[charlie] = new AccountSummary(charlie, "Charlie Whale", "charlie@exchange.local", DateTimeOffset.UtcNow);

        _balances[BalanceKey(alice, "USD")] = new AccountBalanceView(alice, "USD", 100_000m, 0, 100_000m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(alice, "BTC")] = new AccountBalanceView(alice, "BTC", 5m, 0, 5m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(alice, "ETH")] = new AccountBalanceView(alice, "ETH", 50m, 0, 50m, DateTimeOffset.UtcNow);

        _balances[BalanceKey(bob, "USD")] = new AccountBalanceView(bob, "USD", 250_000m, 0, 250_000m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(bob, "BTC")] = new AccountBalanceView(bob, "BTC", 10m, 0, 10m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(bob, "SOL")] = new AccountBalanceView(bob, "SOL", 500m, 0, 500m, DateTimeOffset.UtcNow);

        _balances[BalanceKey(charlie, "USD")] = new AccountBalanceView(charlie, "USD", 1_000_000m, 0, 1_000_000m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(charlie, "BTC")] = new AccountBalanceView(charlie, "BTC", 50m, 0, 50m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(charlie, "ETH")] = new AccountBalanceView(charlie, "ETH", 200m, 0, 200m, DateTimeOffset.UtcNow);
        _balances[BalanceKey(charlie, "SOL")] = new AccountBalanceView(charlie, "SOL", 2000m, 0, 2000m, DateTimeOffset.UtcNow);
    }
}
