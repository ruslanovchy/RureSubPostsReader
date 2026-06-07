using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsReader.Models.Dtos;
using RureSubPostsReader.Services;
using StackExchange.Redis;
using System.Security.Claims;

namespace RureSubPostsReader.Controllers;

[Route("/")]
public class PostsController : Controller
{
    private readonly ILogger<PostsController> logger;

    public PostsController([FromServices] ILogger<PostsController> logger)
    {
        this.logger = logger;
    }

    public async Task<bool[]> CheckLikesWithPipelineAsync(IDatabase db, string userId, Guid[] postIds)
    {
        string hashKey = $"user:{userId}:liked_posts";

        RedisValue[] fields = [.. postIds.Select(id => (RedisValue)id.ToString())];

        RedisValue[] results = await db.HashGetAsync(hashKey, fields);

        return [.. results.Select(v => !v.IsNull)];
    }

    public async Task<string?[]> GetUsersRedisId(IDatabase db, Guid[] userIds)
    {
        var tasks = new Task<RedisValue>[userIds.Length];

        for (int i = 0; i < tasks.Length; i++)
        {
            var key = $"user:id:{userIds[i]}";

            tasks[i] = db.StringGetAsync(key);
        }

        var result = await Task.WhenAll(tasks);

        return [.. result.Select(r => (string?)r)];
    }

    public async Task<bool[]> CheckFollowingsWithPipelineAsync(IDatabase db, string userId, Guid[] authorIds)
    {
        string?[] authorRedisIds = await GetUsersRedisId(db, authorIds);
        var tasks = new Task<long?>[authorIds.Length];

        string sortedSetKey = $"user:{userId}:following";

        for (int i = 0; i < tasks.Length; i++)
        {
            var member = authorRedisIds[i];

            tasks[i] = db.SortedSetRankAsync(sortedSetKey, member);
        }

        long?[] results = await Task.WhenAll(tasks);

        return [.. results.Select(r => r.HasValue)];
    }

    public static PostResponseDto GetPostResponseDto(PostDocument document) => new()
    {
        Id = document.Id,
        AuthorId = document.AuthorId,
        Author = document.Author,
        Title = document.Title,
        MediaFiles = document.MediaFiles?.Select(p => new MediaFileResponseDto
        {
            Id = p.Id,
            Src = p.Path,
            Type = p.Type
        }).ToArray(),
        Content = document.Content == null ? null : BsonTypeMapper.MapToDotNetValue(document.Content),
        LikesCount = document.LikesCount,
        CommentsCount = document.CommentsCount,
        IsEdited = document.IsEdited,
        PostedAt = document.PostedAt,
        IsFollowed = false,
    };

    [HttpGet("/")]
    public async Task<IActionResult> GetPost(
        [FromServices] MongoDbService db,
        [FromServices] IConnectionMultiplexer redis,
        [FromQuery] Guid? id)
    {
        if (id == null)
        {
            return BadRequest();
        }

        var filter = Builders<PostDocument>.Filter.Eq(p => p.Id, id);

        var post = await db.Posts.Find(filter).FirstOrDefaultAsync();

        if (post == null)
        {
            return NotFound();
        }

        var result = GetPostResponseDto(post);

        var redisDb = redis.GetDatabase();

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Ok(result);
        }
        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return Ok(result);
        }

        var postIsLiked = await CheckLikesWithPipelineAsync(redisDb, userRedisId, [ result.Id ]);
        var postIsFollowed = await CheckFollowingsWithPipelineAsync(redisDb, userRedisId, [ result.AuthorId ]);

        if (postIsLiked.Length == 1 && postIsLiked[0])
        {
            result.IsLiked = true;
        }

        if (postIsFollowed.Length == 1 && postIsFollowed[0])
        {
            result.IsFollowed = true;
        }

        return Ok(result);
    }

    [HttpGet("/feed")]
    public async Task<IActionResult> Feed(
        [FromServices] MongoDbService db,
        [FromServices] IConnectionMultiplexer redis,
        [FromQuery] int pageSize = 10, 
        [FromQuery] DateTime? lastPostedAt = null,
        [FromQuery] Guid? lastId = null)
    {
        lastPostedAt ??= DateTime.MaxValue;
        lastId ??= Guid.Empty;

        pageSize = pageSize < 2 ? 2 : pageSize > 100 ? 100 : pageSize;

        var filter = Builders<PostDocument>.Filter.Or(
            Builders<PostDocument>.Filter.Lt(d => d.PostedAt, lastPostedAt),
            Builders<PostDocument>.Filter.And(
                Builders<PostDocument>.Filter.Eq(d => d.PostedAt, lastPostedAt),
                Builders<PostDocument>.Filter.Lt(d => d.Id, lastId)
            )
        );
        var sort = Builders<PostDocument>.Sort
            .Descending(d => d.PostedAt)
            .Descending(d => d.Id);

        var posts = await db.Posts.Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync();

        var result = posts
            .Select(GetPostResponseDto)
            .ToList();

        var redisDb = redis.GetDatabase();

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Ok(result);
        }
        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return Ok(result);
        }

        var postsIsLiked = await CheckLikesWithPipelineAsync(redisDb, userRedisId, [.. result.Select(p => p.Id)]);
        var postsIsFollowed = await CheckFollowingsWithPipelineAsync(redisDb, userRedisId, [.. result.Select(p => p.AuthorId)]);

        if (postsIsLiked.Length != result.Count || postsIsFollowed.Length != result.Count)
        {
            return Ok(result);
        }

        for (int i = 0; i < postsIsLiked.Length; i++)
        {
            result[i].IsLiked = postsIsLiked[i];
            result[i].IsFollowed = postsIsFollowed[i];
        }

        return Ok(result);
    }

    [HttpGet("/user")]
    public async Task<IActionResult> UserPosts(
        [FromServices] MongoDbService db,
        [FromQuery] Guid id,
        [FromServices] IConnectionMultiplexer redis,
        [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? lastPostedAt = null,
        [FromQuery] Guid? lastId = null)
    {
        lastPostedAt ??= DateTime.MaxValue;
        lastId ??= Guid.Empty;
        pageSize = pageSize < 10 ? 10 : pageSize > 100 ? 100 : pageSize;

        var filter = Builders<PostDocument>.Filter.And(
            Builders<PostDocument>.Filter.Or(
                Builders<PostDocument>.Filter.Lt(d => d.PostedAt, lastPostedAt),
                Builders<PostDocument>.Filter.And(
                    Builders<PostDocument>.Filter.Eq(d => d.PostedAt, lastPostedAt),
                    Builders<PostDocument>.Filter.Lt(d => d.Id, lastId)
                )
            ),
            Builders<PostDocument>.Filter.Eq(d => d.AuthorId, id)
        );
        var sort = Builders<PostDocument>.Sort
            .Descending(d => d.PostedAt)
            .Descending(d => d.Id);

        var posts = await db.Posts.Find(filter)
            .Sort(sort)
            .Limit(pageSize)
            .ToListAsync();

        var result = posts
            .Select(GetPostResponseDto)
            .ToList();

        var redisDb = redis.GetDatabase();

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Ok(result);
        }
        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return Ok(result);
        }

        var postsIsLiked = await CheckLikesWithPipelineAsync(redisDb, userRedisId, [.. result.Select(p => p.Id)]);

        if (postsIsLiked.Length != result.Count)
        {
            return Ok(result);
        }

        for (int i = 0; i < postsIsLiked.Length; i++)
        {
            result[i].IsLiked = postsIsLiked[i];
        }

        return Ok(result);
    }

    [HttpGet("/user_likes")]
    public async Task<IActionResult> UserLikes(
        [FromServices] MongoDbService db,
        [FromServices] IConnectionMultiplexer redis,
        [FromQuery] Guid id,
        [FromQuery] int pageSize = 3,
        [FromQuery] int page = 1)
    {
        var redisDb = redis.GetDatabase();

        string? likesUserRedisId = await redisDb.StringGetAsync($"user:id:{id}");

        if (string.IsNullOrEmpty(likesUserRedisId))
        {
            return NotFound();
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
        .ToList();

        var notNullPosts = postIds;

        var filter = Builders<PostDocument>.Filter.In(p => p.Id, postIds);

        var posts = (await db.Posts.Find(filter).ToListAsync())
            .ToDictionary(x => x.Id);

        var orderedPosts = postIds
            .Where(id => posts.ContainsKey(id))
            .Select(id => posts[id])
            .ToList();

        var result = orderedPosts
            .Select(GetPostResponseDto)
            .ToList();

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Ok(result);
        }
        string? userRedisId = await redisDb.StringGetAsync($"user:id:{userId}");

        if (string.IsNullOrEmpty(userRedisId))
        {
            return Ok(result);
        }

        var postsIsLiked = await CheckLikesWithPipelineAsync(redisDb, userRedisId, [.. result.Select(p => p.Id)]);

        if (postsIsLiked.Length != result.Count)
        {
            return Ok(result);
        }

        for (int i = 0; i < postsIsLiked.Length; i++)
        {
            result[i].IsLiked = postsIsLiked[i];
        }

        return Ok(result);
    }
}
