namespace MicroCommerce.SharedKernel.Configuration;

public sealed class ServiceEndpointsOptions
{
    public const string SectionName = "ServiceEndpoints";

    public string GatewayBaseUrl { get; init; } = "http://localhost:5080";

    public string ProductsGrpcUrl { get; init; } = "http://products-api:8081";

    public string UsersGrpcUrl { get; init; } = "http://users-api:8081";
}
