using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Models.Dtos;
using RureSubPostsReader.Services;
using System.Text.Json;

namespace RureSubPostsReader.Workers;

public class PostsDeletedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly ILogger<PostsCreateWorker> logger;
    private readonly MongoDbService mongoService;

    public PostsDeletedWorker(ConsumerConfig config, ILogger<PostsCreateWorker> logger, MongoDbService postsService)
    {
        this.config = config;
        this.logger = logger;
        this.mongoService = postsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("post-deleted");

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

                var postDto = JsonSerializer.Deserialize<PostDeleteRequestDto>(result.Message.Value);

                if (postDto == null)
                {
                    consumer.Commit(result);
                    continue;
                }

                var filter = Builders<PostDocument>.Filter.Eq(d => d.Id, postDto.Id);

                await mongoService.Posts.DeleteOneAsync(filter, stoppingToken);
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
