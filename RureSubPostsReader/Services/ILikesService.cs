namespace RureSubPostsReader.Services;

public interface ILikesService
{
    Task<bool[]> IsPostsLiked(Guid userId, Guid[] postIds);
    Task<Guid[]> GetUserLikes(Guid userId, int pageSize, int page);
}