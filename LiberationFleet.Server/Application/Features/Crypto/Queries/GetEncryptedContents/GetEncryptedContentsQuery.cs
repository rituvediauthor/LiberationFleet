using LiberationFleet.Server.Application.Common.Interfaces;

using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

using LiberationFleet.Server.Application.Features.Crypto;

using LiberationFleet.Server.Application.Features.Crypto.Contracts;

using MediatR;



namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContents;



public record GetEncryptedContentsQuery(

    EncryptedContentTypeDto ContentType,

    IReadOnlyList<string> ResourceIds,

    int? CrewId = null,

    int? FleetId = null) : IRequest<IReadOnlyList<EncryptedContentEnvelopeDto>>;



public class GetEncryptedContentsQueryHandler(

    ICurrentUserService currentUser,

    ICrewMembershipRepository membershipRepository,

    IFleetRepository fleetRepository,

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



        var hasCrewScope = request.CrewId.HasValue;

        var hasFleetScope = request.FleetId.HasValue;

        if (hasCrewScope == hasFleetScope)

        {

            return Array.Empty<EncryptedContentEnvelopeDto>();

        }



        var userId = currentUser.UserId.Value;

        if (hasCrewScope)

        {

            if (!await membershipRepository.IsUserInCrewAsync(userId, request.CrewId!.Value, cancellationToken))

            {

                return Array.Empty<EncryptedContentEnvelopeDto>();

            }

        }

        else if (!await fleetRepository.IsUserInFleetAsync(userId, request.FleetId!.Value, cancellationToken))

        {

            return Array.Empty<EncryptedContentEnvelopeDto>();

        }



        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            CryptoMapper.ToDomain(request.ContentType),
            request.ResourceIds,
            crewId: request.CrewId,
            fleetId: request.FleetId,
            cancellationToken: cancellationToken);



        return envelopes.Select(CryptoMapper.MapEnvelope).ToList();

    }

}


