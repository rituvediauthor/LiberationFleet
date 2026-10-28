using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Mentions;

public sealed class ContentMentionContext
{
    public int CrewId { get; init; }
    public int? FleetId { get; init; }
    public int AuthorUserId { get; init; }
    public bool OmitNotificationActorIdentity { get; init; }
    public string? NotificationAuthorName { get; init; }
    public MentionedContentType ContentType { get; init; }
    public int ResourceId { get; init; }
    public int? ParentResourceId { get; init; }
    public string ActionUrl { get; init; } = string.Empty;
    public IReadOnlyList<int> MentionedUserIds { get; init; } = Array.Empty<int>();
    public bool IsUpdate { get; init; }
    public string? Preview { get; init; }
    public string? LocationLabel { get; init; }
}

public class ContentMentionService(
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IContentMentionRepository mentionRepository,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public async Task ApplyMentionsAsync(ContentMentionContext context, CancellationToken cancellationToken = default)
    {
        var requestedIds = context.MentionedUserIds
            .Where(id => id > 0 && id != context.AuthorUserId)
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0 && !context.IsUpdate)
        {
            return;
        }

        HashSet<int> validMemberIds;
        if (context.FleetId.HasValue)
        {
            var fleetCrews = await fleetRepository.GetFleetCrewsAsync(context.FleetId.Value, cancellationToken);
            validMemberIds = new HashSet<int>();
            foreach (var fleetCrew in fleetCrews)
            {
                var members = await membershipRepository.GetActiveMembersByCrewIdAsync(
                    fleetCrew.CrewId,
                    cancellationToken);
                foreach (var member in members)
                {
                    validMemberIds.Add(member.UserId);
                }
            }
        }
        else
        {
            var members = await membershipRepository.GetActiveMembersByCrewIdAsync(context.CrewId, cancellationToken);
            validMemberIds = members.Select(m => m.UserId).ToHashSet();
        }

        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(context.AuthorUserId, cancellationToken);
        var validMentionIds = requestedIds
            .Where(id => validMemberIds.Contains(id) && !hiddenUserIds.Contains(id))
            .Distinct()
            .ToList();

        IReadOnlyList<int> previousMentionIds = Array.Empty<int>();
        if (context.IsUpdate)
        {
            previousMentionIds = await mentionRepository.GetMentionedUserIdsAsync(
                context.ContentType,
                context.ResourceId,
                cancellationToken);
            await mentionRepository.DeleteByContentAsync(
                context.ContentType,
                context.ResourceId,
                cancellationToken);
        }

        if (validMentionIds.Count > 0)
        {
            var utcNow = DateTime.UtcNow;
            await mentionRepository.AddRangeAsync(
                validMentionIds.Select(userId => new ContentMention
                {
                    CrewId = context.CrewId,
                    AuthorUserId = context.AuthorUserId,
                    MentionedUserId = userId,
                    ContentType = context.ContentType,
                    ResourceId = context.ResourceId,
                    ParentResourceId = context.ParentResourceId,
                    CreatedAt = utcNow
                }),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var notifyUserIds = context.IsUpdate
            ? validMentionIds.Except(previousMentionIds).ToList()
            : validMentionIds;

        if (notifyUserIds.Count == 0)
        {
            return;
        }

        var authorName = context.NotificationAuthorName;
        if (string.IsNullOrWhiteSpace(authorName))
        {
            var author = await userRepository.GetByIdWithProfileAsync(context.AuthorUserId, cancellationToken);
            authorName = UserDisplay.Name(author);
        }
        if (string.IsNullOrWhiteSpace(authorName))
        {
            authorName = "A crewmate";
        }

        var mentionTitle = NotificationCopy.MentionedYouIn(authorName, context.LocationLabel);
        var mentionBody = NotificationPreview.BodyOrFallback(
            context.Preview,
            mentionTitle + ".");

        var mutePostId = context.ContentType switch
        {
            MentionedContentType.ForumPost => context.ResourceId,
            MentionedContentType.ForumComment => context.ParentResourceId,
            _ => null
        };

        await notificationService.NotifyUsersAsync(
            notifyUserIds.Select(userId => new CreateNotificationRequest
            {
                UserId = userId,
                CrewId = context.CrewId,
                Kind = context.FleetId.HasValue ? NotificationKind.FleetMention : NotificationKind.Mention,
                Title = mentionTitle,
                Body = mentionBody,
                ActionUrl = context.ActionUrl,
                RelatedEntityId = context.ResourceId,
                SecondaryEntityId = context.ParentResourceId,
                ActorUserId = context.OmitNotificationActorIdentity ? null : context.AuthorUserId,
                MuteForumPostId = mutePostId,
                MuteCrewSpaceId = mutePostId.HasValue && !context.FleetId.HasValue ? context.CrewId : null,
                MuteFleetSpaceId = mutePostId.HasValue ? context.FleetId : null
            }),
            cancellationToken);
    }
}
