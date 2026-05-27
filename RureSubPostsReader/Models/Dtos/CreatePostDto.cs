namespace RureSubPostsReader.Models.Dtos;

public class CreatePostDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime PostedAt { get; set; }
}
