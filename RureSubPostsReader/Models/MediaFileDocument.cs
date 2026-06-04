using MongoDB.Bson.Serialization.Attributes;

namespace RureSubPostsReader.Models;

public class MediaFileDocument
{
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid PostId { get; set; }
    public string? Path { get; set; }
    public string? Type { get; set; }
}
