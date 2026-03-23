using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Infrastructure.Matching;
using Exchange.Trading.Infrastructure.Messaging;
using Exchange.Trading.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.Trading.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton<IMatchingEngineClient, KafkaMatchingEngineClient>();
        services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();
        return services;
    }
}
