using Exchange.Ledger.Domain.Entities;
using Exchange.Ledger.Domain.Enums;
using Exchange.Platform.Contracts.ReadModels;

namespace Exchange.Ledger.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();

        app.MapGet("/health", () => Results.Ok(new
        {
            service = "ledger-service",
            status = "ok",
            utcNow = DateTimeOffset.UtcNow
        }));

        app.MapGet("/api/ledger/accounts/{accountId:guid}", (Guid accountId) =>
        {
            var account = new LedgerAccount(
                accountId,
                new[]
                {
                    new LedgerBalance("USD", 25000m, 2500m),
                    new LedgerBalance("BTC", 1.25m, 0.15m)
                },
                new[]
                {
                    new LedgerEntry(Guid.NewGuid(), accountId, "USD", -1250m, LedgerEntryType.Hold, "order:hold", DateTimeOffset.UtcNow.AddMinutes(-5)),
                    new LedgerEntry(Guid.NewGuid(), accountId, "BTC", 0.15m, LedgerEntryType.TradeSettlement, "trade:settlement", DateTimeOffset.UtcNow.AddMinutes(-1))
                });

            return Results.Ok(account);
        });

        app.MapGet("/api/ledger/accounts/{accountId:guid}/balances", (Guid accountId) =>
        {
            var balances = new[]
            {
                new BalanceSnapshot(accountId, "USD", 25000m, 2500m, DateTimeOffset.UtcNow),
                new BalanceSnapshot(accountId, "BTC", 1.25m, 0.15m, DateTimeOffset.UtcNow)
            };

            return Results.Ok(balances);
        });

        app.Run();
    }
}
