using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleet;

public record UpdateFleetCommand(
    string Name,
    string Privacy,
    string Scope,
    string? ZipCode,
    int? RadiusMiles,
    bool RequireApprovalForEdits,
    bool LibraryOfThingsEnabled,
    bool AllowCrewmateFileAttachments,
    int MinimumCrewmateTenureDaysForAttachments,
    decimal MinimumContributionForAttachments,
    int MinimumCrewmateTenureDaysForProposals,
    decimal MinimumContributionForProposals) : IRequest<FleetOperationResponse>;

public class UpdateFleetCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    FleetSettingsProposalService fleetSettingsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateFleetCommand, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(UpdateFleetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (!Enum.TryParse<CrewPrivacy>(request.Privacy, true, out var privacy)
            || !Enum.IsDefined(privacy)
            || !Enum.TryParse<CrewScope>(request.Scope, true, out var scope)
            || !Enum.IsDefined(scope))
        {
            return new FleetOperationResponse { Success = false, Message = "Invalid privacy or scope." };
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            return new FleetOperationResponse { Success = false, Message = "Fleet name is required (max 100 characters)." };
        }

        if (scope == CrewScope.Local
            && (string.IsNullOrWhiteSpace(request.ZipCode) || request.ZipCode.Trim().Length != 5
                || request.RadiusMiles is null or < 1 or > 500))
        {
            return new FleetOperationResponse
            {
                Success = false,
                Message = "Local fleets require a 5-digit zip code and radius between 1 and 500 miles."
            };
        }

        var changes = FleetSettingsChangeDetector.DetectChanges(fleet, request, privacy, scope);
        if (changes.Count == 0)
        {
            return new FleetOperationResponse { Success = false, Message = "No changes to save." };
        }

        var crewCount = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken)).Count;

        if (fleet.RequireApprovalForEdits)
        {
            var proposalsCreated = await fleetSettingsProposalService.CreateProposalsAsync(
                fleet,
                currentUser.UserId.Value,
                changes,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new FleetOperationResponse
            {
                Success = true,
                Message = proposalsCreated == 1
                    ? "1 proposal submitted for fleet approval."
                    : $"{proposalsCreated} proposals submitted for fleet approval.",
                Fleet = FleetMapper.MapFleet(fleet, crewCount),
                ProposalsSubmitted = true,
                ProposalsCreated = proposalsCreated
            };
        }

        FleetSettingsProposalService.ApplyDirectUpdate(fleet, request, privacy, scope);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await fleetSettingsProposalService.NotifyFleetSettingChangedAsync(
            fleet.Id,
            currentUser.UserId.Value,
            cancellationToken);

        return new FleetOperationResponse
        {
            Success = true,
            Message = "Fleet updated.",
            Fleet = FleetMapper.MapFleet(fleet, crewCount)
        };
    }
}
