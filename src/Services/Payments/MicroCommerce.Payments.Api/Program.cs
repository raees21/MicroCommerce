using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Contracts.Payments;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.Payments.Api.Data;
using MicroCommerce.Payments.Api.Services;
using MicroCommerce.SharedKernel.Configuration;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddHostedService<OrderSubmittedConsumer>();
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
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "payments", mode = "simulated" }));

app.MapGet("/api/payments/{orderId:guid}", async (Guid orderId, PaymentsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var record = await dbContext.PaymentRecords
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    return record is null ? Results.NotFound() : Results.Ok(record);
}).RequireAuthorization();

app.MapPost("/api/payments/{orderId:guid}/decision", async (
    Guid orderId,
    PaymentDecisionRequest request,
    PaymentsDbContext dbContext,
    IEventPublisher eventPublisher,
    IOptions<KafkaOptions> kafkaOptions,
    CancellationToken cancellationToken) =>
{
    var record = await dbContext.PaymentRecords
        .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    if (record is null)
    {
        return Results.NotFound(new { message = "Payment record could not be found." });
    }

    if (!string.Equals(record.Status, "Pending", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = $"Payment is already {record.Status}." });
    }

    record.Status = request.Approved ? "Authorized" : "Rejected";
    record.Details = request.Approved
        ? "Approved manually from the demo UI."
        : "Rejected manually from the demo UI.";

    await dbContext.SaveChangesAsync(cancellationToken);

    if (request.Approved)
    {
        await eventPublisher.PublishAsync(
            kafkaOptions.Value.Topics.PaymentSucceeded,
            new PaymentSucceededIntegrationEvent(
                record.OrderId,
                record.UserId,
                record.Amount,
                $"AUTH-{record.OrderId:N}"[..12]),
            cancellationToken);
    }
    else
    {
        await eventPublisher.PublishAsync(
            kafkaOptions.Value.Topics.PaymentFailed,
            new PaymentFailedIntegrationEvent(
                record.OrderId,
                record.UserId,
                record.Amount,
                "Rejected manually from the demo UI."),
            cancellationToken);
    }

    return Results.Ok(new
    {
        record.OrderId,
        record.Status,
        record.Details
    });
}).RequireAuthorization();

app.Run();

public sealed record PaymentDecisionRequest(bool Approved);
