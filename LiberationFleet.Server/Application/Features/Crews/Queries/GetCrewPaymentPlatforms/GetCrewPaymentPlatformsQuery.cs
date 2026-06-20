using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetCrewPaymentPlatforms;

public record GetCrewPaymentPlatformsQuery(bool OtherCrewmatesOnly = false) : IRequest<IReadOnlyList<PaymentPlatformOptionDto>>;

public class GetCrewPaymentPlatformsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository)
    : IRequestHandler<GetCrewPaymentPlatformsQuery, IReadOnlyList<PaymentPlatformOptionDto>>
{
    public async Task<IReadOnlyList<PaymentPlatformOptionDto>> Handle(
        GetCrewPaymentPlatformsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<PaymentPlatformOptionDto>();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return Array.Empty<PaymentPlatformOptionDto>();
        }

        var platforms = request.OtherCrewmatesOnly
            ? await crewPaymentPlatformRepository.GetUsedByOtherCrewmatesAsync(
                membership.CrewId,
                currentUser.UserId.Value,
                cancellationToken)
            : await crewPaymentPlatformRepository.GetByCrewIdAsync(membership.CrewId, cancellationToken);
        return platforms
            .Select(p => new PaymentPlatformOptionDto { Id = p.Id, Name = p.Name })
            .ToList();
    }
}
