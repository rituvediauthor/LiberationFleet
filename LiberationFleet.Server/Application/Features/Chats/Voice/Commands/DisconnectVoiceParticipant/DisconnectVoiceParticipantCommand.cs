using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Infrastructure.LiveKit;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Voice.Commands.DisconnectVoiceParticipant;

public record DisconnectVoiceParticipantCommand(int RoomId, int TargetUserId) : IRequest<VoiceOperationResponse>;

public class DisconnectVoiceParticipantCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IVoicePresenceRepository voicePresenceRepository,
    ILiveKitAdminService liveKitAdminService,
    IVoicePresenceNotifier voicePresenceNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<DisconnectVoiceParticipantCommand, VoiceOperationResponse>
{
    public async Task<VoiceOperationResponse> Handle(DisconnectVoiceParticipantCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VoiceOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var actorId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(actorId, cancellationToken);
        if (membership is null || !CrewRoleAuthorizationService.CanModerateAttachments(membership))
        {
            return new VoiceOperationResponse { Success = false, Message = "You do not have permission to disconnect participants." };
        }

        var session = await voicePresenceRepository.GetByUserAndRoomAsync(request.TargetUserId, request.RoomId, cancellationToken);
        if (session is null)
        {
            return new VoiceOperationResponse { Success = false, Message = "Participant not found in this voice channel." };
        }

        var liveKitRoomName = LiveKitRoomNaming.ForVoiceChannel(session.CrewId, session.ChatRoomId);
        await liveKitAdminService.RemoveParticipantAsync(liveKitRoomName, request.TargetUserId.ToString(), cancellationToken);
        await voicePresenceRepository.RemoveAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(session.CrewId, cancellationToken);

        return new VoiceOperationResponse { Success = true, Message = "Participant disconnected." };
    }
}
