using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.UpdateCrew;

public record UpdateCrewCommand(
    string Name,
    int MaxSize,
    string Privacy,
    string Scope,
    string? ZipCode,
    int? RadiusMiles,
    bool AllowSurvivalThresholds,
    bool RequireApprovalForEdits,
    decimal InNeedDefaultThreshold,
    bool LibraryOfThingsEnabled,
    string MemberCycleCapMode,
    decimal MemberCycleCapFixedAmount,
    decimal MemberCycleCapMultiplier,
    string NonMemberCycleCapMode,
    decimal NonMemberCycleCapFixedAmount,
    decimal NonMemberCycleCapMultiplier,
    bool AllowCrewmateFileAttachments,
    int MinimumCrewmateTenureDaysForAttachments,
    decimal MinimumContributionForAttachments,
    int MinimumCrewmateTenureDaysForProposals,
    decimal MinimumContributionForProposals) : IRequest<CrewOperationResponse>;

public class UpdateCrewCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    CrewSettingsProposalService crewSettingsProposalService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCrewCommand, CrewOperationResponse>
{
    public async Task<CrewOperationResponse> Handle(UpdateCrewCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new CrewOperationResponse { Success = false, Message = "Crew not found." };
        }

        var memberCount = await crewRepository.CountMembersAsync(crew.Id, cancellationToken);
        var validationError = CrewUpdateValidator.Validate(request, memberCount, out var privacy, out var scope);
        if (validationError is not null)
        {
            return validationError;
        }

        var changes = CrewSettingsChangeDetector.DetectChanges(crew, request, privacy, scope);
        if (changes.Count == 0)
        {
            return new CrewOperationResponse { Success = false, Message = "No changes to save." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var proposalsCreated = await crewSettingsProposalService.CreateProposalsAsync(
                crew,
                currentUser.UserId.Value,
                changes,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new CrewOperationResponse
            {
                Success = true,
                Message = proposalsCreated == 1
                    ? "1 proposal submitted for crew approval."
                    : $"{proposalsCreated} proposals submitted for crew approval.",
                Crew = CrewMapper.MapCrew(crew, memberCount),
                ProposalsSubmitted = true,
                ProposalsCreated = proposalsCreated
            };
        }

        CrewSettingsProposalService.ApplyDirectUpdate(crew, request, privacy, scope);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.CrewSettingChanged,
            "Crew setting changed",
            "Crew settings were updated.",
            "/app/crew/edit",
            excludeUserId: currentUser.UserId.Value,
            cancellationToken: cancellationToken);

        return new CrewOperationResponse
        {
            Success = true,
            Message = "Crew updated.",
            Crew = CrewMapper.MapCrew(crew, memberCount)
        };
    }
}
