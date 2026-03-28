using System.Text.Json;
using Microsoft.Extensions.Options;
using MicroCommerce.Cart.Api.Models;
using MicroCommerce.SharedKernel.Configuration;
using MongoDB.Driver;
using StackExchange.Redis;

namespace MicroCommerce.Cart.Api.Services;

public sealed class CartSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _redis;
    private readonly IMongoCollection<CartSnapshot> _collection;

    public CartSnapshotStore(IOptions<StorageOptions> storageOptions)
    {
        var options = storageOptions.Value;

        var multiplexer = ConnectionMultiplexer.Connect(options.RedisConnectionString);
        _redis = multiplexer.GetDatabase();

        var client = new MongoClient(options.MongoConnectionString);
        var database = client.GetDatabase(options.MongoDatabaseName);
        _collection = database.GetCollection<CartSnapshot>("cart_snapshots");
    }

    public async Task<CartSnapshot?> GetAsync(Guid userId)
    {
        var redisValue = await _redis.StringGetAsync(GetRedisKey(userId));
        if (redisValue.HasValue)
        {
            return JsonSerializer.Deserialize<CartSnapshot>(redisValue.ToString(), JsonOptions);
        }

        return await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(CartSnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(snapshot, JsonOptions);
        await _redis.StringSetAsync(GetRedisKey(snapshot.UserId), payload, TimeSpan.FromHours(12));
        await _collection.ReplaceOneAsync(
            x => x.UserId == snapshot.UserId,
            snapshot,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _redis.KeyDeleteAsync(GetRedisKey(userId));
        await _collection.DeleteOneAsync(x => x.UserId == userId, cancellationToken);
    }

    private static string GetRedisKey(Guid userId) => $"cart:{userId:N}";
}
