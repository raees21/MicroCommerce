using MicroCommerce.Contracts.Payments;
using MicroCommerce.Contracts.Shipping;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.Ordering.Api.Services;
using MicroCommerce.SharedKernel.Configuration;

namespace MicroCommerce.Ordering.Api.Services;

public sealed class PaymentSucceededConsumer(
    KafkaConsumer consumer,
    KafkaOptions options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<PaymentSucceededIntegrationEvent>(
            options.Topics.PaymentSucceeded,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "PaymentAuthorized", message.EventType, message, cancellationToken);
            },
            stoppingToken);
}

public sealed class PaymentFailedConsumer(
    KafkaConsumer consumer,
    KafkaOptions options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<PaymentFailedIntegrationEvent>(
            options.Topics.PaymentFailed,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "PaymentRejected", message.EventType, message, cancellationToken);
            },
            stoppingToken);
}

public sealed class ShipmentCreatedConsumer(
    KafkaConsumer consumer,
    KafkaOptions options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<ShipmentCreatedIntegrationEvent>(
            options.Topics.ShipmentCreated,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "Completed", message.EventType, message, cancellationToken);
            },
            stoppingToken);
}

public sealed class ShipmentFailedConsumer(
    KafkaConsumer consumer,
    KafkaOptions options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<ShipmentFailedIntegrationEvent>(
            options.Topics.ShipmentFailed,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var updater = scope.ServiceProvider.GetRequiredService<OrderStateUpdater>();
                await updater.UpdateStatusAsync(message.OrderId, "ShippingFailed", message.EventType, message, cancellationToken);
            },
            stoppingToken);
}
