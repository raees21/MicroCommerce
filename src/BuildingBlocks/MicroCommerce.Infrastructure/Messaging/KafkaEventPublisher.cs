using System.Text.Json;
using Confluent.Kafka;
using MicroCommerce.SharedKernel.Abstractions;
using MicroCommerce.SharedKernel.Configuration;

namespace MicroCommerce.Infrastructure.Messaging;

public sealed class KafkaEventPublisher(
    IProducer<string, string> producer,
    KafkaOptions options) : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(string topic, IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = integrationEvent.EventId.ToString("N"),
            Value = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions),
            Headers = new Headers
            {
                { "event-type", System.Text.Encoding.UTF8.GetBytes(integrationEvent.EventType) }
            }
        };

        await producer.ProduceAsync(topic, message, cancellationToken);
    }
}
