using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public record ForumPostPage(IReadOnlyList<ForumPost> Items, bool HasMore);

public interface IForumRepository
{
    Task<ForumPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ForumPost?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumPost>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumPost>> GetByFleetIdAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<ForumPostPage> GetByCrewIdPageAsync(
        int crewId,
        int offset,
        int limit,
        bool excludeAdultContent,
        CancellationToken cancellationToken = default);
    Task<ForumPostPage> GetByFleetIdPageAsync(
        int fleetId,
        int offset,
        int limit,
        bool excludeAdultContent,
        IReadOnlyCollection<int>? excludeAuthorUserIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumComment>> GetCommentsByPostIdAsync(int postId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumComment>> GetRepliesByParentCommentIdAsync(
        int postId,
        int parentCommentId,
        CancellationToken cancellationToken = default);
    Task<ForumComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetActiveLikeCountsForPostsAsync(
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetActiveLikedPostIdsByUserAsync(
        int userId,
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetActiveLikeCountsForCommentsAsync(
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetActiveLikedCommentIdsByUserAsync(
        int userId,
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetCommentCountsForPostsAsync(
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default);
    Task<ForumLike?> GetPostLikeAsync(int userId, int postId, CancellationToken cancellationToken = default);
    Task<ForumLike?> GetCommentLikeAsync(int userId, int commentId, CancellationToken cancellationToken = default);
    Task AddLikeAsync(ForumLike like, CancellationToken cancellationToken = default);
    Task AddPostAsync(ForumPost post, CancellationToken cancellationToken = default);
    Task AddCommentAsync(ForumComment comment, CancellationToken cancellationToken = default);
}
