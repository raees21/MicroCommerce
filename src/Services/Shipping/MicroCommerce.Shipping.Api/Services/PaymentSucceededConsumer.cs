using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MicroCommerce.Contracts.Payments;
using MicroCommerce.Contracts.Shipping;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;
using MicroCommerce.Shipping.Api.Data;

namespace MicroCommerce.Shipping.Api.Services;

public sealed class PaymentSucceededConsumer(
    KafkaConsumer consumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<PaymentSucceededIntegrationEvent>(
            kafkaOptions.Value.Topics.PaymentSucceeded,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var existing = await dbContext.Shipments
                    .SingleOrDefaultAsync(x => x.OrderId == message.OrderId, cancellationToken);

                if (existing is not null)
                {
                    return;
                }

                var shipment = new ShipmentRecordEntity
                {
                    OrderId = message.OrderId,
                    UserId = message.UserId,
                    TrackingNumber = $"TRK-{message.OrderId:N}"[..16],
                    Status = "Created"
                };

                dbContext.Shipments.Add(shipment);
                await dbContext.SaveChangesAsync(cancellationToken);

                await publisher.PublishAsync(
                    kafkaOptions.Value.Topics.ShipmentCreated,
                    new ShipmentCreatedIntegrationEvent(
                        message.OrderId,
                        message.UserId,
                        shipment.TrackingNumber,
                        shipment.Status),
                    cancellationToken);
            },
            stoppingToken);
}
