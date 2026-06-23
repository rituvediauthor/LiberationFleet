using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IForumRepository
{
    Task<ForumPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ForumPost?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumPost>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumComment>> GetCommentsByPostIdAsync(int postId, CancellationToken cancellationToken = default);
    Task<ForumComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task AddPostAsync(ForumPost post, CancellationToken cancellationToken = default);
    Task AddCommentAsync(ForumComment comment, CancellationToken cancellationToken = default);
}
