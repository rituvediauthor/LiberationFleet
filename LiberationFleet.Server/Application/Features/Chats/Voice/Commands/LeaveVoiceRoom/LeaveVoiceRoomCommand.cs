using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Infrastructure.LiveKit;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Voice.Commands.LeaveVoiceRoom;

public record LeaveVoiceRoomCommand(int RoomId) : IRequest<VoiceOperationResponse>;

public class LeaveVoiceRoomCommandHandler(
    ICurrentUserService currentUser,
    IVoicePresenceRepository voicePresenceRepository,
    ILiveKitAdminService liveKitAdminService,
    IVoicePresenceNotifier voicePresenceNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<LeaveVoiceRoomCommand, VoiceOperationResponse>
{
    public async Task<VoiceOperationResponse> Handle(LeaveVoiceRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VoiceOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var activeSession = await voicePresenceRepository.GetByUserAndRoomAsync(userId, request.RoomId, cancellationToken);
        if (activeSession is null)
        {
            return new VoiceOperationResponse { Success = true, Message = "Voice session already ended." };
        }

        var liveKitRoomName = LiveKitRoomNaming.ForVoiceChannel(activeSession.CrewId, activeSession.ChatRoomId);
        await liveKitAdminService.RemoveParticipantAsync(liveKitRoomName, userId.ToString(), cancellationToken);
        await voicePresenceRepository.RemoveAsync(activeSession, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(activeSession.CrewId, cancellationToken);

        return new VoiceOperationResponse { Success = true, Message = "Left voice channel." };
    }
}
