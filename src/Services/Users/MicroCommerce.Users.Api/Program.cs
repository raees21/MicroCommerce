using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;
using MicroCommerce.Users.Api.Data;
using MicroCommerce.Users.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

builder.Services.AddDbContext<UserProfileDbContext>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddHostedService<UserRegisteredConsumer>();
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
    var dbContext = scope.ServiceProvider.GetRequiredService<UserProfileDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "users", projection = "identity.user-registered" }));

app.MapGet("/api/users/{id:guid}", async (Guid id, UserProfileDbContext dbContext, CancellationToken cancellationToken) =>
{
    var user = await dbContext.UserProfiles
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    return user is null ? Results.NotFound() : Results.Ok(user);
}).RequireAuthorization();

app.MapGet("/api/users", async (UserProfileDbContext dbContext, CancellationToken cancellationToken) =>
{
    var users = await dbContext.UserProfiles
        .AsNoTracking()
        .OrderBy(x => x.CreatedAtUtc)
        .ToListAsync(cancellationToken);

    return Results.Ok(users);
}).RequireAuthorization();

app.Run();
