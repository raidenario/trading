using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Trading.Application.Services;

public interface IAccountService
{
    Task<CreateAccountResult> CreateAsync(CreateAccountCommand command, CancellationToken ct);
    Task<AccountSummary?> GetByIdAsync(Guid accountId, CancellationToken ct);
    Task<IReadOnlyCollection<AccountSummary>> ListAsync(CancellationToken ct);
    Task<FundAccountResult> FundAsync(FundAccountCommand command, CancellationToken ct);
    Task<IReadOnlyCollection<AccountBalanceView>> GetBalancesAsync(Guid accountId, CancellationToken ct);
}

public sealed record CreateAccountResult(
    bool Success,
    Guid AccountId,
    string? DisplayName,
    string? Email,
    DateTimeOffset? CreatedAt,
    string? Reason);

public sealed record FundAccountResult(
    bool Success,
    decimal NewAvailableBalance,
    DateTimeOffset? FundedAt,
    string? Reason);
