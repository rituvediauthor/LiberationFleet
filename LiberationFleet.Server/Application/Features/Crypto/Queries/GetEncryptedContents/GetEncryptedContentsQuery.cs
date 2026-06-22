using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContents;

public record GetEncryptedContentsQuery(
    EncryptedContentTypeDto ContentType,
    IReadOnlyList<string> ResourceIds,
    int? CrewId = null) : IRequest<IReadOnlyList<EncryptedContentEnvelopeDto>>;

public class GetEncryptedContentsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetEncryptedContentsQuery, IReadOnlyList<EncryptedContentEnvelopeDto>>
{
    public async Task<IReadOnlyList<EncryptedContentEnvelopeDto>> Handle(
        GetEncryptedContentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue || request.ResourceIds.Count == 0)
        {
            return Array.Empty<EncryptedContentEnvelopeDto>();
        }

        if (request.CrewId.HasValue
            && !await membershipRepository.IsUserInCrewAsync(currentUser.UserId.Value, request.CrewId.Value, cancellationToken))
        {
            return Array.Empty<EncryptedContentEnvelopeDto>();
        }

        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            CryptoMapper.ToDomain(request.ContentType),
            request.ResourceIds,
            request.CrewId,
            cancellationToken);

        return envelopes.Select(CryptoMapper.MapEnvelope).ToList();
    }
}
