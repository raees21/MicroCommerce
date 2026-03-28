namespace MicroCommerce.SharedKernel.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "microcommerce.identity";

    public string Audience { get; init; } = "microcommerce.clients";

    public string SigningKey { get; init; } = "super-secret-dev-key-change-me-please";

    public int ExpirationMinutes { get; init; } = 120;
}
