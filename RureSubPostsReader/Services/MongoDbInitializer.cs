using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsWriter.Services;

namespace RureSubPostsReader.Services;

public class MongoDbInitializer(MongoDbService mongoDbService) : IHostedService
{
    private readonly MongoDbService mongoDbService = mongoDbService;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexKeys = Builders<InboxMessage>.IndexKeys.Ascending(m => m.MessageId);
        var indexOptions = new CreateIndexOptions
        {
            Unique = true,
            ExpireAfter = TimeSpan.FromDays(7)
        };

        var model = new CreateIndexModel<InboxMessage>(indexKeys, indexOptions);

        await mongoDbService.InboxMessages.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
