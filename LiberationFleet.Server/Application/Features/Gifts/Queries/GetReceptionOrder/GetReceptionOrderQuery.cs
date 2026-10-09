using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;

public record GetReceptionOrderQuery(int Limit = 30, int? ImpersonateAsUserId = null)
    : IRequest<IReadOnlyList<ReceptionOrderEntryDto>>;

public class GetReceptionOrderQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IMutualAidService mutualAidService) : IRequestHandler<GetReceptionOrderQuery, IReadOnlyList<ReceptionOrderEntryDto>>
{
    public async Task<IReadOnlyList<ReceptionOrderEntryDto>> Handle(
        GetReceptionOrderQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        var viewerId = currentUser.UserId.Value;
        var giverUserId = viewerId;

        if (request.ImpersonateAsUserId is int impersonateAs && impersonateAs != viewerId)
        {
            var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
            if (membership is null || !CrewRoleAuthorizationService.CanImpersonateGiftGiver(membership))
            {
                return Array.Empty<ReceptionOrderEntryDto>();
            }

            var targetMembership = await membershipRepository.GetMembershipAsync(
                impersonateAs,
                membership.CrewId,
                cancellationToken);
            if (targetMembership is null
                || targetMembership.IsBanned
                || targetMembership.LeftAt is not null)
            {
                return Array.Empty<ReceptionOrderEntryDto>();
            }

            giverUserId = impersonateAs;
        }

        return await mutualAidService.GetReceptionOrderAsync(
            giverUserId,
            request.Limit,
            requireGiverInSeason: true,
            excludeSelfAsRecipient: false,
            forRecordGift: true,
            cancellationToken);
    }
}
