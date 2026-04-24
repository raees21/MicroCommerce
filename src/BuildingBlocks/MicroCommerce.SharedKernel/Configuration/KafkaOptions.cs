namespace MicroCommerce.SharedKernel.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:29092";

    public string ClientId { get; init; } = "microcommerce";

    public string ConsumerGroup { get; init; } = "microcommerce-group";

    public KafkaTopicOptions Topics { get; init; } = new();
}

public sealed class KafkaTopicOptions
{
    public string UserRegistered { get; init; } = "identity.user-registered";

    public string OrderSubmitted { get; init; } = "ordering.order-submitted";

    public string ProcessPayment { get; init; } = "payments.process-payment";

    public string PaymentSucceeded { get; init; } = "payments.payment-succeeded";

    public string PaymentFailed { get; init; } = "payments.payment-failed";

    public string CreateShipment { get; init; } = "shipping.create-shipment";

    public string ShipmentCreated { get; init; } = "shipping.shipment-created";

    public string ShipmentFailed { get; init; } = "shipping.shipment-failed";

    public IReadOnlyList<string> GetAll() =>
    [
        UserRegistered,
        OrderSubmitted,
        ProcessPayment,
        PaymentSucceeded,
        PaymentFailed,
        CreateShipment,
        ShipmentCreated,
        ShipmentFailed
    ];
}
