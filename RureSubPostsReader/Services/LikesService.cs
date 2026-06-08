using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace RureSubPostsReader.Services;

public class LikesService : ILikesService
{
    private readonly IConnectionMultiplexer redis;

    public LikesService(
        [FromKeyedServices("likes")] IConnectionMultiplexer redis)
    {
        this.redis = redis;
    }
    public async Task<bool[]> IsPostsLiked(Guid userId, Guid[] postIds)
    {
        var db = redis.GetDatabase();

        string? userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            var result = new bool[postIds.Length];
            Array.Fill(result, false);
            return result;
        }

        string hashKey = $"user:{userRedisId}:liked_posts";

        RedisValue[] fields = [.. postIds.Select(id => (RedisValue)id.ToString())];

        RedisValue[] results = await db.HashGetAsync(hashKey, fields);

        return [.. results.Select(v => !v.IsNull)];
    }

    public async Task<Guid[]> GetUserLikes(Guid userId, int pageSize, int page)
    {
        var redisDb = redis.GetDatabase();

        string? likesUserRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(likesUserRedisId))
        {
            return [];
        }

        int start = (page - 1) * pageSize;
        int stop = start + pageSize - 1;

        var postIds = (await redisDb.SortedSetRangeByRankAsync(
            $"user:{likesUserRedisId}:liked_posts_sorted",
            start,
            stop,
            Order.Descending
        ))
        .Select(x =>
        {
            if (!Guid.TryParse(x.ToString(), out var id))
                return (Guid?)null;

            return id;
        })
        .Where(x => x != null)
        .Select(x => x!.Value)
        .ToArray();

        return postIds ?? [];
    }
}
