namespace RureSubPostsReader.Services;

public interface IFollowersService
{
    Task<bool[]> IsFollowed(Guid userId, Guid[] followingIds);
}