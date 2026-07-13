using System.Security.Cryptography;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleet;

public record CreateFleetCommand(
    string Name,
    string Privacy,
    string Scope,
    string? ZipCode,
    int? RadiusMiles) : IRequest<FleetOperationResponse>;

public class CreateFleetCommandHandler(
    ICurrentUserService currentUserService,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetCommand, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(CreateFleetCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized" };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetOperationResponse { Success = false, Message = "You must be in a crew to create a fleet." };
        }

        if (await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken) is not null)
        {
            return new FleetOperationResponse { Success = false, Message = "Your crew already belongs to a fleet." };
        }

        if (!Enum.TryParse<CrewPrivacy>(request.Privacy, true, out var privacy)
            || !Enum.TryParse<CrewScope>(request.Scope, true, out var scope))
        {
            return new FleetOperationResponse { Success = false, Message = "Invalid privacy or scope." };
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return new FleetOperationResponse { Success = false, Message = "Fleet name is required (max 100 characters)." };
        }

        if (scope == CrewScope.Local)
        {
            if (string.IsNullOrWhiteSpace(request.ZipCode) || request.ZipCode.Trim().Length != 5
                || request.RadiusMiles is null or < 1 or > 500)
            {
                return new FleetOperationResponse
                {
                    Success = false,
                    Message = "Local fleets require a 5-digit zip code and radius between 1 and 500 miles."
                };
            }
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Crew not found." };
        }

        var fleet = new Fleet
        {
            Name = name,
            Privacy = privacy,
            Scope = scope,
            ZipCode = scope == CrewScope.Local ? request.ZipCode!.Trim() : null,
            RadiusMiles = scope == CrewScope.Local ? request.RadiusMiles : null,
            JoinCode = await GenerateUniqueJoinCodeAsync(cancellationToken),
            CreatedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            RequireApprovalForEdits = true,
            LibraryOfThingsEnabled = true
        };

        await fleetRepository.AddAsync(fleet, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await fleetRepository.AddFleetCrewAsync(new FleetCrew
        {
            FleetId = fleet.Id,
            CrewId = crew.Id,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);

        await chatRepository.AddRoomAsync(new ChatRoom
        {
            FleetId = fleet.Id,
            LinkedCrewId = crew.Id,
            Name = crew.Name,
            Purpose = $"Fleet chat for {crew.Name}",
            RoomType = ChatRoomType.Text,
            CreatedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetOperationResponse
        {
            Success = true,
            Message = "Fleet created successfully",
            Fleet = FleetMapper.MapFleet(fleet, 1)
        };
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateJoinCode();
            if (await fleetRepository.GetByJoinCodeAsync(code, cancellationToken) is null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique join code.");
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> result = stackalloc char[8];
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < 8; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }
}
