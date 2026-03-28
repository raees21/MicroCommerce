using System.Text.Json;
using Confluent.Kafka;

namespace MicroCommerce.Infrastructure.Messaging;

public sealed class KafkaConsumer(
    ConsumerConfig config)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ConsumeAsync<T>(
        string topic,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = consumer.Consume(cancellationToken);

            if (string.IsNullOrWhiteSpace(result.Message.Value))
            {
                consumer.Commit(result);
                continue;
            }

            var payload = JsonSerializer.Deserialize<T>(result.Message.Value, JsonOptions);
            if (payload is null)
            {
                consumer.Commit(result);
                continue;
            }

            await handler(payload, cancellationToken);
            consumer.Commit(result);
        }
    }
}
