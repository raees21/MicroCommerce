using Microsoft.Extensions.Options;
using MicroCommerce.Contracts.Ordering;
using MicroCommerce.Contracts.Payments;
using MicroCommerce.Contracts.Shipping;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.Ordering.Api.Services;
using MicroCommerce.SharedKernel.Configuration;

namespace MicroCommerce.Ordering.Api.Services;

public sealed class OrderSubmittedOrchestratorConsumer(
    KafkaConsumer consumer,
    IEventPublisher eventPublisher,
    IOptions<KafkaOptions> options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<OrderSubmittedIntegrationEvent>(
            options.Value.Topics.OrderSubmitted,
            async (message, cancellationToken) =>
            {
                await eventPublisher.PublishAsync(
                    options.Value.Topics.ProcessPayment,
                    new ProcessPaymentCommand(
                        message.OrderId,
                        message.UserId,
                        message.PaymentToken,
                        message.TotalAmount),
                    cancellationToken);
            },
            stoppingToken);
}

public sealed class PaymentSucceededOrchestratorConsumer(
    KafkaConsumer consumer,
    IEventPublisher eventPublisher,
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<PaymentSucceededIntegrationEvent>(
            options.Value.Topics.PaymentSucceeded,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                var applied = await updater.UpdateStatusAsync(message.OrderId, "PaymentAuthorized", message, cancellationToken);

                if (!applied)
                {
                    return;
                }

                await eventPublisher.PublishAsync(
                    options.Value.Topics.CreateShipment,
                    new CreateShipmentCommand(message.OrderId, message.UserId),
                    cancellationToken);
            },
            stoppingToken);
}

public sealed class PaymentFailedOrchestratorConsumer(
    KafkaConsumer consumer,
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<PaymentFailedIntegrationEvent>(
            options.Value.Topics.PaymentFailed,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "PaymentRejected", message, cancellationToken);
            },
            stoppingToken);
}

public sealed class ShipmentCreatedOrchestratorConsumer(
    KafkaConsumer consumer,
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<ShipmentCreatedIntegrationEvent>(
            options.Value.Topics.ShipmentCreated,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "Completed", message, cancellationToken);
            },
            stoppingToken);
}

public sealed class ShipmentFailedOrchestratorConsumer(
    KafkaConsumer consumer,
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<ShipmentFailedIntegrationEvent>(
            options.Value.Topics.ShipmentFailed,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "ShippingFailed", message, cancellationToken);
            },
            stoppingToken);
}
