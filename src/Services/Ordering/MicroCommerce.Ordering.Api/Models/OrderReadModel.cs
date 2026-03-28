namespace MicroCommerce.Ordering.Api.Models;

public sealed record OrderReadModel
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string IdempotencyHash { get; init; } = string.Empty;

    public string Status { get; init; } = "PendingPayment";

    public string ShippingAddress { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public List<OrderReadModelLine> Lines { get; init; } = new();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record OrderReadModelLine
{
    public Guid ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }
}
