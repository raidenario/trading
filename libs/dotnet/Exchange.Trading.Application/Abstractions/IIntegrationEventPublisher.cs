namespace Exchange.Trading.Application.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken)
        where TMessage : class;
}
