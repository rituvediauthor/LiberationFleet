using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetCrewPublicKeys;

public record GetCrewPublicKeysQuery(int CrewId) : IRequest<IReadOnlyList<UserKeyBundleDto>>;

public class GetCrewPublicKeysQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewPublicKeysQuery, IReadOnlyList<UserKeyBundleDto>>
{
    public async Task<IReadOnlyList<UserKeyBundleDto>> Handle(GetCrewPublicKeysQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<UserKeyBundleDto>();
        }

        if (!await membershipRepository.IsUserInCrewAsync(currentUser.UserId.Value, request.CrewId, cancellationToken))
        {
            return Array.Empty<UserKeyBundleDto>();
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(request.CrewId, cancellationToken);
        var userIds = members.Select(m => m.UserId).ToList();

        var pendingApplicants = await proposalRepository.GetPendingJoinApplicantUserIdsForCrewAsync(
            request.CrewId,
            cancellationToken);
        userIds.AddRange(pendingApplicants);

        var distinctUserIds = userIds.Distinct().ToList();
        var bundles = await cryptoRepository.GetUserKeyBundlesAsync(distinctUserIds, cancellationToken);
        return bundles.Select(CryptoMapper.MapKeyBundle).ToList();
    }
}
