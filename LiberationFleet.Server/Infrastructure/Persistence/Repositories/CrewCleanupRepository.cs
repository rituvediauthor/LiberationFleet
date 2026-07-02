using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewCleanupRepository(ApplicationDbContext context) : ICrewCleanupRepository
{
    public async Task CleanupCrewExceptGiftsAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var chatRoomIds = await context.ChatRooms
            .Where(r => r.CrewId == crewId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var forumPostIds = await context.ForumPosts
            .Where(p => p.CrewId == crewId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var projectPostIds = await context.ProjectPosts
            .Where(p => p.CrewId == crewId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        await RemoveMutedAndHiddenAsync(
            MutedContentType.ChatRoom,
            chatRoomIds,
            cancellationToken);
        await RemoveMutedAndHiddenAsync(
            MutedContentType.Forum,
            forumPostIds,
            cancellationToken);
        await RemoveMutedAndHiddenAsync(
            MutedContentType.Project,
            projectPostIds,
            cancellationToken);

        await context.Notifications
            .Where(n => n.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.EncryptedContentEnvelopes
            .Where(e => e.CrewId == crewId && e.ContentType != EncryptedContentType.GiftLogEntry)
            .ExecuteDeleteAsync(cancellationToken);

        await context.CrewKeyDistributions
            .Where(d => d.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.SeasonCycles
            .Where(c => c.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.CrewRules
            .Where(r => r.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ChatRoomMessages
            .Where(m => m.ChatRoom.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ChatRooms
            .Where(r => r.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ProposalComments
            .Where(c => c.Proposal.CrewId == crewId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null), cancellationToken);
        await context.ProposalComments
            .Where(c => c.Proposal.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ForumComments
            .Where(c => c.ForumPost.CrewId == crewId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null), cancellationToken);
        await context.ForumComments
            .Where(c => c.ForumPost.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ForumPosts
            .Where(p => p.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ProjectComments
            .Where(c => c.ProjectPost.CrewId == crewId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null), cancellationToken);
        await context.ProjectComments
            .Where(c => c.ProjectPost.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.ProjectPosts
            .Where(p => p.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Proposals
            .Where(p => p.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryRequestMessages
            .Where(m => m.Request.Unit.Offering.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryRequests
            .Where(r => r.Unit.Offering.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryMaintenanceRecords
            .Where(m => m.Unit.Offering.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryUnits
            .Where(u => u.Offering.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryOfferingCategories
            .Where(c => c.Offering.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.LibraryOfferings
            .Where(o => o.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.CrewMemberships
            .Where(m => m.CrewId == crewId)
            .ExecuteDeleteAsync(cancellationToken);

        var hasGifts = await context.Gifts.AnyAsync(g => g.CrewId == crewId, cancellationToken);
        if (!hasGifts)
        {
            await context.CrewPaymentPlatforms
                .Where(p => p.CrewId == crewId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.Crews
                .Where(c => c.Id == crewId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task RemoveMutedAndHiddenAsync(
        MutedContentType contentType,
        IReadOnlyList<int> resourceIds,
        CancellationToken cancellationToken)
    {
        if (resourceIds.Count == 0)
        {
            return;
        }

        await context.UserMutedContents
            .Where(m => m.ContentType == contentType && resourceIds.Contains(m.ResourceId))
            .ExecuteDeleteAsync(cancellationToken);

        await context.UserHiddenContents
            .Where(h => h.ContentType == contentType && resourceIds.Contains(h.ResourceId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
