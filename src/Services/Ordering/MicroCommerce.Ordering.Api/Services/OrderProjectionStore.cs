using Microsoft.Extensions.Options;
using MicroCommerce.Ordering.Api.Models;
using MicroCommerce.SharedKernel.Configuration;
using MongoDB.Driver;

namespace MicroCommerce.Ordering.Api.Services;

public sealed class OrderProjectionStore
{
    private readonly IMongoCollection<OrderReadModel> _collection;

    public OrderProjectionStore(IOptions<StorageOptions> storageOptions)
    {
        var options = storageOptions.Value;
        var client = new MongoClient(options.MongoConnectionString);
        var database = client.GetDatabase(options.MongoDatabaseName);
        _collection = database.GetCollection<OrderReadModel>("orders");
    }

    public Task<OrderReadModel?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        _collection.Find(x => x.Id == orderId).FirstOrDefaultAsync(cancellationToken);

    public Task UpsertAsync(OrderReadModel model, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(
            x => x.Id == model.Id,
            model,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
}
