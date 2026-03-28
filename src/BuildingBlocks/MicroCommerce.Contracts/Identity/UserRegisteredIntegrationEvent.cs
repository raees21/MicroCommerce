using MicroCommerce.SharedKernel.Abstractions;

namespace MicroCommerce.Contracts.Identity;

public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email,
    string FullName) : IntegrationEvent("identity.user-registered");
