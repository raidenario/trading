using System.Text.Json;
using Exchange.Trading.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Exchange.Trading.Infrastructure.Messaging;

public sealed class InMemoryIntegrationEventPublisher(ILogger<InMemoryIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken)
        where TMessage : class
    {
        logger.LogInformation("Integration event published to {Topic}: {Payload}", topic, JsonSerializer.Serialize(message));
        return Task.CompletedTask;
    }
}
