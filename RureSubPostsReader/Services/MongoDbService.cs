using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RureSubPostsReader.Models;

namespace RureSubPostsWriter.Services;

public class MongoDbService
{
    private readonly IMongoClient client;
    public IMongoClient Client => client;

    private readonly IMongoCollection<PostDocument> posts;
    public IMongoCollection<PostDocument> Posts => posts;


    private readonly IMongoCollection<InboxMessage> inboxMessages;
    public IMongoCollection<InboxMessage> InboxMessages => inboxMessages;

    public MongoDbService(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        var database = client.GetDatabase(settings.Value.DatabaseName);

        this.client = client;
        posts = database.GetCollection<PostDocument>("posts");
        inboxMessages = database.GetCollection<InboxMessage>("inbox_messages");
    }

    public async Task CreateAsync(PostDocument post)
    {
        await posts.InsertOneAsync(post);
    }

    public async Task<List<PostDocument>> GetByAuthorIdAsync(Guid authorId)
    {
        return await posts
            .Find(p => p.AuthorId == authorId)
            .ToListAsync();
    }

    public async Task<PostDocument?> GetByIdAsync(Guid postId)
    {
        return await posts
            .Find(p => p.Id == postId)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateLikesCountAsync(Guid postId, int likes)
    {
        var update = Builders<PostDocument>
            .Update
            .Set(p => p.LikesCount, likes);

        await posts.UpdateOneAsync(p => p.Id == postId, update);
    }

    public async Task DeleteAsync(Guid postId)
    {
        await posts.DeleteOneAsync(p => p.Id == postId);
    }
}
