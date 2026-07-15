using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetFleetKeyState;

public class FleetKeyStateDto
{
    public int? LatestKeyVersion { get; set; }
    public FleetKeyDistributionDto? MyDistribution { get; set; }
    public IReadOnlyList<FleetKeyDistributionDto> Distributions { get; set; } = Array.Empty<FleetKeyDistributionDto>();
}

public record GetFleetKeyStateQuery(int FleetId) : IRequest<FleetKeyStateDto?>;

public class GetFleetKeyStateQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetFleetKeyStateQuery, FleetKeyStateDto?>
{
    public async Task<FleetKeyStateDto?> Handle(GetFleetKeyStateQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var userId = currentUser.UserId.Value;
        if (!await fleetRepository.IsUserInFleetAsync(userId, request.FleetId, cancellationToken))
        {
            return null;
        }

        var latestVersion = await cryptoRepository.GetLatestFleetKeyVersionAsync(request.FleetId, cancellationToken);
        if (!latestVersion.HasValue)
        {
            return new FleetKeyStateDto();
        }

        var distributions = await cryptoRepository.GetFleetKeyDistributionsAsync(
            request.FleetId,
            latestVersion.Value,
            cancellationToken);

        return new FleetKeyStateDto
        {
            LatestKeyVersion = latestVersion,
            MyDistribution = distributions
                .Where(d => d.UserId == userId)
                .Select(CryptoMapper.MapFleetKeyDistribution)
                .FirstOrDefault(),
            Distributions = distributions.Select(CryptoMapper.MapFleetKeyDistribution).ToList()
        };
    }
}
