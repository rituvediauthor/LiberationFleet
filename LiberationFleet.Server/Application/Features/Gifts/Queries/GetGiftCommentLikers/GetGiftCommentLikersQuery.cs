using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Engagement.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftCommentLikers;

public record GetGiftCommentLikersQuery(int GiftId, int CommentId) : IRequest<ContentLikersResponse>;

public class GetGiftCommentLikersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetGiftCommentLikersQuery, ContentLikersResponse>
{
    public async Task<ContentLikersResponse> Handle(
        GetGiftCommentLikersQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ContentLikersResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ContentLikersResponse { Success = false, Message = "You are not in a crew." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new ContentLikersResponse { Success = false, Message = "Gift not found." };
        }

        var comment = await giftRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.GiftId != gift.Id)
        {
            return new ContentLikersResponse { Success = false, Message = "Comment not found." };
        }

        var likers = await giftRepository.GetActiveGiftCommentLikersAsync(comment.Id, cancellationToken);
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(membership.CrewId, cancellationToken);

        return new ContentLikersResponse
        {
            Success = true,
            Message = "Likers loaded.",
            Items = likers.Select(l => new ContentLikerDto
            {
                UserId = l.UserId,
                Username = l.Username,
                AvatarResourceId = CrewAvatarVisibilityService.Filter(l.AvatarResourceId, l.UserId, avatarAllowed)
            }).ToList()
        };
    }
}
