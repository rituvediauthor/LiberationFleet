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

    public async Task<IReadOnlyList<ForumPost>> GetByFleetIdAsync(int fleetId, CancellationToken cancellationToken = default) =>
        await _context.ForumPosts
            .Include(p => p.AuthorUser)
            .Where(p => p.FleetId == fleetId && !p.IsDeleted)
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

    public async Task<IReadOnlyList<ForumComment>> GetRepliesByParentCommentIdAsync(
        int postId,
        int parentCommentId,
        CancellationToken cancellationToken = default) =>
        await _context.ForumComments
            .Include(c => c.AuthorUser)
            .Where(c => c.ForumPostId == postId
                && c.ParentCommentId == parentCommentId
                && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ForumComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _context.ForumComments
            .Include(c => c.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

    public async Task<Dictionary<int, int>> GetActiveLikeCountsForPostsAsync(
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.ForumLikes
            .Where(l => l.ForumPostId.HasValue
                && ids.Contains(l.ForumPostId.Value)
                && l.RemovedAt == null)
            .GroupBy(l => l.ForumPostId!.Value)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostId, x => x.Count, cancellationToken);
    }

    public async Task<HashSet<int>> GetActiveLikedPostIdsByUserAsync(
        int userId,
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var likedIds = await _context.ForumLikes
            .Where(l => l.UserId == userId
                && l.ForumPostId.HasValue
                && ids.Contains(l.ForumPostId.Value)
                && l.RemovedAt == null)
            .Select(l => l.ForumPostId!.Value)
            .ToListAsync(cancellationToken);

        return likedIds.ToHashSet();
    }

    public async Task<Dictionary<int, int>> GetActiveLikeCountsForCommentsAsync(
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.ForumLikes
            .Where(l => l.ForumCommentId.HasValue
                && ids.Contains(l.ForumCommentId.Value)
                && l.RemovedAt == null)
            .GroupBy(l => l.ForumCommentId!.Value)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count, cancellationToken);
    }

    public async Task<HashSet<int>> GetActiveLikedCommentIdsByUserAsync(
        int userId,
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var likedIds = await _context.ForumLikes
            .Where(l => l.UserId == userId
                && l.ForumCommentId.HasValue
                && ids.Contains(l.ForumCommentId.Value)
                && l.RemovedAt == null)
            .Select(l => l.ForumCommentId!.Value)
            .ToListAsync(cancellationToken);

        return likedIds.ToHashSet();
    }

    public async Task<Dictionary<int, int>> GetCommentCountsForPostsAsync(
        IEnumerable<int> postIds,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.ForumComments
            .Where(c => ids.Contains(c.ForumPostId) && !c.IsDeleted)
            .GroupBy(c => c.ForumPostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostId, x => x.Count, cancellationToken);
    }

    public Task<ForumLike?> GetPostLikeAsync(int userId, int postId, CancellationToken cancellationToken = default) =>
        _context.ForumLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ForumPostId == postId, cancellationToken);

    public Task<ForumLike?> GetCommentLikeAsync(int userId, int commentId, CancellationToken cancellationToken = default) =>
        _context.ForumLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ForumCommentId == commentId, cancellationToken);

    public async Task AddLikeAsync(ForumLike like, CancellationToken cancellationToken = default) =>
        await _context.ForumLikes.AddAsync(like, cancellationToken);

    public async Task AddPostAsync(ForumPost post, CancellationToken cancellationToken = default) =>
        await _context.ForumPosts.AddAsync(post, cancellationToken);

    public async Task AddCommentAsync(ForumComment comment, CancellationToken cancellationToken = default) =>
        await _context.ForumComments.AddAsync(comment, cancellationToken);
}
