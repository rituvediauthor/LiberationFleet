using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ContentMentionRepository(ApplicationDbContext context) : IContentMentionRepository
{
    public async Task<IReadOnlyList<int>> GetMentionedUserIdsAsync(
        MentionedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default)
    {
        return await context.ContentMentions
            .Where(m => m.ContentType == contentType && m.ResourceId == resourceId)
            .Select(m => m.MentionedUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ContentMention> mentions, CancellationToken cancellationToken = default)
    {
        await context.ContentMentions.AddRangeAsync(mentions, cancellationToken);
    }

    public async Task DeleteByContentAsync(
        MentionedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.ContentMentions
            .Where(m => m.ContentType == contentType && m.ResourceId == resourceId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            context.ContentMentions.RemoveRange(existing);
        }
    }
}
