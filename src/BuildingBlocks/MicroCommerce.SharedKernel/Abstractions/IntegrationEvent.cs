namespace MicroCommerce.SharedKernel.Abstractions;

public abstract record IntegrationEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc)
{
    protected IntegrationEvent(string eventType)
        : this(Guid.NewGuid(), eventType, DateTimeOffset.UtcNow)
    {
    }
}
