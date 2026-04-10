using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Services;
using Exchange.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.Trading.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingApplication(this IServiceCollection services)
    {
        services.AddSingleton<IInstrumentCatalog>(_ => new StaticInstrumentCatalog(
            DemoSeed.Instruments,
            DemoSeed.InstrumentTradingRules,
            DemoSeed.InstrumentMarketConfigs,
            DemoSeed.InstrumentStatuses));
        services.AddSingleton<ITradingAccountResolver>(_ => new DemoTradingAccountResolver(DemoSeed.TradingAccounts));
        services.AddSingleton<IOrderCommandService, OrderCommandService>();
        services.AddSingleton<IAccountService, InMemoryAccountService>();
        return services;
    }
}
