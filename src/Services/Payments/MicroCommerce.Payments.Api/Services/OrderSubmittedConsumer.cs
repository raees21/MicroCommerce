using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MicroCommerce.Contracts.Ordering;
using MicroCommerce.Contracts.Payments;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.Payments.Api.Data;
using MicroCommerce.SharedKernel.Configuration;

namespace MicroCommerce.Payments.Api.Services;

public sealed class OrderSubmittedConsumer(
    KafkaConsumer consumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<OrderSubmittedIntegrationEvent>(
            kafkaOptions.Value.Topics.OrderSubmitted,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var existing = await dbContext.PaymentRecords
                    .SingleOrDefaultAsync(x => x.OrderId == message.OrderId, cancellationToken);

                if (existing is not null)
                {
                    return;
                }

                var requiresManualReview = string.Equals(message.PaymentToken, "manual-review", StringComparison.OrdinalIgnoreCase);
                var shouldApprove = message.TotalAmount <= 10000m &&
                                    !string.Equals(message.PaymentToken, "fail", StringComparison.OrdinalIgnoreCase);

                var record = new PaymentRecordEntity
                {
                    OrderId = message.OrderId,
                    UserId = message.UserId,
                    Amount = message.TotalAmount,
                    Status = requiresManualReview ? "Pending" : shouldApprove ? "Authorized" : "Rejected",
                    Details = requiresManualReview
                        ? "Awaiting manual review."
                        : shouldApprove
                            ? "Simulated approval."
                            : "Simulated rejection."
                };

                dbContext.PaymentRecords.Add(record);
                await dbContext.SaveChangesAsync(cancellationToken);

                if (requiresManualReview)
                {
                    return;
                }

                if (shouldApprove)
                {
                    await publisher.PublishAsync(
                        kafkaOptions.Value.Topics.PaymentSucceeded,
                        new PaymentSucceededIntegrationEvent(
                            message.OrderId,
                            message.UserId,
                            message.TotalAmount,
                            $"AUTH-{message.OrderId:N}"[..12]),
                        cancellationToken);
                }
                else
                {
                    await publisher.PublishAsync(
                        kafkaOptions.Value.Topics.PaymentFailed,
                        new PaymentFailedIntegrationEvent(
                            message.OrderId,
                            message.UserId,
                            message.TotalAmount,
                            "Simulated payment failure."),
                        cancellationToken);
                }
            },
            stoppingToken);
}
