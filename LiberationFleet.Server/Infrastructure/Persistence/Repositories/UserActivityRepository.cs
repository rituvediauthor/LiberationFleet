using System.Globalization;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class UserActivityRepository(ApplicationDbContext context) : IUserActivityRepository
{
    private const int PerSourceFetchLimit = 40;

    public async Task<IReadOnlyList<UserActivityRecord>> GetUserActivitiesAsync(
        int userId,
        UserActivityFilterCategory category,
        DateTime? beforeCreatedAt,
        string? beforeKey,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var fetchLimit = Math.Clamp(limit, 1, 100);
        var sourceLimit = category == UserActivityFilterCategory.All
            ? Math.Max(fetchLimit, PerSourceFetchLimit)
            : fetchLimit * 2;

        var records = new List<UserActivityRecord>();

        if (category is UserActivityFilterCategory.All or UserActivityFilterCategory.Chats)
        {
            records.AddRange(await GetChatActivitiesAsync(userId, beforeCreatedAt, sourceLimit, cancellationToken));
        }

        if (category is UserActivityFilterCategory.All or UserActivityFilterCategory.Forums)
        {
            records.AddRange(await GetForumActivitiesAsync(userId, beforeCreatedAt, sourceLimit, cancellationToken));
        }

        if (category is UserActivityFilterCategory.All or UserActivityFilterCategory.Library)
        {
            records.AddRange(await GetLibraryActivitiesAsync(userId, beforeCreatedAt, sourceLimit, cancellationToken));
        }

        if (category is UserActivityFilterCategory.All or UserActivityFilterCategory.Gifts)
        {
            records.AddRange(await GetGiftActivitiesAsync(userId, beforeCreatedAt, sourceLimit, cancellationToken));
        }

        if (category is UserActivityFilterCategory.All or UserActivityFilterCategory.Proposals)
        {
            records.AddRange(await GetProposalActivitiesAsync(userId, beforeCreatedAt, sourceLimit, cancellationToken));
        }

        return records
            .Where(record => IsBeforeCursor(record, beforeCreatedAt, beforeKey))
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Key)
            .Take(fetchLimit + 1)
            .ToList();
    }

    private static bool IsBeforeCursor(UserActivityRecord record, DateTime? beforeCreatedAt, string? beforeKey)
    {
        if (!beforeCreatedAt.HasValue)
        {
            return true;
        }

        if (record.CreatedAt < beforeCreatedAt.Value)
        {
            return true;
        }

        if (record.CreatedAt > beforeCreatedAt.Value)
        {
            return false;
        }

        return string.IsNullOrEmpty(beforeKey) || string.CompareOrdinal(record.Key, beforeKey) < 0;
    }

    private async Task<IReadOnlyList<UserActivityRecord>> GetChatActivitiesAsync(
        int userId,
        DateTime? beforeCreatedAt,
        int limit,
        CancellationToken cancellationToken)
    {
        var roomsQuery = context.ChatRooms
            .AsNoTracking()
            .Where(room => room.CreatedByUserId == userId && !room.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            roomsQuery = roomsQuery.Where(room => room.CreatedAt <= beforeCreatedAt.Value);
        }

        var rooms = await roomsQuery
            .OrderByDescending(room => room.CreatedAt)
            .Take(limit)
            .Select(room => new UserActivityRecord
            {
                Key = $"chat-room:{room.Id}",
                Kind = UserActivityKind.ChatRoom,
                Category = UserActivityFilterCategory.Chats,
                Label = "Created chat room",
                Detail = room.RoomType == ChatRoomType.Voice ? "Voice chat" : "Text chat",
                PlaintextPreview = Truncate(string.IsNullOrWhiteSpace(room.Purpose) ? room.Name : room.Purpose, 120),
                CreatedAt = room.CreatedAt,
                CrewId = room.CrewId!.Value,
                ResourceId = room.Id,
                ChatRoomType = room.RoomType,
                ResourceExists = true
            })
            .ToListAsync(cancellationToken);

        var messagesQuery = context.ChatRoomMessages
            .AsNoTracking()
            .Where(message => message.AuthorUserId == userId && !message.IsDeleted)
            .Join(
                context.ChatRooms.AsNoTracking(),
                message => message.ChatRoomId,
                room => room.Id,
                (message, room) => new { message, room })
            .Where(pair => !pair.room.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            messagesQuery = messagesQuery.Where(pair => pair.message.CreatedAt <= beforeCreatedAt.Value);
        }

        var messages = await messagesQuery
            .OrderByDescending(pair => pair.message.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"chat-message:{pair.message.Id}",
                Kind = UserActivityKind.ChatMessage,
                Category = UserActivityFilterCategory.Chats,
                Label = "Chat message",
                Detail = $"In {Truncate(pair.room.Purpose, 80)}",
                PreviewContentType = EncryptedContentType.ChatRoomMessage,
                CreatedAt = pair.message.CreatedAt,
                CrewId = pair.room.CrewId ?? 0,
                ResourceId = pair.message.Id,
                ParentResourceId = pair.room.Id,
                ChatRoomType = pair.room.RoomType,
                ResourceExists = true
            })
            .ToListAsync(cancellationToken);

        return rooms.Concat(messages).ToList();
    }

    private async Task<IReadOnlyList<UserActivityRecord>> GetForumActivitiesAsync(
        int userId,
        DateTime? beforeCreatedAt,
        int limit,
        CancellationToken cancellationToken)
    {
        var postsQuery = context.ForumPosts
            .AsNoTracking()
            .Where(post => post.AuthorUserId == userId && !post.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            postsQuery = postsQuery.Where(post => post.CreatedAt <= beforeCreatedAt.Value);
        }

        var posts = await postsQuery
            .OrderByDescending(post => post.CreatedAt)
            .Take(limit)
            .Select(post => new UserActivityRecord
            {
                Key = $"forum-post:{post.Id}",
                Kind = UserActivityKind.ForumPost,
                Category = UserActivityFilterCategory.Forums,
                Label = "Forum post",
                PreviewContentType = EncryptedContentType.ForumPost,
                CreatedAt = post.CreatedAt,
                CrewId = post.CrewId,
                ResourceId = post.Id,
                ResourceExists = true
            })
            .ToListAsync(cancellationToken);

        var commentsQuery = context.ForumComments
            .AsNoTracking()
            .Where(comment => comment.AuthorUserId == userId && !comment.IsDeleted)
            .Join(
                context.ForumPosts.AsNoTracking(),
                comment => comment.ForumPostId,
                post => post.Id,
                (comment, post) => new { comment, post })
            .Where(pair => !pair.post.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            commentsQuery = commentsQuery.Where(pair => pair.comment.CreatedAt <= beforeCreatedAt.Value);
        }

        var comments = await commentsQuery
            .OrderByDescending(pair => pair.comment.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"forum-comment:{pair.comment.Id}",
                Kind = UserActivityKind.ForumComment,
                Category = UserActivityFilterCategory.Forums,
                Label = pair.comment.ParentCommentId.HasValue ? "Forum reply" : "Forum comment",
                PreviewContentType = EncryptedContentType.ForumComment,
                CreatedAt = pair.comment.CreatedAt,
                CrewId = pair.post.CrewId,
                ResourceId = pair.comment.Id,
                ParentResourceId = pair.post.Id,
                ResourceExists = true
            })
            .ToListAsync(cancellationToken);

        return posts.Concat(comments).ToList();
    }

    private async Task<IReadOnlyList<UserActivityRecord>> GetLibraryActivitiesAsync(
        int userId,
        DateTime? beforeCreatedAt,
        int limit,
        CancellationToken cancellationToken)
    {
        var offeringsQuery = context.LibraryOfferings
            .AsNoTracking()
            .Where(offering => offering.CreatorUserId == userId && !offering.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            offeringsQuery = offeringsQuery.Where(offering => offering.CreatedAt <= beforeCreatedAt.Value);
        }

        var offerings = await offeringsQuery
            .OrderByDescending(offering => offering.CreatedAt)
            .Take(limit)
            .Select(offering => new
            {
                offering.Id,
                offering.CrewId,
                offering.Title,
                offering.CreatedAt,
                offering.ThumbnailResourceId,
                offering.HasEncryptedContent,
                UnitId = offering.Units
                    .Where(unit => !unit.IsRetired)
                    .OrderBy(unit => unit.Id)
                    .Select(unit => (int?)unit.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var offeringRecords = offerings.Select(offering => new UserActivityRecord
        {
            Key = $"library-offering:{offering.Id}",
            Kind = UserActivityKind.LibraryOffering,
            Category = UserActivityFilterCategory.Library,
            Label = "Library offering",
            PlaintextPreview = Truncate(offering.Title, 120),
            ThumbnailResourceId = offering.ThumbnailResourceId,
            PreviewContentType = offering.HasEncryptedContent ? EncryptedContentType.LibraryItem : null,
            CreatedAt = offering.CreatedAt,
            CrewId = offering.CrewId,
            ResourceId = offering.Id,
            LibraryUnitId = offering.UnitId,
            ResourceExists = true
        }).ToList();

        var requestsQuery = context.LibraryRequests
            .AsNoTracking()
            .Where(request => request.RequesterUserId == userId)
            .Join(
                context.LibraryUnits.AsNoTracking(),
                request => request.UnitId,
                unit => unit.Id,
                (request, unit) => new { request, unit })
            .Join(
                context.LibraryOfferings.AsNoTracking(),
                pair => pair.unit.OfferingId,
                offering => offering.Id,
                (pair, offering) => new { pair.request, pair.unit, offering });

        if (beforeCreatedAt.HasValue)
        {
            requestsQuery = requestsQuery.Where(pair => pair.request.CreatedAt <= beforeCreatedAt.Value);
        }

        var requests = await requestsQuery
            .OrderByDescending(pair => pair.request.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"library-request:{pair.request.Id}",
                Kind = UserActivityKind.LibraryRequest,
                Category = UserActivityFilterCategory.Library,
                Label = "Library request",
                Detail = Truncate(pair.offering.Title, 80),
                PlaintextPreview = Truncate(pair.request.PurposePreview, 120),
                CreatedAt = pair.request.CreatedAt,
                CrewId = pair.offering.CrewId,
                ResourceId = pair.request.Id,
                LibraryUnitId = pair.unit.Id,
                ResourceExists = !pair.offering.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var messagesQuery = context.LibraryRequestMessages
            .AsNoTracking()
            .Where(message => message.AuthorUserId == userId)
            .Join(
                context.LibraryRequests.AsNoTracking(),
                message => message.RequestId,
                request => request.Id,
                (message, request) => new { message, request })
            .Join(
                context.LibraryUnits.AsNoTracking(),
                pair => pair.request.UnitId,
                unit => unit.Id,
                (pair, unit) => new { pair.message, pair.request, unit })
            .Join(
                context.LibraryOfferings.AsNoTracking(),
                pair => pair.unit.OfferingId,
                offering => offering.Id,
                (pair, offering) => new { pair.message, pair.request, pair.unit, offering });

        if (beforeCreatedAt.HasValue)
        {
            messagesQuery = messagesQuery.Where(pair => pair.message.CreatedAt <= beforeCreatedAt.Value);
        }

        var messages = await messagesQuery
            .OrderByDescending(pair => pair.message.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"library-request-message:{pair.message.Id}",
                Kind = UserActivityKind.LibraryRequestMessage,
                Category = UserActivityFilterCategory.Library,
                Label = "Library request message",
                Detail = Truncate(pair.offering.Title, 80),
                PreviewContentType = EncryptedContentType.LibraryRequestMessage,
                CreatedAt = pair.message.CreatedAt,
                CrewId = pair.offering.CrewId,
                ResourceId = pair.message.Id,
                ParentResourceId = pair.request.Id,
                LibraryUnitId = pair.unit.Id,
                ResourceExists = !pair.offering.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var maintenanceQuery = context.LibraryMaintenanceRecords
            .AsNoTracking()
            .Where(record => record.ContributorUserId == userId)
            .Join(
                context.LibraryUnits.AsNoTracking(),
                record => record.UnitId,
                unit => unit.Id,
                (record, unit) => new { record, unit })
            .Join(
                context.LibraryOfferings.AsNoTracking(),
                pair => pair.unit.OfferingId,
                offering => offering.Id,
                (pair, offering) => new { pair.record, pair.unit, offering });

        if (beforeCreatedAt.HasValue)
        {
            maintenanceQuery = maintenanceQuery.Where(pair => pair.record.CreatedAt <= beforeCreatedAt.Value);
        }

        var maintenance = await maintenanceQuery
            .OrderByDescending(pair => pair.record.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"library-maintenance:{pair.record.Id}",
                Kind = UserActivityKind.LibraryMaintenance,
                Category = UserActivityFilterCategory.Library,
                Label = "Library maintenance",
                Detail = Truncate(pair.offering.Title, 80),
                PreviewContentType = EncryptedContentType.LibraryMaintenanceRecord,
                CreatedAt = pair.record.CreatedAt,
                CrewId = pair.offering.CrewId,
                ResourceId = pair.record.Id,
                ParentResourceId = pair.unit.Id,
                LibraryUnitId = pair.unit.Id,
                ResourceExists = !pair.offering.IsDeleted && !pair.unit.IsRetired
            })
            .ToListAsync(cancellationToken);

        return offeringRecords
            .Concat(requests)
            .Concat(messages)
            .Concat(maintenance)
            .ToList();
    }

    private async Task<IReadOnlyList<UserActivityRecord>> GetGiftActivitiesAsync(
        int userId,
        DateTime? beforeCreatedAt,
        int limit,
        CancellationToken cancellationToken)
    {
        var giftsQuery = context.Gifts
            .AsNoTracking()
            .Where(gift => gift.GiverUserId == userId)
            .Join(
                context.Users.AsNoTracking(),
                gift => gift.RecipientUserId,
                user => user.Id,
                (gift, recipient) => new { gift, recipient });

        if (beforeCreatedAt.HasValue)
        {
            giftsQuery = giftsQuery.Where(pair => pair.gift.CreatedAt <= beforeCreatedAt.Value);
        }

        return await giftsQuery
            .OrderByDescending(pair => pair.gift.CreatedAt)
            .Take(limit)
            .Select(pair => new UserActivityRecord
            {
                Key = $"gift:{pair.gift.Id}",
                Kind = UserActivityKind.Gift,
                Category = UserActivityFilterCategory.Gifts,
                Label = "Gift",
                Detail = $"To {pair.recipient.Username} · ${pair.gift.Amount.ToString("0.##", CultureInfo.InvariantCulture)}",
                PreviewContentType = EncryptedContentType.GiftLogEntry,
                CreatedAt = pair.gift.CreatedAt,
                CrewId = pair.gift.CrewId,
                ResourceId = pair.gift.Id,
                RelatedUserId = pair.recipient.Id,
                ResourceExists = true
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<UserActivityRecord>> GetProposalActivitiesAsync(
        int userId,
        DateTime? beforeCreatedAt,
        int limit,
        CancellationToken cancellationToken)
    {
        var proposalsQuery = context.Proposals
            .AsNoTracking()
            .Where(proposal => proposal.AuthorUserId == userId && !proposal.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            proposalsQuery = proposalsQuery.Where(proposal => proposal.CreatedAt <= beforeCreatedAt.Value);
        }

        var proposals = await proposalsQuery
            .Include(proposal => proposal.CrewmateKick)
            .Include(proposal => proposal.CrewJoinRequest)
            .Include(proposal => proposal.CrewRoleChange)
            .Include(proposal => proposal.ClaimPlaceholderIdentity)
            .Include(proposal => proposal.CrewmateRejoin)
            .Include(proposal => proposal.CrewChatChange)
            .Include(proposal => proposal.CrewRuleChange)
            .Include(proposal => proposal.CrewSettingChange)
            .OrderByDescending(proposal => proposal.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var proposalRecords = proposals.Select(proposal => new UserActivityRecord
        {
            Key = $"proposal:{proposal.Id}",
            Kind = UserActivityKind.Proposal,
            Category = UserActivityFilterCategory.Proposals,
            Label = "Proposal",
            Detail = Truncate(ResolveProposalTitle(proposal), 120),
            PlaintextPreview = Truncate(ResolveProposalTitle(proposal), 120),
            PreviewContentType = EncryptedContentType.Proposal,
            CreatedAt = proposal.CreatedAt,
            CrewId = proposal.CrewId!.Value,
            ResourceId = proposal.Id,
            ResourceExists = true
        }).ToList();

        var commentsQuery = context.ProposalComments
            .AsNoTracking()
            .Where(comment => comment.AuthorUserId == userId && !comment.IsDeleted)
            .Join(
                context.Proposals.AsNoTracking(),
                comment => comment.ProposalId,
                proposal => proposal.Id,
                (comment, proposal) => new { comment, proposal })
            .Where(pair => !pair.proposal.IsDeleted);

        if (beforeCreatedAt.HasValue)
        {
            commentsQuery = commentsQuery.Where(pair => pair.comment.CreatedAt <= beforeCreatedAt.Value);
        }

        var comments = await commentsQuery
            .OrderByDescending(pair => pair.comment.CreatedAt)
            .Take(limit)
            .Select(pair => new { pair.comment, pair.proposal })
            .ToListAsync(cancellationToken);

        var commentRecords = comments.Select(pair => new UserActivityRecord
        {
            Key = $"proposal-comment:{pair.comment.Id}",
            Kind = UserActivityKind.ProposalComment,
            Category = UserActivityFilterCategory.Proposals,
            Label = pair.comment.ParentCommentId.HasValue ? "Proposal reply" : "Proposal comment",
            Detail = "On proposal",
            PreviewContentType = EncryptedContentType.ProposalComment,
            CreatedAt = pair.comment.CreatedAt,
            CrewId = pair.proposal.CrewId ?? 0,
            ResourceId = pair.comment.Id,
            ParentResourceId = pair.proposal.Id,
            ResourceExists = true
        }).ToList();

        return proposalRecords.Concat(commentRecords).ToList();
    }

    private static string ResolveProposalTitle(Domain.Entities.Proposal proposal)
    {
        var title = proposal.CrewmateKick?.Title
            ?? proposal.CrewJoinRequest?.Title
            ?? proposal.CrewRoleChange?.Title
            ?? proposal.ClaimPlaceholderIdentity?.Title
            ?? proposal.CrewmateRejoin?.Title
            ?? proposal.CrewChatChange?.Title
            ?? proposal.CrewRuleChange?.Title
            ?? proposal.CrewSettingChange?.Title;

        return string.IsNullOrWhiteSpace(title) ? proposal.Kind.ToString() : title;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)]}…";
    }
}
