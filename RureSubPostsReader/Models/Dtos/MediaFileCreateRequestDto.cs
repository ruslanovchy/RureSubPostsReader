namespace RureSubPostsReader.Models.Dtos;

public class MediaFileCreateRequestDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? Path { get; set; }
    public string? Type { get; set; }
}
