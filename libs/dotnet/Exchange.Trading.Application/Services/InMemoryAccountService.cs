using Exchange.Platform.Contracts;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Events;
using Exchange.Platform.Contracts.Messaging;
using Exchange.Platform.Contracts.ReadModels;
using Exchange.Trading.Application.Abstractions;

namespace Exchange.Trading.Application.Services;

public sealed class InMemoryAccountService : IAccountService
{
    private readonly Dictionary<Guid, AccountSummary> _accounts = new();
    private readonly Dictionary<string, AccountBalanceView> _balances = new();
    private readonly IIntegrationEventPublisher _integrationEventPublisher;

    public InMemoryAccountService(IIntegrationEventPublisher integrationEventPublisher)
    {
        _integrationEventPublisher = integrationEventPublisher;
        SeedDemoAccounts();
    }

    public async Task<CreateAccountResult> CreateAsync(CreateAccountCommand command, CancellationToken ct)
    {
        if (_accounts.ContainsKey(command.AccountId))
        {
            return new CreateAccountResult(false, command.AccountId, null, null, null, "Account already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var account = new AccountSummary(command.AccountId, command.DisplayName, command.Email, now);
        _accounts[command.AccountId] = account;

        await _integrationEventPublisher.PublishAsync(
            KafkaTopics.AccountEvents,
            new AccountCreated(command.AccountId, command.DisplayName, command.Email, now),
            ct);

        return new CreateAccountResult(true, command.AccountId, command.DisplayName, command.Email, now, null);
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

    public async Task<FundAccountResult> FundAsync(FundAccountCommand command, CancellationToken ct)
    {
        if (!_accounts.ContainsKey(command.AccountId))
        {
            return new FundAccountResult(false, 0, null, "Account not found.");
        }

        var key = BalanceKey(command.AccountId, command.Asset);
        var fundedAt = DateTimeOffset.UtcNow;
        if (_balances.TryGetValue(key, out var existing))
        {
            var newAvailable = existing.Available + command.Amount;
            _balances[key] = existing with { Available = newAvailable, Total = newAvailable + existing.Reserved, AsOf = fundedAt };
        }
        else
        {
            _balances[key] = new AccountBalanceView(command.AccountId, command.Asset.ToUpperInvariant(), command.Amount, 0, command.Amount, fundedAt);
        }

        await _integrationEventPublisher.PublishAsync(
            KafkaTopics.AccountEvents,
            new AccountFunded(command.AccountId, command.Asset.ToUpperInvariant(), command.Amount, _balances[key].Available, fundedAt),
            ct);

        return new FundAccountResult(true, _balances[key].Available, fundedAt, null);
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
        var seededAt = DateTimeOffset.UtcNow;

        foreach (var account in DemoSeed.Accounts)
        {
            _accounts[account.AccountId] = new AccountSummary(account.AccountId, account.DisplayName, account.Email, seededAt);
        }

        foreach (var balance in DemoSeed.Balances)
        {
            _balances[BalanceKey(balance.AccountId, balance.Asset)] = new AccountBalanceView(
                balance.AccountId,
                balance.Asset,
                balance.Available,
                balance.Reserved,
                balance.Available + balance.Reserved,
                seededAt);
        }
    }
}
