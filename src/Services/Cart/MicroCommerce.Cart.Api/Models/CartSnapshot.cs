using MicroCommerce.Contracts.Cart;

namespace MicroCommerce.Cart.Api.Models;

public sealed record CartSnapshot(
    Guid UserId,
    List<CartItemDto> Items,
    decimal TotalAmount,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertCartRequest(Guid UserId, List<CartItemDto> Items);
