namespace MicroCommerce.Contracts.Cart;

public sealed record CartDto(
    Guid UserId,
    IReadOnlyList<CartItemDto> Items,
    decimal TotalAmount,
    DateTimeOffset UpdatedAtUtc);

public sealed record CartItemDto(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);
