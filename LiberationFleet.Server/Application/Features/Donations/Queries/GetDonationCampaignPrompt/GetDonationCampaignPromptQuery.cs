using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Features.Donations.Queries.GetDonationCampaignPrompt;

public record GetDonationCampaignPromptQuery(string MessageVariant) : IRequest<DonationCampaignPromptResponse>;

public class DonationCampaignPromptResponse
{
    public bool Show { get; set; }
    public string MessageVariant { get; set; } = "crew";
    public string Message { get; set; } = string.Empty;
    public bool DonationsEnabled { get; set; }
}

public class GetDonationCampaignPromptQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    ICrewMembershipRepository membershipRepository,
    IMutualAidService mutualAidService,
    IOptions<StripeDonationOptions> stripeOptions) : IRequestHandler<GetDonationCampaignPromptQuery, DonationCampaignPromptResponse>
{
    public const string CrewMessage =
        "Please consider the ways the Liberation App has strengthened your community. The mutual aid we empower is valuable. Please join the crewmates who make this possible by donating whatever amount you can comfortably afford.";

    public const string FleetMessage =
        "Your support is what keeps this fleet afloat. So if you can afford to give any amount, please consider making a donation.";

    public async Task<DonationCampaignPromptResponse> Handle(
        GetDonationCampaignPromptQuery request,
        CancellationToken cancellationToken)
    {
        var variant = string.Equals(request.MessageVariant, "fleet", StringComparison.OrdinalIgnoreCase)
            ? "fleet"
            : "crew";
        var message = variant == "fleet" ? FleetMessage : CrewMessage;
        var donationsEnabled = stripeOptions.Value.IsConfigured;

        if (!currentUser.UserId.HasValue)
        {
            return Hidden(variant, message, donationsEnabled);
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null || user.EmergencyLevel > 0)
        {
            return Hidden(variant, message, donationsEnabled);
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(user.Id, cancellationToken);
        var avgContributions = 0m;
        var isFinancialMember = false;
        var estimatedMonthly = membership?.EstimatedMonthlyContribution ?? 0m;

        if (membership is not null)
        {
            var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                user.Id,
                membership.CrewId,
                membership.Crew?.CurrentSeasonStartDate,
                cancellationToken);
            avgContributions = giftStats.AverageMonthlyContributions;
            isFinancialMember = await mutualAidService.IsFinancialMemberAsync(
                user.Id,
                membership.CrewId,
                membership,
                cancellationToken);
        }

        var contributes = isFinancialMember || avgContributions > 0m || estimatedMonthly > 0m;
        var utcNow = DateTime.UtcNow;
        var inCharityHighSeason = IsCharityHighSeason(utcNow);

        if (inCharityHighSeason)
        {
            // Everyone not in emergency during Dec 20–Jan 3 (once per high season window).
            var seasonStart = GetCharityHighSeasonStart(utcNow);
            if (user.LastDonationCampaignPromptAt.HasValue
                && user.LastDonationCampaignPromptAt.Value >= seasonStart)
            {
                return Hidden(variant, message, donationsEnabled);
            }

            return new DonationCampaignPromptResponse
            {
                Show = true,
                MessageVariant = variant,
                Message = message,
                DonationsEnabled = donationsEnabled
            };
        }

        if (!contributes)
        {
            return Hidden(variant, message, donationsEnabled);
        }

        var intervalDays = user.InNeedOfAid ? 60 : 30;
        var lastShown = user.LastDonationCampaignPromptAt;
        if (lastShown.HasValue && lastShown.Value > utcNow.AddDays(-intervalDays))
        {
            return Hidden(variant, message, donationsEnabled);
        }

        return new DonationCampaignPromptResponse
        {
            Show = true,
            MessageVariant = variant,
            Message = message,
            DonationsEnabled = donationsEnabled
        };
    }

    /// <summary>Dec 20 through Jan 3 inclusive (UTC calendar dates).</summary>
    public static bool IsCharityHighSeason(DateTime utcNow)
    {
        var d = DateOnly.FromDateTime(utcNow);
        if (d.Month == 12 && d.Day >= 20)
        {
            return true;
        }

        if (d.Month == 1 && d.Day <= 3)
        {
            return true;
        }

        return false;
    }

    public static DateTime GetCharityHighSeasonStart(DateTime utcNow)
    {
        var year = utcNow.Month == 1 ? utcNow.Year - 1 : utcNow.Year;
        return new DateTime(year, 12, 20, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DonationCampaignPromptResponse Hidden(string variant, string message, bool enabled) =>
        new()
        {
            Show = false,
            MessageVariant = variant,
            Message = message,
            DonationsEnabled = enabled
        };
}
