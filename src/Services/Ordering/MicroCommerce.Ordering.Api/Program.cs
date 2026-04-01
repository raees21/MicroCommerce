using Grpc.Net.Client;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Contracts.Ordering;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.Ordering.Api.Data;
using MicroCommerce.Ordering.Api.Services;
using MicroCommerce.Products.Grpc;
using MicroCommerce.SharedKernel.Configuration;
using MicroCommerce.SharedKernel.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<ServiceEndpointsOptions>(builder.Configuration.GetSection(ServiceEndpointsOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
var serviceEndpoints = builder.Configuration.GetSection(ServiceEndpointsOptions.SectionName).Get<ServiceEndpointsOptions>() ?? new ServiceEndpointsOptions();

builder.Services.AddDbContext<OrderingDbContext>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddGrpc();
builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddSingleton<OrderProjectionStore>();
builder.Services.AddScoped<OrderStateUpdater>();

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(serviceEndpoints.ProductsGrpcUrl);
    return new ProductCatalog.ProductCatalogClient(channel);
});

builder.Services.AddHostedService<PaymentSucceededConsumer>();
builder.Services.AddHostedService<PaymentFailedConsumer>();
builder.Services.AddHostedService<ShipmentCreatedConsumer>();
builder.Services.AddHostedService<ShipmentFailedConsumer>();

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
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "ordering",
    workflow = "REST in, gRPC to products, Kafka saga kickoff"
}));

app.MapPost("/api/orders", async (
    PlaceOrderRequest request,
    ProductCatalog.ProductCatalogClient productsClient,
    OrderStateUpdater stateUpdater,
    IEventPublisher eventPublisher,
    IOptions<KafkaOptions> kafkaOptions,
    CancellationToken cancellationToken) =>
{
    if (request.Lines.Count == 0)
    {
        return Results.BadRequest(new { message = "An order needs at least one line." });
    }

    var productResponse = await productsClient.GetProductsAsync(new GetProductsRequest
    {
        ProductIds = { request.Lines.Select(x => x.ProductId.ToString()) }
    }, cancellationToken: cancellationToken);

    var products = productResponse.Products.ToDictionary(x => Guid.Parse(x.Id));

    if (products.Count != request.Lines.Count)
    {
        return Results.BadRequest(new { message = "One or more products could not be found." });
    }

    var orderLines = new List<OrderLineSnapshot>();
    decimal total = 0m;

    foreach (var line in request.Lines)
    {
        var product = products[line.ProductId];
        if (!product.IsActive || product.AvailableStock < line.Quantity)
        {
            return Results.BadRequest(new { message = $"Product {product.Name} is unavailable for quantity {line.Quantity}." });
        }

        var unitPrice = Convert.ToDecimal(product.Price);
        total += unitPrice * line.Quantity;
        orderLines.Add(new OrderLineSnapshot(line.ProductId, product.Sku, product.Name, line.Quantity, unitPrice));
    }

    var rawHash = string.Join('|',
        request.UserId,
        request.ShippingAddress.Trim().ToLowerInvariant(),
        string.Join(';', orderLines.OrderBy(x => x.ProductId).Select(x => $"{x.ProductId}:{x.Quantity}:{x.UnitPrice:F2}")));

    var integrationEvent = new OrderSubmittedIntegrationEvent(
        OrderId: Guid.NewGuid(),
        UserId: request.UserId,
        IdempotencyHash: IdempotencyHasher.Compute(rawHash),
        ShippingAddress: request.ShippingAddress,
        PaymentToken: request.PaymentToken,
        TotalAmount: total,
        Lines: orderLines);

    await stateUpdater.CreatePendingOrderAsync(integrationEvent.OrderId, integrationEvent, cancellationToken);
    await eventPublisher.PublishAsync(kafkaOptions.Value.Topics.OrderSubmitted, integrationEvent, cancellationToken);

    return Results.Accepted($"/api/orders/{integrationEvent.OrderId}", new
    {
        integrationEvent.OrderId,
        integrationEvent.IdempotencyHash,
        integrationEvent.TotalAmount,
        Status = "PendingPayment"
    });
}).RequireAuthorization();

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, OrderProjectionStore store, CancellationToken cancellationToken) =>
{
    var order = await store.GetAsync(orderId, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
}).RequireAuthorization();

app.MapGet("/api/orders", async (ClaimsPrincipal user, OrderProjectionStore store, CancellationToken cancellationToken) =>
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdValue, out var userId))
    {
        return Results.Unauthorized();
    }

    var orders = await store.ListByUserAsync(userId, cancellationToken);
    return Results.Ok(orders.Select(order => new
    {
        OrderId = order.Id,
        order.IdempotencyHash,
        order.Status,
        order.TotalAmount,
        order.CreatedAtUtc,
        order.UpdatedAtUtc
    }));
}).RequireAuthorization();

app.Run();
