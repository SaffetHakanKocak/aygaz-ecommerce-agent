using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.DataAccess;

public sealed class MongoDatabaseInitializer(IMongoDatabase database) : IMongoDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        foreach ((string collectionName, IReadOnlyList<CreateIndexModel<BsonDocument>> indexes) in MongoCollectionSetup.CreateIndexModels())
        {
            await database
                .GetCollection<BsonDocument>(collectionName)
                .Indexes
                .CreateManyAsync(indexes, cancellationToken);
        }
    }
}
