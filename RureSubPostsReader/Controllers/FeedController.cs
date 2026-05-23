using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using RureSubPostsReader.Models;
using RureSubPostsWriter.Services;
using System.Runtime.CompilerServices;

namespace RureSubPostsReader.Controllers;

[Route("/")]
public class FeedController : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Feed([FromServices]MongoDbService db, [FromQuery]int page = 1, [FromQuery]int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 10 ? 10 : pageSize > 100 ? 100 : pageSize;

        var filter = Builders<PostDocument>.Filter.Empty;
        var sort = Builders<PostDocument>.Sort.Descending(d => d.PostedAt);

        var posts = await db.Posts.Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var result = posts
            .Select(p => new
            {
                p.Id,
                p.AuthorId,
                p.Title,
                Content = BsonTypeMapper.MapToDotNetValue(p.Content),
                p.LikesCount,
                p.CommentsCount,
                p.IsEdited,
                p.PostedAt
            })
            .ToList();

        return Ok(result);
    }

}
