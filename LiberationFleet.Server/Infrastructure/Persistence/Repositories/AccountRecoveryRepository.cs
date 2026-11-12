using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class AccountRecoveryRepository(ApplicationDbContext context) : IAccountRecoveryRepository
{
    public async Task TransferEncryptionIdentityAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        var oldCrewWraps = await context.CrewKeyDistributions
            .Where(d => d.UserId == oldUserId)
            .ToListAsync(cancellationToken);
        context.CrewKeyDistributions.RemoveRange(oldCrewWraps);

        var oldFleetWraps = await context.FleetKeyDistributions
            .Where(d => d.UserId == oldUserId)
            .ToListAsync(cancellationToken);
        context.FleetKeyDistributions.RemoveRange(oldFleetWraps);

        var newCrewWraps = await context.CrewKeyDistributions
            .Where(d => d.UserId == newUserId)
            .ToListAsync(cancellationToken);
        foreach (var wrap in newCrewWraps)
        {
            wrap.UserId = oldUserId;
        }

        var newFleetWraps = await context.FleetKeyDistributions
            .Where(d => d.UserId == newUserId)
            .ToListAsync(cancellationToken);
        foreach (var wrap in newFleetWraps)
        {
            wrap.UserId = oldUserId;
        }

        await context.SaveChangesAsync(cancellationToken);

        var newBundle = await context.UserKeyBundles
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == newUserId, cancellationToken);
        var oldBundle = await context.UserKeyBundles
            .FirstOrDefaultAsync(b => b.UserId == oldUserId, cancellationToken);
        if (newBundle is not null)
        {
            if (oldBundle is null)
            {
                context.UserKeyBundles.Add(new Domain.Entities.UserKeyBundle
                {
                    UserId = oldUserId,
                    IdentityPublicKey = newBundle.IdentityPublicKey,
                    KeyVersion = newBundle.KeyVersion,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                oldBundle.IdentityPublicKey = newBundle.IdentityPublicKey;
                oldBundle.KeyVersion = newBundle.KeyVersion;
                oldBundle.UpdatedAt = DateTime.UtcNow;
            }
        }

        var newBackup = await context.UserPrivateKeyBackups
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == newUserId, cancellationToken);
        var oldBackup = await context.UserPrivateKeyBackups
            .FirstOrDefaultAsync(b => b.UserId == oldUserId, cancellationToken);
        if (newBackup is not null)
        {
            if (oldBackup is null)
            {
                context.UserPrivateKeyBackups.Add(new Domain.Entities.UserPrivateKeyBackup
                {
                    UserId = oldUserId,
                    Salt = newBackup.Salt,
                    Iv = newBackup.Iv,
                    Ciphertext = newBackup.Ciphertext,
                    KeyVersion = newBackup.KeyVersion,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                oldBackup.Salt = newBackup.Salt;
                oldBackup.Iv = newBackup.Iv;
                oldBackup.Ciphertext = newBackup.Ciphertext;
                oldBackup.KeyVersion = newBackup.KeyVersion;
                oldBackup.UpdatedAt = DateTime.UtcNow;
            }
        }

        var discardBundles = await context.UserKeyBundles
            .Where(b => b.UserId == newUserId)
            .ToListAsync(cancellationToken);
        context.UserKeyBundles.RemoveRange(discardBundles);

        var discardBackups = await context.UserPrivateKeyBackups
            .Where(b => b.UserId == newUserId)
            .ToListAsync(cancellationToken);
        context.UserPrivateKeyBackups.RemoveRange(discardBackups);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DropUndecryptablePersonalContentAsync(
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        // Keep DMs and personal DM media — the other party can still decrypt their copies
        // and re-share ciphertext when they next open the thread.
        var avatars = await context.EncryptedContentEnvelopes
            .Where(e => e.ContentType == EncryptedContentType.ProfileAvatar
                && e.AuthorUserId == oldUserId
                && e.CrewId == null
                && e.FleetId == null)
            .ToListAsync(cancellationToken);
        context.EncryptedContentEnvelopes.RemoveRange(avatars);

        var oldUser = await context.Users.FirstOrDefaultAsync(u => u.Id == oldUserId, cancellationToken);
        if (oldUser is not null)
        {
            oldUser.AvatarResourceId = null;
            oldUser.LocationNonce = null;
            oldUser.LocationCiphertext = null;
            oldUser.LocationKeyVersion = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignAuthoredContentAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        foreach (var envelope in await context.EncryptedContentEnvelopes
                     .Where(e => e.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            envelope.AuthorUserId = oldUserId;
        }

        foreach (var envelope in await context.EncryptedContentEnvelopes
                     .Where(e => e.RecipientUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            envelope.RecipientUserId = oldUserId;
        }

        foreach (var message in await context.ChatRoomMessages
                     .Where(m => m.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            message.AuthorUserId = oldUserId;
        }

        foreach (var post in await context.ForumPosts
                     .Where(p => p.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            post.AuthorUserId = oldUserId;
        }

        foreach (var comment in await context.ForumComments
                     .Where(c => c.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            comment.AuthorUserId = oldUserId;
        }

        foreach (var proposal in await context.Proposals
                     .Where(p => p.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            proposal.AuthorUserId = oldUserId;
        }

        foreach (var comment in await context.ProposalComments
                     .Where(c => c.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            comment.AuthorUserId = oldUserId;
        }

        foreach (var comment in await context.GiftComments
                     .Where(c => c.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            comment.AuthorUserId = oldUserId;
        }

        await DedupThenReassignAsync(
            context.GiftLikes.Where(l => l.UserId == newUserId || l.UserId == oldUserId),
            l => (l.GiftId, l.GiftCommentId),
            l => l.UserId,
            (l, id) => l.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await DedupThenReassignAsync(
            context.ForumLikes.Where(l => l.UserId == newUserId || l.UserId == oldUserId),
            l => (l.ForumPostId, l.ForumCommentId),
            l => l.UserId,
            (l, id) => l.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await DedupThenReassignAsync(
            context.ChatMessageLikes.Where(l => l.UserId == newUserId || l.UserId == oldUserId),
            l => l.ChatRoomMessageId,
            l => l.UserId,
            (l, id) => l.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await DedupThenReassignAsync(
            context.ProposalVotes.Where(v => v.UserId == newUserId || v.UserId == oldUserId),
            v => v.ProposalId,
            v => v.UserId,
            (v, id) => v.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        foreach (var mention in await context.ContentMentions
                     .Where(m => m.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            mention.AuthorUserId = oldUserId;
        }

        foreach (var mention in await context.ContentMentions
                     .Where(m => m.MentionedUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            mention.MentionedUserId = oldUserId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignGiftRolesAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        var gifts = await context.Gifts
            .Where(g => g.GiverUserId == newUserId
                || g.RecipientUserId == newUserId
                || g.MiddlemanUserId == newUserId
                || g.ImpersonatedByUserId == newUserId
                || g.VerifiedByUserId == newUserId)
            .ToListAsync(cancellationToken);

        foreach (var gift in gifts)
        {
            if (gift.GiverUserId == newUserId)
            {
                gift.GiverUserId = oldUserId;
            }

            if (gift.RecipientUserId == newUserId)
            {
                gift.RecipientUserId = oldUserId;
            }

            if (gift.MiddlemanUserId == newUserId)
            {
                gift.MiddlemanUserId = oldUserId;
            }

            if (gift.ImpersonatedByUserId == newUserId)
            {
                gift.ImpersonatedByUserId = oldUserId;
            }

            if (gift.VerifiedByUserId == newUserId)
            {
                gift.VerifiedByUserId = oldUserId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MergeDirectMessagesAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        // Preserve all DM rows. Remap the recovering account onto the surviving user id;
        // when both already had a thread with the same friend, fold messages into one conversation.
        var newConvos = await context.DirectConversations
            .Where(c => c.UserLowId == newUserId || c.UserHighId == newUserId)
            .ToListAsync(cancellationToken);

        foreach (var convo in newConvos)
        {
            var other = convo.UserLowId == newUserId ? convo.UserHighId : convo.UserLowId;
            if (other == oldUserId)
            {
                // Thread was only between the two accounts being merged — drop it.
                var selfMessages = await context.DirectMessages
                    .Where(m => m.ConversationId == convo.Id)
                    .ToListAsync(cancellationToken);
                context.DirectMessages.RemoveRange(selfMessages);
                context.DirectConversations.Remove(convo);
                continue;
            }

            var low = Math.Min(oldUserId, other);
            var high = Math.Max(oldUserId, other);
            var existing = await context.DirectConversations
                .FirstOrDefaultAsync(c => c.UserLowId == low && c.UserHighId == high, cancellationToken);
            if (existing is not null && existing.Id != convo.Id)
            {
                var messages = await context.DirectMessages
                    .Where(m => m.ConversationId == convo.Id)
                    .ToListAsync(cancellationToken);
                foreach (var message in messages)
                {
                    message.ConversationId = existing.Id;
                }

                context.DirectConversations.Remove(convo);
            }
            else
            {
                convo.UserLowId = low;
                convo.UserHighId = high;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var message in await context.DirectMessages
                     .Where(m => m.AuthorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            message.AuthorUserId = oldUserId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MergeFriendshipsAndBlocksAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        var pairLow = Math.Min(newUserId, oldUserId);
        var pairHigh = Math.Max(newUserId, oldUserId);
        var between = await context.Friendships
            .Where(f => f.UserLowId == pairLow && f.UserHighId == pairHigh)
            .ToListAsync(cancellationToken);
        context.Friendships.RemoveRange(between);

        var newFriendships = await context.Friendships
            .Where(f => f.RequesterUserId == newUserId || f.AddresseeUserId == newUserId)
            .ToListAsync(cancellationToken);

        foreach (var friendship in newFriendships)
        {
            if (friendship.RequesterUserId == newUserId)
            {
                friendship.RequesterUserId = oldUserId;
            }

            if (friendship.AddresseeUserId == newUserId)
            {
                friendship.AddresseeUserId = oldUserId;
            }

            friendship.UserLowId = Math.Min(friendship.RequesterUserId, friendship.AddresseeUserId);
            friendship.UserHighId = Math.Max(friendship.RequesterUserId, friendship.AddresseeUserId);

            var duplicate = await context.Friendships
                .AnyAsync(
                    f => f.Id != friendship.Id
                        && f.UserLowId == friendship.UserLowId
                        && f.UserHighId == friendship.UserHighId,
                    cancellationToken);
            if (duplicate || friendship.RequesterUserId == friendship.AddresseeUserId)
            {
                context.Friendships.Remove(friendship);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var mutualBlocks = await context.UserBlocks
            .Where(b => (b.BlockerUserId == newUserId && b.BlockedUserId == oldUserId)
                || (b.BlockerUserId == oldUserId && b.BlockedUserId == newUserId))
            .ToListAsync(cancellationToken);
        context.UserBlocks.RemoveRange(mutualBlocks);

        var newBlocks = await context.UserBlocks
            .Where(b => b.BlockerUserId == newUserId || b.BlockedUserId == newUserId)
            .ToListAsync(cancellationToken);
        foreach (var block in newBlocks)
        {
            if (block.BlockerUserId == newUserId)
            {
                block.BlockerUserId = oldUserId;
            }

            if (block.BlockedUserId == newUserId)
            {
                block.BlockedUserId = oldUserId;
            }

            var duplicate = await context.UserBlocks
                .AnyAsync(
                    b => b.Id != block.Id
                        && b.BlockerUserId == block.BlockerUserId
                        && b.BlockedUserId == block.BlockedUserId,
                    cancellationToken);
            if (duplicate || block.BlockerUserId == block.BlockedUserId)
            {
                context.UserBlocks.Remove(block);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignLibraryOwnershipAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        foreach (var offering in await context.LibraryOfferings
                     .Where(o => o.CreatorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            offering.CreatorUserId = oldUserId;
        }

        foreach (var unit in await context.LibraryUnits
                     .Where(u => u.CurrentPossessorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            unit.CurrentPossessorUserId = oldUserId;
        }

        foreach (var request in await context.LibraryRequests
                     .Where(r => r.RequesterUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            request.RequesterUserId = oldUserId;
        }

        await DedupThenReassignAsync(
            context.UserLibraryFavorites.Where(f => f.UserId == newUserId || f.UserId == oldUserId),
            f => f.LibraryUnitId,
            f => f.UserId,
            (f, id) => f.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignNotificationsAndPrefsAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        foreach (var notification in await context.Notifications
                     .Where(n => n.UserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            notification.UserId = oldUserId;
        }

        foreach (var notification in await context.Notifications
                     .Where(n => n.ActorUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            notification.ActorUserId = oldUserId;
        }

        await DedupThenReassignAsync(
            context.UserNotificationPreferences.Where(p => p.UserId == newUserId || p.UserId == oldUserId),
            p => p.Kind,
            p => p.UserId,
            (p, id) => p.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await DedupThenReassignAsync(
            context.UserMutedContents.Where(m => m.UserId == newUserId || m.UserId == oldUserId),
            m => (m.ContentType, m.ResourceId),
            m => m.UserId,
            (m, id) => m.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await DedupThenReassignAsync(
            context.UserHiddenContents.Where(h => h.UserId == newUserId || h.UserId == oldUserId),
            h => (h.ContentType, h.ResourceId),
            h => h.UserId,
            (h, id) => h.UserId = id,
            newUserId,
            oldUserId,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignMiscOwnershipAsync(
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        foreach (var request in await context.EmergencyRequests
                     .Where(r => r.RequesterUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            request.RequesterUserId = oldUserId;
        }

        foreach (var rule in await context.CrewRules
                     .Where(r => r.CreatedByUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            rule.CreatedByUserId = oldUserId;
        }

        foreach (var rule in await context.FleetRules
                     .Where(r => r.CreatedByUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            rule.CreatedByUserId = oldUserId;
        }

        foreach (var room in await context.ChatRooms
                     .Where(r => r.CreatedByUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            room.CreatedByUserId = oldUserId;
        }

        foreach (var report in await context.ContentReports
                     .Where(r => r.ReporterUserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            report.ReporterUserId = oldUserId;
        }

        foreach (var donation in await context.AppDonations
                     .Where(d => d.UserId == newUserId)
                     .ToListAsync(cancellationToken))
        {
            donation.UserId = oldUserId;
        }

        context.DevicePushTokens.RemoveRange(
            await context.DevicePushTokens.Where(t => t.UserId == newUserId).ToListAsync(cancellationToken));
        context.UserRegisteredDevices.RemoveRange(
            await context.UserRegisteredDevices.Where(d => d.UserId == newUserId).ToListAsync(cancellationToken));
        context.EmailMfaChallenges.RemoveRange(
            await context.EmailMfaChallenges.Where(c => c.UserId == newUserId).ToListAsync(cancellationToken));
        context.PasswordResetTokens.RemoveRange(
            await context.PasswordResetTokens.Where(t => t.UserId == newUserId).ToListAsync(cancellationToken));

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task DedupThenReassignAsync<TEntity, TKey>(
        IQueryable<TEntity> query,
        Func<TEntity, TKey> keySelector,
        Func<TEntity, int> userIdSelector,
        Action<TEntity, int> setUserId,
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var rows = await query.ToListAsync(cancellationToken);
        var oldKeys = rows
            .Where(r => userIdSelector(r) == oldUserId)
            .Select(keySelector)
            .ToHashSet();

        foreach (var row in rows.Where(r => userIdSelector(r) == newUserId).ToList())
        {
            if (oldKeys.Contains(keySelector(row)))
            {
                context.Remove(row);
            }
            else
            {
                setUserId(row, oldUserId);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
