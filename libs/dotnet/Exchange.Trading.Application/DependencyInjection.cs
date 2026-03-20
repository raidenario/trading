using Exchange.Trading.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.Trading.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingApplication(this IServiceCollection services)
    {
        services.AddSingleton<IOrderCommandService, OrderCommandService>();
        services.AddSingleton<IAccountService, InMemoryAccountService>();
        return services;
    }
}
