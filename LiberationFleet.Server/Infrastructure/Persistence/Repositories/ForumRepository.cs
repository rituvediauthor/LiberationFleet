using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ForumRepository : IForumRepository
{
    private readonly ApplicationDbContext _context;

    public ForumRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ForumPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ForumPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<ForumPost?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ForumPosts
            .Include(p => p.AuthorUser)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<ForumPost>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.ForumPosts
            .Include(p => p.AuthorUser)
            .Where(p => p.CrewId == crewId && !p.IsDeleted)
            .OrderByDescending(p => p.LastActivityAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ForumComment>> GetCommentsByPostIdAsync(
        int postId,
        CancellationToken cancellationToken = default) =>
        await _context.ForumComments
            .Include(c => c.AuthorUser)
            .Where(c => c.ForumPostId == postId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ForumComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _context.ForumComments
            .Include(c => c.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

    public async Task AddPostAsync(ForumPost post, CancellationToken cancellationToken = default) =>
        await _context.ForumPosts.AddAsync(post, cancellationToken);

    public async Task AddCommentAsync(ForumComment comment, CancellationToken cancellationToken = default) =>
        await _context.ForumComments.AddAsync(comment, cancellationToken);
}
