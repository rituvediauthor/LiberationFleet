using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;

public record GetCrewGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository) : IRequestHandler<GetCrewGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetCrewGiftLogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLogResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftLogResponse { Success = false, Message = "You are not in a crew." };
        }

        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 100);
        var page = await giftRepository.GetLogPageByCrewIdAsync(
            membership.CrewId,
            limit,
            request.BeforeCreatedAt,
            request.BeforeId,
            cancellationToken);
        var completedInitiatedIds = await giftRepository.GetCompletedInitiatedGiftIdsAsync(membership.CrewId, cancellationToken);

        var items = page.Items.Select(gift =>
        {
            var isPendingMiddleman = gift.Type == GiftType.Initiated
                && gift.MiddlemanUserId == userId
                && !completedInitiatedIds.Contains(gift.Id);
            var status = gift.Type == GiftType.Initiated
                ? (completedInitiatedIds.Contains(gift.Id) ? "completed" : "pending")
                : "completed";

            IReadOnlyList<PaymentPlatformOptionDto>? completionOptions = null;
            if (isPendingMiddleman && gift.MiddlemanUser is not null && gift.RecipientUser is not null)
            {
                completionOptions = CrewPaymentPlatformService.GetCommonPlatforms(gift.MiddlemanUser, gift.RecipientUser);
            }

            return GiftMapper.MapGift(gift, isPendingMiddleman, status, completionOptions);
        }).ToList();

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }
}
