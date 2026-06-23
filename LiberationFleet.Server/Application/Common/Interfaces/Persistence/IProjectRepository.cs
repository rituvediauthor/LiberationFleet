using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IProjectRepository
{
    Task<ProjectPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProjectPost?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectPost>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectComment>> GetCommentsByPostIdAsync(int postId, CancellationToken cancellationToken = default);
    Task<ProjectComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task AddPostAsync(ProjectPost post, CancellationToken cancellationToken = default);
    Task AddCommentAsync(ProjectComment comment, CancellationToken cancellationToken = default);
}
