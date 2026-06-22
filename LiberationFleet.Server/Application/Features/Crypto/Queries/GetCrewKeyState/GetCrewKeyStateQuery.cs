using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetCrewKeyState;

public class CrewKeyStateDto
{
    public int? LatestKeyVersion { get; set; }
    public CrewKeyDistributionDto? MyDistribution { get; set; }
    public IReadOnlyList<CrewKeyDistributionDto> Distributions { get; set; } = Array.Empty<CrewKeyDistributionDto>();
}

public record GetCrewKeyStateQuery(int CrewId) : IRequest<CrewKeyStateDto?>;

public class GetCrewKeyStateQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewKeyStateQuery, CrewKeyStateDto?>
{
    public async Task<CrewKeyStateDto?> Handle(GetCrewKeyStateQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var userId = currentUser.UserId.Value;
        if (!await membershipRepository.IsUserInCrewAsync(userId, request.CrewId, cancellationToken))
        {
            return null;
        }

        var latestVersion = await cryptoRepository.GetLatestCrewKeyVersionAsync(request.CrewId, cancellationToken);
        if (!latestVersion.HasValue)
        {
            return new CrewKeyStateDto();
        }

        var distributions = await cryptoRepository.GetCrewKeyDistributionsAsync(
            request.CrewId,
            latestVersion.Value,
            cancellationToken);

        return new CrewKeyStateDto
        {
            LatestKeyVersion = latestVersion,
            MyDistribution = distributions
                .Where(d => d.UserId == userId)
                .Select(CryptoMapper.MapCrewKeyDistribution)
                .FirstOrDefault(),
            Distributions = distributions.Select(CryptoMapper.MapCrewKeyDistribution).ToList()
        };
    }
}
