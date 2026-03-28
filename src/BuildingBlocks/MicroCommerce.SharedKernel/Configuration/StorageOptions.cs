namespace MicroCommerce.SharedKernel.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string PostgresConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=microcommerce;Username=postgres;Password=postgres";

    public string MongoConnectionString { get; init; } = "mongodb://localhost:27017";

    public string MongoDatabaseName { get; init; } = "microcommerce";

    public string RedisConnectionString { get; init; } = "localhost:6379";
}
