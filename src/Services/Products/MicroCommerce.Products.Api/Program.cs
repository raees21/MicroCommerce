using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Contracts.Products;
using MicroCommerce.Products.Api.Data;
using MicroCommerce.Products.Api.Grpc;
using MicroCommerce.SharedKernel.Configuration;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddGrpc();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    if (!await dbContext.Products.AnyAsync())
    {
        dbContext.Products.AddRange(
            new ProductEntity
            {
                Sku = "SKU-CHAIR-001",
                Name = "Nordic Accent Chair",
                Description = "A simple seeded product to test the checkout flow.",
                Price = 249.00m,
                AvailableStock = 12
            },
            new ProductEntity
            {
                Sku = "SKU-LAMP-002",
                Name = "Studio Desk Lamp",
                Description = "Warm light desk lamp used for sample ordering.",
                Price = 89.50m,
                AvailableStock = 30
            });

        await dbContext.SaveChangesAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<ProductCatalogGrpcService>();

app.MapGet("/", () => Results.Ok(new
{
    service = "products",
    rest = "/api/products",
    grpc = "ProductCatalog/GetProducts"
}));

app.MapGet("/api/products", async (ProductDbContext dbContext, CancellationToken cancellationToken) =>
{
    var products = await dbContext.Products
        .AsNoTracking()
        .OrderBy(x => x.Name)
        .Select(x => new ProductDto(x.Id, x.Sku, x.Name, x.Description, x.Price, x.AvailableStock, x.IsActive))
        .ToListAsync(cancellationToken);

    return Results.Ok(products);
});

app.MapGet("/api/products/{id:guid}", async (Guid id, ProductDbContext dbContext, CancellationToken cancellationToken) =>
{
    var product = await dbContext.Products
        .AsNoTracking()
        .Where(x => x.Id == id)
        .Select(x => new ProductDto(x.Id, x.Sku, x.Name, x.Description, x.Price, x.AvailableStock, x.IsActive))
        .SingleOrDefaultAsync(cancellationToken);

    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.MapPost("/api/products", async (UpsertProductRequest request, ProductDbContext dbContext, CancellationToken cancellationToken) =>
{
    var entity = new ProductEntity
    {
        Sku = request.Sku,
        Name = request.Name,
        Description = request.Description,
        Price = request.Price,
        AvailableStock = request.AvailableStock,
        IsActive = request.IsActive
    };

    dbContext.Products.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/products/{entity.Id}", new ProductDto(
        entity.Id,
        entity.Sku,
        entity.Name,
        entity.Description,
        entity.Price,
        entity.AvailableStock,
        entity.IsActive));
}).RequireAuthorization();

app.Run();
