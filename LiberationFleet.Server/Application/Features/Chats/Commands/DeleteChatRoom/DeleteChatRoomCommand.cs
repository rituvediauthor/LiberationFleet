using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.DeleteChatRoom;

public record DeleteChatRoomCommand(
    int RoomId,
    string PlaintextName,
    string PlaintextPurpose) : IRequest<ChatOperationResponse>;

public class DeleteChatRoomCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IChatRepository chatRepository,
    CrewChatsProposalService crewChatsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteChatRoomCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(DeleteChatRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, room.CrewId, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var crew = await crewRepository.GetByIdAsync(room.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var proposalId = await crewChatsProposalService.CreateProposalAsync(
                crew.Id,
                userId,
                CrewChatProposalAction.Delete,
                CrewChatChangeDescriber.DeleteTitle,
                CrewChatChangeDescriber.BuildDeleteDescription(request.PlaintextName, request.PlaintextPurpose),
                room.Id,
                room.Purpose,
                room.RoomType,
                nameNonce: null,
                nameCiphertext: null,
                keyVersion: 1,
                room.IsAdultContent,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for crew approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        room.IsDeleted = true;
        room.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Chat room deleted."
        };
    }
}
