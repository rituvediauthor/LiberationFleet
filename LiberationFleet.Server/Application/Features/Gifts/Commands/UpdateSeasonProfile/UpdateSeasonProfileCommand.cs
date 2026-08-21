using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.UpdateSeasonProfile;

public class UpdateSeasonProfileCommand : IRequest<SeasonProfileResponse>
{
    public List<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = [];
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public List<string> IdentityGroups { get; set; } = [];
    public bool NeedsSurvivalAid { get; set; }
    public decimal EstimatedMonthlyContribution { get; set; }
}
