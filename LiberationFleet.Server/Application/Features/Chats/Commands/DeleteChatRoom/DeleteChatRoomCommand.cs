using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
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
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService,
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

        if (room.FleetId.HasValue)
        {
            return await HandleFleetDeleteAsync(request, userId, room, cancellationToken);
        }

        if (!room.CrewId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var membership = await membershipRepository.GetMembershipAsync(userId, room.CrewId.Value, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var crew = await crewRepository.GetByIdAsync(room.CrewId.Value, cancellationToken);
        if (crew is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
                crew,
                membership,
                giftRepository,
                contentTenureService,
                cancellationToken);
            if (!canPropose)
            {
                return new ChatOperationResponse
                {
                    Success = false,
                    Message = proposeError ?? "You are not allowed to create proposals yet."
                };
            }

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

    private async Task<ChatOperationResponse> HandleFleetDeleteAsync(
        DeleteChatRoomCommand request,
        int userId,
        Domain.Entities.ChatRoom room,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null || fleet.Id != room.FleetId)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        if (fleet.RequireApprovalForEdits)
        {
            var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureFleetMemberCanCreateAsync(
                fleet,
                membership,
                membership.Crew,
                giftRepository,
                contentTenureService,
                cancellationToken);
            if (!canPropose)
            {
                return new ChatOperationResponse
                {
                    Success = false,
                    Message = proposeError ?? "You are not allowed to create fleet proposals yet."
                };
            }

            var proposalId = await crewChatsProposalService.CreateFleetProposalAsync(
                fleet.Id,
                userId,
                CrewChatProposalAction.Delete,
                CrewChatChangeDescriber.DeleteTitle,
                CrewChatChangeDescriber.BuildDeleteDescription(request.PlaintextName, request.PlaintextPurpose),
                room.Id,
                room.Purpose,
                room.RoomType,
                request.PlaintextName,
                room.IsAdultContent,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for fleet approval.",
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
            Message = "Fleet chat room deleted."
        };
    }
}
