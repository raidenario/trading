using System.Text.Json.Serialization;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.ReadModels;
using Exchange.Trading.Application;
using Exchange.Trading.Application.Services;
using Exchange.Trading.Infrastructure;

namespace Exchange.Gateway.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddTradingApplication();
        builder.Services.AddTradingInfrastructure();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();

        // =====================================================
        // Health
        // =====================================================
        app.MapGet("/health", () => Results.Ok(new
        {
            service = "gateway-api",
            status = "ok",
            utcNow = DateTimeOffset.UtcNow
        }));

        // =====================================================
        // Accounts
        // =====================================================
        app.MapPost("/api/accounts", async (CreateAccountCommand command, IAccountService accountService, CancellationToken ct) =>
        {
            var result = await accountService.CreateAsync(command, ct);
            return result.Success
                ? Results.Created($"/api/accounts/{result.AccountId}", new
                {
                    result.AccountId,
                    result.DisplayName,
                    result.Email,
                    result.CreatedAt
                })
                : Results.BadRequest(new { reason = result.Reason });
        });

        app.MapGet("/api/accounts/{accountId:guid}", async (Guid accountId, IAccountService accountService, CancellationToken ct) =>
        {
            var account = await accountService.GetByIdAsync(accountId, ct);
            return account is null ? Results.NotFound() : Results.Ok(account);
        });

        app.MapGet("/api/accounts", async (IAccountService accountService, CancellationToken ct) =>
        {
            var accounts = await accountService.ListAsync(ct);
            return Results.Ok(accounts);
        });

        // =====================================================
        // Funding
        // =====================================================
        app.MapPost("/api/accounts/{accountId:guid}/fund", async (Guid accountId, FundRequest request, IAccountService accountService, CancellationToken ct) =>
        {
            var command = new FundAccountCommand(accountId, request.Asset, request.Amount, request.ReferenceId, DateTimeOffset.UtcNow);
            var result = await accountService.FundAsync(command, ct);
            return result.Success
                ? Results.Ok(new
                {
                    accountId,
                    request.Asset,
                    request.Amount,
                    newAvailable = result.NewAvailableBalance,
                    fundedAt = result.FundedAt
                })
                : Results.BadRequest(new { reason = result.Reason });
        });

        app.MapGet("/api/accounts/{accountId:guid}/balances", async (Guid accountId, IAccountService accountService, CancellationToken ct) =>
        {
            var balances = await accountService.GetBalancesAsync(accountId, ct);
            return Results.Ok(balances);
        });

        // =====================================================
        // Orders
        // =====================================================
        app.MapPost("/api/orders", async (CreateOrderCommand command, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(command, cancellationToken);
            return result.RejectionReason is null
                ? Results.Accepted($"/api/orders/{result.OrderId}", new
                {
                    result.OrderId,
                    status = result.Status.ToString(),
                    trades = result.Trades.Select(trade => new
                    {
                        trade.TradeId,
                        price = trade.Price.Value,
                        quantity = trade.Quantity.Value,
                        trade.ExecutedAt
                    }),
                    book = result.Book is null ? null : new
                    {
                        symbol = result.Book.Symbol.Value,
                        bids = result.Book.Bids.Select(level => new { price = level.Price.Value, quantity = level.TotalQuantity.Value, level.OrderCount }),
                        asks = result.Book.Asks.Select(level => new { price = level.Price.Value, quantity = level.TotalQuantity.Value, level.OrderCount }),
                        result.Book.AsOf
                    }
                })
                : Results.BadRequest(new
                {
                    result.OrderId,
                    status = result.Status.ToString(),
                    reason = result.RejectionReason
                });
        });

        app.MapPost("/api/orders/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest request, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(
                new CancelOrderCommand(orderId, request.AccountId, request.Symbol, request.RequestedAt),
                cancellationToken);

            return result.Cancelled
                ? Results.Ok(new { orderId, status = "cancelled" })
                : Results.BadRequest(new { orderId, status = "rejected", reason = result.Reason });
        });

        app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var order = await service.GetByIdAsync(orderId, cancellationToken);
            return order is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    order.OrderId,
                    order.AccountId,
                    symbol = order.Symbol.Value,
                    side = order.Side.ToString(),
                    type = order.Type.ToString(),
                    status = order.Status.ToString(),
                    quantity = order.OriginalQuantity.Value,
                    order.FilledQuantity,
                    price = order.LimitPrice?.Value,
                    order.CreatedAt,
                    order.UpdatedAt
                });
        });

        app.MapGet("/api/orders", async (Guid? accountId, IOrderCommandService service, CancellationToken cancellationToken) =>
        {
            var orders = await service.ListAsync(accountId, cancellationToken);
            return Results.Ok(orders.Select(order => new
            {
                order.OrderId,
                order.AccountId,
                symbol = order.Symbol.Value,
                side = order.Side.ToString(),
                type = order.Type.ToString(),
                status = order.Status.ToString(),
                quantity = order.OriginalQuantity.Value,
                order.FilledQuantity,
                price = order.LimitPrice?.Value,
                order.CreatedAt,
                order.UpdatedAt
            }));
        });

        app.Run();
    }

    public sealed record CancelOrderRequest(Guid AccountId, string Symbol, DateTimeOffset RequestedAt);
    public sealed record FundRequest(string Asset, decimal Amount, string? ReferenceId);
}
