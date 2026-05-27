using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Models.Dtos;
using System.Text.Json;

namespace RureSubPostsReader.Services;

public class PostsCreateProcessor : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly ILogger<PostsCreateProcessor> logger;
    private readonly MongoDbService mongoService;

    public PostsCreateProcessor(ConsumerConfig config, ILogger<PostsCreateProcessor> logger, MongoDbService postsService)
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

                var postDto = JsonSerializer.Deserialize<CreatePostDto>(result.Message.Value);

                if (postDto == null)
                {
                    consumer.Commit(result);
                    continue;
                }

                var post = new PostDocument
                {
                    Id = postDto.Id,
                    AuthorId = postDto.AuthorId,
                    Author = postDto.Author,
                    Title = postDto.Title,
                    Content = BsonDocument.Parse(postDto.Content),
                    PostedAt = postDto.PostedAt
                };

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
