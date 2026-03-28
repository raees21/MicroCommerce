using MicroCommerce.SharedKernel.Abstractions;

namespace MicroCommerce.Contracts.Shipping;

public sealed record ShipmentCreatedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    string TrackingNumber,
    string Status) : IntegrationEvent("shipping.shipment-created");

public sealed record ShipmentFailedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    string Reason) : IntegrationEvent("shipping.shipment-failed");
