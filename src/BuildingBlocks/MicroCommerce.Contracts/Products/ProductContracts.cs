namespace MicroCommerce.Contracts.Products;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int AvailableStock,
    bool IsActive);

public sealed record UpsertProductRequest(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int AvailableStock,
    bool IsActive);
