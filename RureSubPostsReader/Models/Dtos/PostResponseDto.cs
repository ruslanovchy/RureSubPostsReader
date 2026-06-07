using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsReader.Models.Dtos;

public class PostResponseDto
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }
    public string Title { get; set; } = string.Empty;

    public MediaFileResponseDto[]? MediaFiles { get; set; }
    public object? Content { get; set; }

    public int LikesCount { get; set; }
    public bool IsLiked { get; set; }
    public bool IsFollowed { get; set; }
    public int CommentsCount { get; set; }
    public bool IsEdited { get; set; }
    public DateTime PostedAt { get; set; }
}
