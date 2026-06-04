namespace RureSubPostsReader.Models.Dtos;

public class PostCreateRequestDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AuthorId { get; set; }
    public AuthorDocument? Author { get; set; }
    public string Title { get; set; } = string.Empty;
    public MediaFileCreateRequestDto[]? MediaFiles { get; set; }
    public string? Content { get; set; }
    public bool IsEdited { get; set; }
    public DateTime PostedAt { get; set; }
}
