using MicroCommerce.SharedKernel.Abstractions;

namespace MicroCommerce.Contracts.Ordering;

public sealed record PlaceOrderRequest(
    Guid UserId,
    string ShippingAddress,
    string PaymentToken,
    IReadOnlyList<PlaceOrderLineRequest> Lines);

public sealed record PlaceOrderLineRequest(
    Guid ProductId,
    int Quantity);

public sealed record OrderLineSnapshot(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);

public sealed record OrderSubmittedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    string IdempotencyHash,
    string ShippingAddress,
    string PaymentToken,
    decimal TotalAmount,
    IReadOnlyList<OrderLineSnapshot> Lines) : IntegrationEvent("ordering.order-submitted");
