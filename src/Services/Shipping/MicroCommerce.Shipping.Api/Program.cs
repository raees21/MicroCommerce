using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;
using MicroCommerce.Shipping.Api.Data;
using MicroCommerce.Shipping.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

builder.Services.AddDbContext<ShippingDbContext>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddHostedService<PaymentSucceededConsumer>();
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
    var dbContext = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "shipping", mode = "simulated" }));

app.MapGet("/api/shipments/{orderId:guid}", async (Guid orderId, ShippingDbContext dbContext, CancellationToken cancellationToken) =>
{
    var shipment = await dbContext.Shipments
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    return shipment is null ? Results.NotFound() : Results.Ok(shipment);
}).RequireAuthorization();

app.Run();
