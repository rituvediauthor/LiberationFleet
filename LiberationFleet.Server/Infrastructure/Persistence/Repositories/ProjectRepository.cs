using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ProjectPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ProjectPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<ProjectPost?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ProjectPosts
            .Include(p => p.AuthorUser)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<ProjectPost>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.ProjectPosts
            .Include(p => p.AuthorUser)
            .Where(p => p.CrewId == crewId && !p.IsDeleted)
            .OrderByDescending(p => p.LastActivityAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectComment>> GetCommentsByPostIdAsync(
        int postId,
        CancellationToken cancellationToken = default) =>
        await _context.ProjectComments
            .Include(c => c.AuthorUser)
            .Where(c => c.ProjectPostId == postId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ProjectComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _context.ProjectComments
            .Include(c => c.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

    public async Task AddPostAsync(ProjectPost post, CancellationToken cancellationToken = default) =>
        await _context.ProjectPosts.AddAsync(post, cancellationToken);

    public async Task AddCommentAsync(ProjectComment comment, CancellationToken cancellationToken = default) =>
        await _context.ProjectComments.AddAsync(comment, cancellationToken);
}
