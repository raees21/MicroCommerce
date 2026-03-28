using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Cart.Api.Models;
using MicroCommerce.Cart.Api.Services;
using MicroCommerce.Contracts.Cart;
using MicroCommerce.SharedKernel.Configuration;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton<CartSnapshotStore>();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "cart", backingStores = new[] { "redis", "mongo" } }));

app.MapGet("/api/cart/{userId:guid}", async (Guid userId, CartSnapshotStore store) =>
{
    var cart = await store.GetAsync(userId);
    return cart is null ? Results.NotFound() : Results.Ok(cart);
}).RequireAuthorization();

app.MapPut("/api/cart/{userId:guid}", async (Guid userId, UpsertCartRequest request, CartSnapshotStore store, CancellationToken cancellationToken) =>
{
    if (userId != request.UserId)
    {
        return Results.BadRequest(new { message = "Route user id and body user id must match." });
    }

    var totalAmount = request.Items.Sum(x => x.UnitPrice * x.Quantity);
    var snapshot = new CartSnapshot(
        request.UserId,
        request.Items,
        totalAmount,
        DateTimeOffset.UtcNow);

    await store.UpsertAsync(snapshot, cancellationToken);

    return Results.Ok(snapshot);
}).RequireAuthorization();

app.MapDelete("/api/cart/{userId:guid}", async (Guid userId, CartSnapshotStore store, CancellationToken cancellationToken) =>
{
    await store.ClearAsync(userId, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();

app.Run();
