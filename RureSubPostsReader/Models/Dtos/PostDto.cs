using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsReader.Models.Dtos;

public class PostDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }
    public string Title { get; set; } = string.Empty;

    public object Content { get; set; } = null!;

    public int LikesCount { get; set; }
    public bool IsLiked { get; set; }
    public int CommentsCount { get; set; }
    public bool IsEdited { get; set; }
    public DateTime PostedAt { get; set; }
}
