using Confluent.Kafka;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsWriter.Services;
using System.Text.Json;

namespace RureSubPostsReader.Services;

public class PostsCreateProcesser : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly ILogger<PostsCreateProcesser> logger;
    private readonly MongoDbService mongoService;

    public PostsCreateProcesser(ConsumerConfig config, ILogger<PostsCreateProcesser> logger, MongoDbService postsService)
    {
        this.config = config;
        this.logger = logger;
        this.mongoService = postsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("post-created");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
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

                var inboxMessage = new InboxMessage { MessageId = messageId, Content = result.Message.Value, Topic = "post-created", ProcessedAt = DateTime.UtcNow };

                try
                {
                    await mongoService.InboxMessages.InsertOneAsync(inboxMessage, cancellationToken: stoppingToken);
                }
                catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    consumer.Commit(result);
                    continue;
                }

                var post = JsonSerializer.Deserialize<PostDocument>(result.Message.Value);

                if (post == null)
                {
                    consumer.Commit(result);
                    continue;
                }

                await mongoService.Posts.InsertOneAsync(post, cancellationToken: stoppingToken);
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
