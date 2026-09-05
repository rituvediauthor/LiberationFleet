using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewCleanupRepository(ApplicationDbContext context) : ICrewCleanupRepository
{
    public async Task CleanupCrewExceptGiftsAsync(int crewId, CancellationToken cancellationToken = default)
    {
        await DetachCrewFromFleetAsync(crewId, cancellationToken);

        var chatRoomIds = await context.ChatRooms
            .Where(r => r.CrewId == crewId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var forumPostIds = await context.ForumPosts
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

        // Load + RemoveRange (not ExecuteDelete) so InMemory EF used in tests works too.
        await DeleteMatchingAsync(context.Notifications.Where(n => n.CrewId == crewId), cancellationToken);

        await DeleteMatchingAsync(
            context.EncryptedContentEnvelopes.Where(e =>
                e.CrewId == crewId
                && e.ContentType != EncryptedContentType.GiftLogEntry
                && e.ContentType != EncryptedContentType.ProfileAvatar),
            cancellationToken);

        await DeleteMatchingAsync(context.CrewKeyDistributions.Where(d => d.CrewId == crewId), cancellationToken);

        // Gifts are retained for investigation but Restrict-FK to cycles/thresholds.
        // Clear those links before deleting season rows so sole-member leave can succeed.
        var giftsWithSeasonLinks = await context.Gifts
            .Where(g => g.CrewId == crewId
                && (g.SeasonCycleId != null || g.MonthlySurvivalThresholdId != null))
            .ToListAsync(cancellationToken);
        foreach (var gift in giftsWithSeasonLinks)
        {
            gift.SeasonCycleId = null;
            gift.MonthlySurvivalThresholdId = null;
        }

        if (giftsWithSeasonLinks.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await DeleteMatchingAsync(context.MonthlySurvivalThresholds.Where(t => t.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.SeasonCycles.Where(c => c.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.CrewRules.Where(r => r.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.ChatRoomMessages.Where(m => m.ChatRoom.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.ChatRooms.Where(r => r.CrewId == crewId), cancellationToken);

        await ClearParentThenDeleteAsync(
            context.ProposalComments.Where(c => c.Proposal.CrewId == crewId),
            c => c.ParentCommentId = null,
            cancellationToken);

        await ClearParentThenDeleteAsync(
            context.ForumComments.Where(c => c.ForumPost.CrewId == crewId),
            c => c.ParentCommentId = null,
            cancellationToken);

        await DeleteMatchingAsync(context.ForumPosts.Where(p => p.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.Proposals.Where(p => p.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(
            context.LibraryRequestMessages.Where(m => m.Request.Unit.Offering.CrewId == crewId),
            cancellationToken);
        await DeleteMatchingAsync(
            context.LibraryRequests.Where(r => r.Unit.Offering.CrewId == crewId),
            cancellationToken);
        await DeleteMatchingAsync(
            context.LibraryMaintenanceRecords.Where(m => m.Unit.Offering.CrewId == crewId),
            cancellationToken);
        await DeleteMatchingAsync(
            context.LibraryUnits.Where(u => u.Offering.CrewId == crewId),
            cancellationToken);
        await DeleteMatchingAsync(
            context.LibraryOfferingCategories.Where(c => c.Offering.CrewId == crewId),
            cancellationToken);
        await DeleteMatchingAsync(context.LibraryOfferings.Where(o => o.CrewId == crewId), cancellationToken);
        await DeleteMatchingAsync(context.CrewMemberships.Where(m => m.CrewId == crewId), cancellationToken);

        var hasGifts = await context.Gifts.AnyAsync(g => g.CrewId == crewId, cancellationToken);
        if (!hasGifts)
        {
            // Detach any leftover user handles before deleting the crew platform catalog.
            var platformIds = await context.CrewPaymentPlatforms
                .Where(p => p.CrewId == crewId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            if (platformIds.Count > 0)
            {
                var lingeringAccounts = await context.UserPaymentPlatforms
                    .Where(a => a.CrewPaymentPlatformId != null && platformIds.Contains(a.CrewPaymentPlatformId.Value))
                    .ToListAsync(cancellationToken);
                foreach (var account in lingeringAccounts)
                {
                    if (string.IsNullOrWhiteSpace(account.PlatformName))
                    {
                        account.PlatformName = "Unknown";
                    }

                    account.CrewPaymentPlatformId = null;
                }

                if (lingeringAccounts.Count > 0)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            await DeleteMatchingAsync(context.CrewPaymentPlatforms.Where(p => p.CrewId == crewId), cancellationToken);
            await DeleteMatchingAsync(context.Crews.Where(c => c.Id == crewId), cancellationToken);
        }
    }

    /// <summary>
    /// Removes the crew from any fleet and soft-deletes its linked fleet chat room
    /// (clearing LinkedCrewId so Restrict FK does not block crew deletion).
    /// </summary>
    private async Task DetachCrewFromFleetAsync(int crewId, CancellationToken cancellationToken)
    {
        var linkedRooms = await context.ChatRooms
            .Where(r => r.LinkedCrewId == crewId)
            .ToListAsync(cancellationToken);
        foreach (var room in linkedRooms)
        {
            room.IsDeleted = true;
            room.LinkedCrewId = null;
        }

        await DeleteMatchingAsync(context.FleetCrews.Where(fc => fc.CrewId == crewId), cancellationToken);

        if (linkedRooms.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
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

        await DeleteMatchingAsync(
            context.UserMutedContents.Where(m => m.ContentType == contentType && resourceIds.Contains(m.ResourceId)),
            cancellationToken);

        await DeleteMatchingAsync(
            context.UserHiddenContents.Where(h => h.ContentType == contentType && resourceIds.Contains(h.ResourceId)),
            cancellationToken);
    }

    private async Task DeleteMatchingAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        where T : class
    {
        var items = await query.ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return;
        }

        context.RemoveRange(items);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearParentThenDeleteAsync<T>(
        IQueryable<T> query,
        Action<T> clearParent,
        CancellationToken cancellationToken)
        where T : class
    {
        var items = await query.ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            clearParent(item);
        }

        await context.SaveChangesAsync(cancellationToken);
        context.RemoveRange(items);
        await context.SaveChangesAsync(cancellationToken);
    }
}
