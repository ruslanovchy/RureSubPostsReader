using StackExchange.Redis;

namespace RureSubPostsReader.Services;

public class FollowersService : IFollowersService
{
    private readonly IConnectionMultiplexer redis;

    public FollowersService(
        [FromKeyedServices("followers")] IConnectionMultiplexer redis)
    {
        this.redis = redis;
    }

    public async Task<bool[]> IsFollowed(Guid userId, Guid[] followingIds)
    {
        var db = redis.GetDatabase();

        var userRedisId = await db.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            var result = new bool[followingIds.Length];
            Array.Fill(result, false);
            return result;
        }

        string?[] authorRedisIds = await GetUserRedisIds(followingIds);
        var tasks = new Task<long?>[followingIds.Length];

        string sortedSetKey = $"user:{userRedisId}:followings";

        for (int i = 0; i < tasks.Length; i++)
        {
            var member = authorRedisIds[i];

            tasks[i] = db.SortedSetRankAsync(sortedSetKey, member);
        }

        long?[] results = await Task.WhenAll(tasks);

        return [.. results.Select(r => r.HasValue)];
    }

    private async Task<string?[]> GetUserRedisIds(Guid[] userIds)
    {
        var db = redis.GetDatabase();

        var tasks = new Task<RedisValue>[userIds.Length];

        for (int i = 0; i < tasks.Length; i++)
        {
            var key = $"user:id:{userIds[i]}";

            tasks[i] = db.StringGetAsync(key);
        }

        var result = await Task.WhenAll(tasks);

        return [.. result.Select(r => (string?)r)];
    }
}
