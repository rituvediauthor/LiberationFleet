using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.LiveKit;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Voice.Commands.JoinVoiceRoom;

public record JoinVoiceRoomCommand(int RoomId) : IRequest<VoiceJoinResponse>;

public class JoinVoiceRoomCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IChatRepository chatRepository,
    IVoicePresenceRepository voicePresenceRepository,
    ILiveKitTokenService liveKitTokenService,
    ILiveKitAdminService liveKitAdminService,
    IVoicePresenceNotifier voicePresenceNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<JoinVoiceRoomCommand, VoiceJoinResponse>
{
    public async Task<VoiceJoinResponse> Handle(JoinVoiceRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VoiceJoinResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || room.IsDeleted)
        {
            return new VoiceJoinResponse { Success = false, Message = "Chat room not found." };
        }

        if (room.RoomType != ChatRoomType.Voice)
        {
            return new VoiceJoinResponse { Success = false, Message = "This room is not a voice channel." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, room.CrewId!.Value, cancellationToken))
        {
            return new VoiceJoinResponse { Success = false, Message = "You are not in this crew." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null)
        {
            return new VoiceJoinResponse { Success = false, Message = "User not found." };
        }

        var preference = user.AdultContentPreference;
        if (AdultContentAccess.IsBlocked(preference, room.IsAdultContent))
        {
            return new VoiceJoinResponse { Success = false, Message = "Chat room not found." };
        }

        int? previousRoomId = null;
        var existingSession = await voicePresenceRepository.GetActiveByUserAndCrewAsync(userId, room.CrewId!.Value, cancellationToken);
        if (existingSession is not null)
        {
            previousRoomId = existingSession.ChatRoomId;
            var previousRoomName = LiveKitRoomNaming.ForVoiceChannel(existingSession.CrewId, existingSession.ChatRoomId);
            await liveKitAdminService.RemoveParticipantAsync(previousRoomName, userId.ToString(), cancellationToken);
            await voicePresenceRepository.RemoveAsync(existingSession, cancellationToken);
        }

        var utcNow = DateTime.UtcNow;
        var session = new VoiceParticipantSession
        {
            UserId = userId,
            CrewId = room.CrewId!.Value,
            ChatRoomId = room.Id,
            JoinedAt = utcNow,
            LastSeenAt = utcNow
        };

        await voicePresenceRepository.AddAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(room.CrewId!.Value, cancellationToken);

        var liveKitRoomName = LiveKitRoomNaming.ForVoiceChannel(room.CrewId!.Value, room.Id);
        var token = liveKitTokenService.CreateRoomToken(userId.ToString(), user.Username, liveKitRoomName);

        return new VoiceJoinResponse
        {
            Success = true,
            Message = "Voice join token issued.",
            Token = token,
            WsUrl = liveKitTokenService.GetWebSocketUrl(),
            LiveKitRoomName = liveKitRoomName,
            SessionId = session.Id,
            ChatRoomId = room.Id,
            PreviousChatRoomId = previousRoomId
        };
    }
}
