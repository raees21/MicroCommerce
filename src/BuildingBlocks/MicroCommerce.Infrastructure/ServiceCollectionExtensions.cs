using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services,
        ConfigurationManager configuration)
    {
        var options = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
        services.AddSingleton(options);

        services.AddSingleton<IProducer<string, string>>(_ =>
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = options.ClientId
            };

            return new ProducerBuilder<string, string>(producerConfig).Build();
        });

        services.AddSingleton(_ =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.ConsumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            return consumerConfig;
        });

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<KafkaConsumer>();

        return services;
    }
}
