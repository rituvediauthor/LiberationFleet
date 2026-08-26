using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.ReorderChatRooms;

public record ReorderChatRoomsCommand(
    IReadOnlyList<int> RoomIds,
    bool Personal,
    string Scope = "crew") : IRequest<ChatOperationResponse>;

public class ReorderChatRoomsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService,
    CrewChatsProposalService crewChatsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<ReorderChatRoomsCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(
        ReorderChatRoomsCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Failure("Unauthorized.");
        }

        if (request.RoomIds is null || request.RoomIds.Any(id => id <= 0))
        {
            return Failure("Room IDs must be positive integers.");
        }

        if (request.RoomIds.Count != request.RoomIds.Distinct().Count())
        {
            return Failure("Room IDs must not contain duplicates.");
        }

        var isFleetScope = string.Equals(request.Scope, "fleet", StringComparison.OrdinalIgnoreCase);
        if (!isFleetScope && !string.Equals(request.Scope, "crew", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Scope must be 'crew' or 'fleet'.");
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return Failure("You are not in a crew.");
        }

        int? crewId = null;
        int? fleetId = null;
        bool requiresApproval;
        Domain.Entities.Crew? crew = null;
        Domain.Entities.Fleet? fleet = null;

        if (isFleetScope)
        {
            fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
            if (fleet is null)
            {
                return Failure("You are not in a fleet.");
            }

            fleetId = fleet.Id;
            requiresApproval = fleet.RequireApprovalForEdits;
        }
        else
        {
            crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
            if (crew is null)
            {
                return Failure("Crew not found.");
            }

            crewId = crew.Id;
            requiresApproval = crew.RequireApprovalForEdits;
        }

        var rooms = isFleetScope
            ? await chatRepository.GetRoomsByFleetIdAsync(fleetId!.Value, cancellationToken)
            : await chatRepository.GetRoomsByCrewIdAsync(crewId!.Value, cancellationToken);
        var validRoomIds = rooms.Select(room => room.Id).ToHashSet();
        if (request.RoomIds.Any(roomId => !validRoomIds.Contains(roomId)))
        {
            return Failure("One or more chat rooms do not belong to the selected scope.");
        }

        var orderedRoomIdsJson = JsonSerializer.Serialize(request.RoomIds);
        if (request.Personal)
        {
            await chatRepository.UpsertPersonalOrderAsync(
                userId,
                crewId,
                fleetId,
                orderedRoomIdsJson,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Success("Personal chat room order saved.");
        }

        if (requiresApproval)
        {
            if (isFleetScope)
            {
                var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureFleetMemberCanCreateAsync(
                    fleet!,
                    membership,
                    membership.Crew,
                    giftRepository,
                    contentTenureService,
                    cancellationToken);
                if (!canPropose)
                {
                    return Failure(proposeError ?? "You are not allowed to create fleet proposals yet.");
                }
            }
            else
            {
                var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
                    crew!,
                    membership,
                    giftRepository,
                    contentTenureService,
                    cancellationToken);
                if (!canPropose)
                {
                    return Failure(proposeError ?? "You are not allowed to create proposals yet.");
                }
            }

            var proposalId = isFleetScope
                ? await crewChatsProposalService.CreateFleetProposalAsync(
                    fleetId!.Value,
                    userId,
                    CrewChatProposalAction.Reorder,
                    "Reorder chat channels",
                    "Change the shared order of fleet chat channels.",
                    roomId: null,
                    purpose: string.Empty,
                    roomType: ChatRoomType.Text,
                    plaintextName: string.Empty,
                    isAdultContent: false,
                    cancellationToken: cancellationToken,
                    orderedRoomIdsJson: orderedRoomIdsJson)
                : await crewChatsProposalService.CreateProposalAsync(
                    crewId!.Value,
                    userId,
                    CrewChatProposalAction.Reorder,
                    "Reorder chat channels",
                    "Change the shared order of crew chat channels.",
                    roomId: null,
                    purpose: string.Empty,
                    roomType: ChatRoomType.Text,
                    nameNonce: null,
                    nameCiphertext: null,
                    keyVersion: 1,
                    isAdultContent: false,
                    cancellationToken: cancellationToken,
                    orderedRoomIdsJson: orderedRoomIdsJson);

            return new ChatOperationResponse
            {
                Success = true,
                Message = $"Proposal submitted for {(isFleetScope ? "fleet" : "crew")} approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        await chatRepository.UpdateRoomSortOrdersAsync(
            request.RoomIds,
            crewId,
            fleetId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Success("Shared chat room order saved.");
    }

    private static ChatOperationResponse Failure(string message) => new()
    {
        Success = false,
        Message = message
    };

    private static ChatOperationResponse Success(string message) => new()
    {
        Success = true,
        Message = message
    };
}
