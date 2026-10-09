using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.AddPlaceholderCrewmate;

public record AddPlaceholderCrewmateCommand(
    string Name,
    IReadOnlyList<PaymentPlatformAccountDto> PaymentPlatforms,
    int EmergencyLevel = 0,
    int PeopleRepresentedCount = 1,
    int DisabilityLevel = 0,
    IReadOnlyList<string>? IdentityGroups = null,
    bool? InNeedOfAid = null,
    bool? NeedsSurvivalAid = null,
    bool? IsFinancialMember = null,
    decimal? EstimatedMonthlyContribution = null,
    int? PercentBoost = null,
    decimal? LifetimeContributionOverride = null,
    decimal? ReceptionThisYearOverride = null,
    AidSeasonAccountingDto? SeasonAccounting = null) : IRequest<AddPlaceholderCrewmateResponse>;

public class AddPlaceholderCrewmateCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    PlaceholderCrewmateService placeholderCrewmateService,
    IUnitOfWork unitOfWork) : IRequestHandler<AddPlaceholderCrewmateCommand, AddPlaceholderCrewmateResponse>
{
    public async Task<AddPlaceholderCrewmateResponse> Handle(
        AddPlaceholderCrewmateCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new AddPlaceholderCrewmateResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new AddPlaceholderCrewmateResponse { Success = false, Message = "You are not in a crew." };
        }

        var hasRichPayload = HasRichPayload(request);
        if (hasRichPayload)
        {
            if (!CrewRoleAuthorizationService.CanManagePlaceholders(membership))
            {
                return new AddPlaceholderCrewmateResponse
                {
                    Success = false,
                    Message = "Only organizers and accountants can create placeholders with aid settings."
                };
            }
        }
        else if (!membership.IsInSeason)
        {
            return new AddPlaceholderCrewmateResponse
            {
                Success = false,
                Message = "You must be in an active season to add a non-member."
            };
        }

        PlaceholderCreateOptions? options = null;
        if (hasRichPayload
            || request.InNeedOfAid.HasValue
            || request.NeedsSurvivalAid.HasValue
            || request.IsFinancialMember.HasValue)
        {
            options = new PlaceholderCreateOptions
            {
                InNeedOfAid = request.InNeedOfAid,
                NeedsSurvivalAid = request.NeedsSurvivalAid,
                IsFinancialMember = request.IsFinancialMember,
                EstimatedMonthlyContribution = request.EstimatedMonthlyContribution,
                PercentBoost = request.PercentBoost,
                LifetimeContributionOverride = request.LifetimeContributionOverride,
                ReceptionThisYearOverride = request.ReceptionThisYearOverride,
                SeasonAccounting = request.SeasonAccounting
            };
        }

        var result = await placeholderCrewmateService.AddPlaceholderAsync(
            membership.CrewId,
            userId,
            request.Name,
            request.PaymentPlatforms,
            request.EmergencyLevel,
            request.PeopleRepresentedCount,
            request.DisabilityLevel,
            request.IdentityGroups,
            cancellationToken,
            options);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddPlaceholderCrewmateResponse
        {
            Success = result.Success,
            Message = result.Message,
            UserId = result.UserId
        };
    }

    private static bool HasRichPayload(AddPlaceholderCrewmateCommand request) =>
        request.EstimatedMonthlyContribution.HasValue
        || request.PercentBoost.HasValue
        || request.LifetimeContributionOverride.HasValue
        || request.ReceptionThisYearOverride.HasValue
        || request.SeasonAccounting is not null
        || request.InNeedOfAid.HasValue
        || request.NeedsSurvivalAid.HasValue
        || request.IsFinancialMember.HasValue;
}
