using MicroCommerce.SharedKernel.Abstractions;

namespace MicroCommerce.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string topic, IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
