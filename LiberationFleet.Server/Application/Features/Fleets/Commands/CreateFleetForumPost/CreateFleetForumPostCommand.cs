using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleetForumPost;

public record CreateFleetForumPostCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    bool IsAdultContent,
    IReadOnlyList<int> MentionedUserIds,
    string? Preview = null) : IRequest<ForumOperationResponse>;

public class CreateFleetForumPostCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(CreateFleetForumPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ForumOperationResponse { Success = false, Message = "Encrypted post content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new ForumOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, fleet.Id, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        var utcNow = DateTime.UtcNow;
        var post = new ForumPost
        {
            FleetId = fleet.Id,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = request.IsAdultContent
        };

        await forumRepository.AddPostAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ForumPost,
            ResourceId = post.Id.ToString(),
            FleetId = fleet.Id,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actionUrl = $"/app/fleet/forums/{post.Id}";
        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewIfNotMutedAsync(
                fleetCrew.CrewId,
                NotificationKind.NewFleetForumPost,
                MutedContentType.Forum,
                post.Id,
                "New fleet forum post",
                NotificationPreview.BodyOrFallback(request.Preview, "A new forum post was published in your fleet."),
                actionUrl,
                relatedEntityId: post.Id,
                excludeUserId: userId,
                cancellationToken: cancellationToken);
        }

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            FleetId = fleet.Id,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ForumPost,
            ResourceId = post.Id,
            ActionUrl = actionUrl,
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            Preview = request.Preview
        }, cancellationToken);

        return new ForumOperationResponse
        {
            Success = true,
            Message = "Forum post created.",
            PostId = post.Id
        };
    }
}
