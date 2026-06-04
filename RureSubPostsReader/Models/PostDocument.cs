using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsReader.Models;

public class PostDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }
    public string Title { get; set; } = string.Empty;

    public MediaFileDocument[]? MediaFiles { get; set; }
    public BsonDocument? Content { get; set; }

    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public bool IsEdited { get; set; }
    public DateTime PostedAt { get; set; }
}
