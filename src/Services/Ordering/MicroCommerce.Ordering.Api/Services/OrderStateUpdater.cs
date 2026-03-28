using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroCommerce.Contracts.Ordering;
using MicroCommerce.Ordering.Api.Data;
using MicroCommerce.Ordering.Api.Models;

namespace MicroCommerce.Ordering.Api.Services;

public sealed class OrderStateUpdater(
    OrderingDbContext dbContext,
    OrderProjectionStore projectionStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CreatePendingOrderAsync(
        Guid orderId,
        OrderSubmittedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        dbContext.OrderEvents.Add(new OrderEventEntity
        {
            OrderId = orderId,
            Version = 1,
            EventType = integrationEvent.EventType,
            Payload = JsonSerializer.Serialize(integrationEvent, JsonOptions)
        });

        dbContext.OrderSagas.Add(new OrderSagaStateEntity
        {
            OrderId = orderId,
            UserId = integrationEvent.UserId,
            IdempotencyHash = integrationEvent.IdempotencyHash,
            TotalAmount = integrationEvent.TotalAmount
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await projectionStore.UpsertAsync(new OrderReadModel
        {
            Id = orderId,
            UserId = integrationEvent.UserId,
            IdempotencyHash = integrationEvent.IdempotencyHash,
            Status = "PendingPayment",
            ShippingAddress = integrationEvent.ShippingAddress,
            TotalAmount = integrationEvent.TotalAmount,
            CreatedAtUtc = integrationEvent.OccurredAtUtc,
            UpdatedAtUtc = integrationEvent.OccurredAtUtc,
            Lines = integrationEvent.Lines.Select(line => new OrderReadModelLine
            {
                ProductId = line.ProductId,
                Sku = line.Sku,
                Name = line.Name,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            }).ToList()
        }, cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid orderId, string newStatus, string eventType, object payload, CancellationToken cancellationToken)
    {
        var saga = await dbContext.OrderSagas.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (saga is null)
        {
            return;
        }

        var nextVersion = await dbContext.OrderEvents
            .Where(x => x.OrderId == orderId)
            .CountAsync(cancellationToken) + 1;

        saga.CurrentState = newStatus;
        saga.UpdatedAtUtc = DateTimeOffset.UtcNow;

        dbContext.OrderEvents.Add(new OrderEventEntity
        {
            OrderId = orderId,
            Version = nextVersion,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var projection = await projectionStore.GetAsync(orderId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await projectionStore.UpsertAsync(projection with
        {
            Status = newStatus,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
