using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Voice.Commands.ServerMuteVoiceParticipant;

public record ServerMuteVoiceParticipantCommand(int RoomId, int TargetUserId, bool IsServerMuted)
    : IRequest<VoiceOperationResponse>;

public class ServerMuteVoiceParticipantCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IVoicePresenceRepository voicePresenceRepository,
    IVoicePresenceNotifier voicePresenceNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<ServerMuteVoiceParticipantCommand, VoiceOperationResponse>
{
    public async Task<VoiceOperationResponse> Handle(ServerMuteVoiceParticipantCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VoiceOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var actorId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(actorId, cancellationToken);
        if (membership is null || !CrewRoleAuthorizationService.CanModerateAttachments(membership))
        {
            return new VoiceOperationResponse { Success = false, Message = "You do not have permission to server mute participants." };
        }

        var session = await voicePresenceRepository.GetByUserAndRoomAsync(request.TargetUserId, request.RoomId, cancellationToken);
        if (session is null)
        {
            return new VoiceOperationResponse { Success = false, Message = "Participant not found in this voice channel." };
        }

        session.IsServerMuted = request.IsServerMuted;
        session.LastSeenAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var participant = VoicePresenceMapper.MapParticipant(session);
        await voicePresenceNotifier.NotifyStateUpdatedAsync(session.CrewId, participant, cancellationToken);
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(session.CrewId, cancellationToken);

        return new VoiceOperationResponse { Success = true, Message = request.IsServerMuted ? "Participant server muted." : "Server mute removed." };
    }
}
