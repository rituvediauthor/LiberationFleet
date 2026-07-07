using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Voice.Queries.GetVoicePresence;

public record GetVoicePresenceQuery(int CrewId) : IRequest<VoicePresenceSnapshotResponse>;

public class GetVoicePresenceQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IVoicePresenceRepository voicePresenceRepository) : IRequestHandler<GetVoicePresenceQuery, VoicePresenceSnapshotResponse>
{
    public async Task<VoicePresenceSnapshotResponse> Handle(GetVoicePresenceQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VoicePresenceSnapshotResponse { Success = false, Message = "Unauthorized." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(currentUser.UserId.Value, request.CrewId, cancellationToken))
        {
            return new VoicePresenceSnapshotResponse { Success = false, Message = "You are not in this crew." };
        }

        return await VoicePresenceMapper.BuildSnapshotAsync(voicePresenceRepository, request.CrewId, cancellationToken);
    }
}
