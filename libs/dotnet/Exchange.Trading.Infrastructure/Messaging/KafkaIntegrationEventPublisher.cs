using System.Text.Json;
using Confluent.Kafka;
using Exchange.Trading.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Exchange.Trading.Infrastructure.Messaging;

public sealed class KafkaIntegrationEventPublisher : IIntegrationEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaIntegrationEventPublisher> _logger;

    public KafkaIntegrationEventPublisher(IConfiguration configuration, ILogger<KafkaIntegrationEventPublisher> logger)
    {
        _logger = logger;
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:29092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken)
        where TMessage : class
    {
        var payload = JsonSerializer.Serialize(message);
        
        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(), // Idealmente usar uma chave de partição baseada em domínio se necessário
                Value = payload
            }, cancellationToken);

            _logger.LogDebug("Message delivered to {Topic} at offset {Offset}", result.TopicPartitionOffset.Topic, result.TopicPartitionOffset.Offset);
        }
        catch (ProduceException<string, string> e)
        {
            _logger.LogError(e, "Error publishing message to Kafka topic {Topic}", topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
