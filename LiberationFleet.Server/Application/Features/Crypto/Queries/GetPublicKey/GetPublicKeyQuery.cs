using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetPublicKey;

public record GetPublicKeyQuery(int UserId) : IRequest<UserKeyBundleDto?>;

public class GetPublicKeyQueryHandler(
    ICurrentUserService currentUser,
    ICryptoRepository cryptoRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetPublicKeyQuery, UserKeyBundleDto?>
{
    public async Task<UserKeyBundleDto?> Handle(GetPublicKeyQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId != request.UserId)
        {
            if (await blockRepository.IsBlockedAsync(viewerId, request.UserId, cancellationToken)
                || await blockRepository.IsBlockedAsync(request.UserId, viewerId, cancellationToken))
            {
                return null;
            }

            var friendship = await friendshipRepository.GetBetweenUsersAsync(viewerId, request.UserId, cancellationToken);
            var areFriends = friendship is not null && friendship.Status == FriendshipStatus.Accepted;
            if (!areFriends)
            {
                var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
                var targetMembership = await membershipRepository.GetActiveMembershipAsync(request.UserId, cancellationToken);
                var sameCrew = viewerMembership is not null
                    && targetMembership is not null
                    && viewerMembership.CrewId == targetMembership.CrewId;

                var sameFleet = false;
                if (!sameCrew)
                {
                    var viewerFleet = await fleetRepository.GetFleetForUserAsync(viewerId, cancellationToken);
                    if (viewerFleet is not null)
                    {
                        sameFleet = await fleetRepository.IsUserInFleetAsync(request.UserId, viewerFleet.Id, cancellationToken);
                    }
                }

                if (!sameCrew && !sameFleet)
                {
                    return null;
                }
            }
        }

        var bundle = await cryptoRepository.GetUserKeyBundleAsync(request.UserId, cancellationToken);
        return bundle is null ? null : CryptoMapper.MapKeyBundle(bundle);
    }
}
