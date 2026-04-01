using System.Text.Json;
using Confluent.Kafka;
using Exchange.Platform.Contracts.Commands;
using Exchange.Platform.Contracts.Messaging;
using Exchange.Trading.Application.Abstractions;
using Exchange.Trading.Application.Models;
using Exchange.Trading.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DomainOrderStatus = Exchange.Trading.Domain.Enums.OrderStatus;

namespace Exchange.Trading.Infrastructure.Matching;

public sealed class KafkaMatchingEngineClient : IMatchingEngineClient, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMatchingEngineClient> _logger;

    public KafkaMatchingEngineClient(IConfiguration configuration, ILogger<KafkaMatchingEngineClient> logger)
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

    public async Task<OrderSubmissionResult> SubmitAsync(Order order, CancellationToken cancellationToken)
    {
        // Converte domínio para contrato de comando
        var command = new CreateOrderCommand(
            order.OrderId,
            order.AccountId,
            order.Symbol.Value,
            (Exchange.Platform.Contracts.OrderSide)order.Side,
            (Exchange.Platform.Contracts.OrderType)order.Type,
            order.OriginalQuantity.Value,
            order.LimitPrice?.Value,
            (Exchange.Platform.Contracts.TimeInForce)order.TimeInForce,
            order.ClientOrderId,
            order.CreatedAt);

        var payload = JsonSerializer.Serialize(command);

        try
        {
            await _producer.ProduceAsync(Topic, new Message<string, string>
            {
                Key = order.Symbol.Value, // Particionamento por símbolo garante ordem cronológica por par
                Value = payload
            }, cancellationToken);

            _logger.LogInformation("Order command published to Kafka for OrderId: {OrderId}", order.OrderId);

            // Na arquitetura assíncrona, a API só confirma enfileiramento.
            return new OrderSubmissionResult(true, DomainOrderStatus.Pending, Array.Empty<Trade>(), null, null);
        }
        catch (ProduceException<string, string> e)
        {
            _logger.LogError(e, "Failed to publish order to Kafka: {OrderId}", order.OrderId);
            return new OrderSubmissionResult(false, DomainOrderStatus.Rejected, Array.Empty<Trade>(), null, e.Error.Reason);
        }
    }

    public async Task<OrderCancellationResult> CancelAsync(Guid orderId, string symbol, CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(orderId, Guid.Empty, symbol, DateTimeOffset.UtcNow); // AccountId simplificado aqui
        var payload = JsonSerializer.Serialize(command);

        try
        {
            await _producer.ProduceAsync(Topic, new Message<string, string>
            {
                Key = symbol,
                Value = payload
            }, cancellationToken);

            return new OrderCancellationResult(true, null);
        }
        catch (ProduceException<string, string> e)
        {
            _logger.LogError(e, "Failed to publish cancel command to Kafka: {OrderId}", orderId);
            return new OrderCancellationResult(false, e.Error.Reason);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }

    private const string Topic = KafkaTopics.OrderCommands;
}
