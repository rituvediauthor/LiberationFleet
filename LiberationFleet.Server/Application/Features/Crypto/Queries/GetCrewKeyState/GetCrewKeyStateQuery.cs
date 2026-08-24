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
    /// <summary>
    /// Every crew-key wrap for the current user across versions.
    /// Lets clients decrypt ciphertext encrypted before a key-version bump
    /// (e.g. accidental rotation) without minting a replacement key.
    /// </summary>
    public IReadOnlyList<CrewKeyDistributionDto> MyHistoricalDistributions { get; set; } = Array.Empty<CrewKeyDistributionDto>();
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

        var myHistorical = await cryptoRepository.GetCrewKeyDistributionsForUserAsync(
            request.CrewId,
            userId,
            cancellationToken);

        var myLatest = distributions
            .Where(d => d.UserId == userId)
            .Select(CryptoMapper.MapCrewKeyDistribution)
            .FirstOrDefault()
            ?? myHistorical
                .Where(d => d.KeyVersion == latestVersion.Value)
                .Select(CryptoMapper.MapCrewKeyDistribution)
                .FirstOrDefault();

        return new CrewKeyStateDto
        {
            LatestKeyVersion = latestVersion,
            MyDistribution = myLatest,
            MyHistoricalDistributions = myHistorical.Select(CryptoMapper.MapCrewKeyDistribution).ToList(),
            Distributions = distributions.Select(CryptoMapper.MapCrewKeyDistribution).ToList()
        };
    }
}
