using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MicroCommerce.Contracts.Identity;
using MicroCommerce.Identity.Api.Data;
using MicroCommerce.Identity.Api.Models;
using MicroCommerce.Identity.Api.Services;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
var kafkaOptions = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();

builder.Services.AddDbContext<IdentityDb>(options =>
    options.UseNpgsql(storageOptions.PostgresConnectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDb>()
    .AddSignInManager();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddKafkaMessaging(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDb>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapGet("/", () => Results.Ok(new { service = "identity", auth = "/api/auth" }));

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    JwtTokenService tokenService,
    IEventPublisher eventPublisher,
    CancellationToken cancellationToken) =>
{
    var user = new ApplicationUser
    {
        UserName = request.Email,
        Email = request.Email,
        FullName = request.FullName
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new
        {
            errors = result.Errors.Select(x => x.Description)
        });
    }

    await eventPublisher.PublishAsync(
        kafkaOptions.Topics.UserRegistered,
        new UserRegisteredIntegrationEvent(user.Id, user.Email ?? string.Empty, user.FullName),
        cancellationToken);

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.FullName,
        token = tokenService.CreateToken(user)
    });
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokenService) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);
    if (!result.Succeeded)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.FullName,
        token = tokenService.CreateToken(user)
    });
});

app.Run();
