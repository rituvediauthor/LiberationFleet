using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.CreateChatRoom;

public record CreateChatRoomCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    ChatRoomType RoomType) : IRequest<ChatOperationResponse>;

public class CreateChatRoomCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateChatRoomCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(CreateChatRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted chat room name is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var room = new ChatRoom
        {
            CrewId = membership.CrewId,
            Name = string.Empty,
            RoomType = request.RoomType,
            CreatedByUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await chatRepository.AddRoomAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedRoom = await chatRepository.GetRoomByIdWithAuthorAsync(room.Id, cancellationToken);
        var nameEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomName,
            room.Id.ToString(),
            cancellationToken);

        if (savedRoom is not null)
        {
            var dto = ChatMapper.MapListItem(savedRoom, nameEnvelope);
            await chatRealtimeNotifier.NotifyRoomCreatedAsync(membership.CrewId, dto, cancellationToken);
        }

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Chat room created.",
            RoomId = room.Id
        };
    }
}
