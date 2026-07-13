using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleetForumPost;

public record CreateFleetForumPostCommand(
    string Title,
    string Body,
    bool IsAdultContent) : IRequest<ForumOperationResponse>;

public class CreateFleetForumPostCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(CreateFleetForumPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return new ForumOperationResponse { Success = false, Message = "Title and body are required." };
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
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = request.IsAdultContent
        };

        await forumRepository.AddPostAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actionUrl = $"/app/fleet/forums/{post.Id}";
        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewIfNotMutedAsync(
                fleetCrew.CrewId,
                NotificationKind.NewForumPost,
                MutedContentType.Forum,
                post.Id,
                "New forum post",
                "A new forum post was published.",
                actionUrl,
                relatedEntityId: post.Id,
                excludeUserId: userId,
                cancellationToken: cancellationToken);
        }

        return new ForumOperationResponse
        {
            Success = true,
            Message = "Forum post created.",
            PostId = post.Id
        };
    }
}
