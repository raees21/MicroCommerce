using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroCommerce.SharedKernel.Configuration;

namespace MicroCommerce.Infrastructure.Messaging;

public sealed class KafkaTopicProvisioningService(
    KafkaOptions options,
    ILogger<KafkaTopicProvisioningService> logger) : IHostedService
{
    private static readonly TimeSpan BrokerReadyTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan TopicVisibilityTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AdminOperationTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var topicNames = options.Topics.GetAll()
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (topicNames.Count == 0)
        {
            logger.LogInformation("Kafka topic provisioning skipped because no topic names were configured.");
            return;
        }

        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = $"{options.ClientId}-topic-provisioner"
        }).Build();

        await WaitForBrokerAsync(adminClient, cancellationToken);

        var topicsToCreate = GetMissingTopics(adminClient, topicNames);
        if (topicsToCreate.Count == 0)
        {
            logger.LogInformation("Kafka topics already existed: {Topics}", string.Join(", ", topicNames));
            return;
        }

        await CreateMissingTopicsAsync(adminClient, topicsToCreate, cancellationToken);

        var remainingTopics = await WaitForTopicsAsync(adminClient, topicNames, cancellationToken);
        if (remainingTopics.Count == 0)
        {
            logger.LogInformation("Kafka topics are ready: {Topics}", string.Join(", ", topicNames));
            return;
        }

        throw new InvalidOperationException($"Kafka topics were still missing after provisioning: {string.Join(", ", remainingTopics)}");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WaitForBrokerAsync(IAdminClient adminClient, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + BrokerReadyTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var metadata = adminClient.GetMetadata(AdminOperationTimeout);
                if (metadata.Brokers.Count > 0)
                {
                    return;
                }
            }
            catch (KafkaException ex)
            {
                logger.LogInformation(ex, "Kafka broker is not ready yet at {BootstrapServers}. Retrying.", options.BootstrapServers);
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }

        throw new TimeoutException($"Kafka broker at {options.BootstrapServers} was not ready within {BrokerReadyTimeout.TotalSeconds} seconds.");
    }

    private IReadOnlyList<string> GetMissingTopics(IAdminClient adminClient, IReadOnlyCollection<string> topicNames)
    {
        var metadata = adminClient.GetMetadata(AdminOperationTimeout);
        var existingTopics = metadata.Topics
            .Where(topic => topic.Error.Code == ErrorCode.NoError)
            .Select(topic => topic.Topic)
            .ToHashSet(StringComparer.Ordinal);

        return topicNames
            .Where(topic => !existingTopics.Contains(topic))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> WaitForTopicsAsync(
        IAdminClient adminClient,
        IReadOnlyCollection<string> topicNames,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + TopicVisibilityTimeout;
        IReadOnlyList<string> remainingTopics = topicNames.ToList();

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                remainingTopics = GetMissingTopics(adminClient, topicNames);
                if (remainingTopics.Count == 0)
                {
                    return remainingTopics;
                }
            }
            catch (KafkaException ex)
            {
                logger.LogInformation(
                    ex,
                    "Kafka topic metadata is not fully available yet at {BootstrapServers}. Retrying.",
                    options.BootstrapServers);
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }

        return remainingTopics;
    }

    private async Task CreateMissingTopicsAsync(
        IAdminClient adminClient,
        IReadOnlyCollection<string> topicNames,
        CancellationToken cancellationToken)
    {
        try
        {
            await adminClient.CreateTopicsAsync(
                    topicNames.Select(topic => new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }))
                .WaitAsync(cancellationToken);
        }
        catch (CreateTopicsException ex)
        {
            var blockingErrors = ex.Results
                .Where(result => result.Error.Code is not ErrorCode.NoError and not ErrorCode.TopicAlreadyExists)
                .Select(result => $"{result.Topic}: {result.Error.Reason}")
                .ToList();

            if (blockingErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Kafka topic provisioning failed for: {string.Join("; ", blockingErrors)}",
                    ex);
            }

            logger.LogInformation("Kafka topics were created concurrently by another service: {Topics}", string.Join(", ", topicNames));
        }
    }
}
