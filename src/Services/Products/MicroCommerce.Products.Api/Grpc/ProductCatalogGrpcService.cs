using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroCommerce.Products.Api.Data;
using MicroCommerce.Products.Grpc;

namespace MicroCommerce.Products.Api.Grpc;

public sealed class ProductCatalogGrpcService(ProductDbContext dbContext) : ProductCatalog.ProductCatalogBase
{
    public override async Task<GetProductsResponse> GetProducts(GetProductsRequest request, ServerCallContext context)
    {
        var ids = request.ProductIds
            .Select(Guid.Parse)
            .ToArray();

        var products = await dbContext.Products
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(context.CancellationToken);

        var response = new GetProductsResponse();
        response.Products.AddRange(products.Select(product => new ProductSnapshot
        {
            Id = product.Id.ToString(),
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Price = Convert.ToDouble(product.Price),
            AvailableStock = product.AvailableStock,
            IsActive = product.IsActive
        }));

        return response;
    }
}
