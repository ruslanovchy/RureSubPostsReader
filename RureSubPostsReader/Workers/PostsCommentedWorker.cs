using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Models.Dtos;
using RureSubPostsReader.Services;
using System.Text.Json;

namespace RureSubPostsReader.Workers;

public class PostsCommentedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly ILogger<PostsCreateWorker> logger;
    private readonly MongoDbService mongoService;

    public PostsCommentedWorker(ConsumerConfig config, ILogger<PostsCreateWorker> logger, MongoDbService postsService)
    {
        this.config = config;
        this.logger = logger;
        this.mongoService = postsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(["post-commented", "comments-deleted"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            if (result == null)
            {
                consumer.Commit(result);
                continue;
            }

            var messageIdRaw = result.Message.Key;

            if (string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
            {
                logger.LogError("Message key, that should represents inbox message id is null or empty!");
                consumer.Commit(result);
                continue;
            }

            var inboxMessage = new InboxMessage { MessageId = messageId, Content = result.Message.Value, Topic = "post-liked", ProcessedAt = DateTime.UtcNow };

            try
            {
                await mongoService.InboxMessages.InsertOneAsync(inboxMessage, cancellationToken: stoppingToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                consumer.Commit(result);
                continue;
            }

            try
            {
                var dto = JsonSerializer.Deserialize<CounterChangeDto>(result.Message.Value);

                if (dto == null)
                {
                    consumer.Commit(result);
                    continue;
                }

                var filter = Builders<PostDocument>.Filter.Eq(p => p.Id, dto.PostId);
                var update = Builders<PostDocument>.Update.Inc(p => p.CommentsCount, dto.Value);

                await mongoService.Posts.UpdateOneAsync(filter, update, cancellationToken: stoppingToken);
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing kafka message!");
                continue;
            }
        }
    }
}
