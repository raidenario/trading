using System.Text.Json.Serialization;
using Exchange.Ledger.Domain.Entities;

namespace Exchange.Ledger.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddSingleton<LedgerProjectionStore>();
        builder.Services.AddSingleton<LedgerEventPublisher>();
        builder.Services.AddHostedService<KafkaLedgerConsumer>();
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

        app.MapGet("/api/ledger/accounts/{accountId:guid}", (Guid accountId, LedgerProjectionStore store) =>
        {
            var account = store.GetAccount(accountId);
            return account is null ? Results.NotFound() : Results.Ok(account);
        });

        app.MapGet("/api/ledger/accounts/{accountId:guid}/balances", (Guid accountId, LedgerProjectionStore store) =>
            Results.Ok(store.GetBalances(accountId)));

        app.Run();
    }
}
