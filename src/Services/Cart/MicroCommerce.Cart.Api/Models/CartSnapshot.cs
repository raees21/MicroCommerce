using MicroCommerce.Contracts.Cart;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroCommerce.Cart.Api.Models;

public sealed record CartSnapshot(
    [property: BsonId]
    [property: BsonGuidRepresentation(GuidRepresentation.Standard)]
    Guid UserId,
    List<CartSnapshotItem> Items,
    decimal TotalAmount,
    DateTimeOffset UpdatedAtUtc);

public sealed record CartSnapshotItem(
    [property: BsonGuidRepresentation(GuidRepresentation.Standard)]
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);

public sealed record UpsertCartRequest(Guid UserId, List<CartItemDto> Items);
