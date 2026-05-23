using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsWriter.Services;

namespace RureSubPostsReader.Services;

public class MongoDbInitializer(MongoDbService mongoDbService) : IHostedService
{
    private readonly MongoDbService mongoDbService = mongoDbService;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var inboxMessageIndexKeys = Builders<InboxMessage>.IndexKeys.Ascending(m => m.MessageId);
        var inboxMessageIndexOptions = new CreateIndexOptions
        {
            Unique = true,
            ExpireAfter = TimeSpan.FromDays(7)
        };

        var inboxMessageModel = new CreateIndexModel<InboxMessage>(inboxMessageIndexKeys, inboxMessageIndexOptions);

        await mongoDbService.InboxMessages.Indexes.CreateOneAsync(inboxMessageModel, cancellationToken: cancellationToken);

        var postIndexes = new List<CreateIndexModel<PostDocument>>
        {
            new(Builders<PostDocument>.IndexKeys.Descending(d => d.PostedAt))
        };

        await mongoDbService.Posts.Indexes.CreateManyAsync(postIndexes, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
