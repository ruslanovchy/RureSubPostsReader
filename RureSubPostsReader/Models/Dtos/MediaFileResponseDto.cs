namespace RureSubPostsReader.Models.Dtos;

public class MediaFileResponseDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? Src { get; set; }
    public string? Type { get; set; }
}
