using MicroCommerce.SharedKernel.Abstractions;

namespace MicroCommerce.Contracts.Payments;

public sealed record PaymentSucceededIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string AuthorizationCode) : IntegrationEvent("payments.payment-succeeded");

public sealed record PaymentFailedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Reason) : IntegrationEvent("payments.payment-failed");
