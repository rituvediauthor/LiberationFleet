using LiberationFleet.Server.Application.Features.Crews.Commands.UpdateCrew;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public static class CrewUpdateValidator
{
    public static CrewOperationResponse? Validate(
        UpdateCrewCommand request,
        int memberCount,
        out CrewPrivacy privacy,
        out CrewScope scope)
    {
        privacy = CrewPrivacy.Public;
        scope = CrewScope.Online;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("Crew name is required.");
        }

        if (request.MaxSize < 2 || request.MaxSize > 100)
        {
            return Failure("Crew size must be between 2 and 100.");
        }

        if (request.MaxSize < memberCount)
        {
            return Failure($"Crew size cannot be less than the current member count ({memberCount}).");
        }

        if (request.InNeedDefaultThreshold < 0)
        {
            return Failure("In-need threshold cannot be negative.");
        }

        if (request.MemberCycleCapFixedAmount < 0 || request.NonMemberCycleCapFixedAmount < 0)
        {
            return Failure("Cycle cap amounts cannot be negative.");
        }

        if (request.MemberCycleCapMultiplier < 0 || request.NonMemberCycleCapMultiplier < 0)
        {
            return Failure("Cycle cap multipliers cannot be negative.");
        }

        if (request.MinimumCrewmateTenureDaysForAttachments < 0
            || request.MinimumCrewmateTenureDaysForProposals < 0)
        {
            return Failure("Minimum crewmate tenure cannot be negative.");
        }

        if (request.MinimumContributionForAttachments < 0 || request.MinimumContributionForProposals < 0)
        {
            return Failure("Minimum contribution amounts cannot be negative.");
        }

        try
        {
            privacy = Enum.Parse<CrewPrivacy>(request.Privacy, ignoreCase: true);
            scope = Enum.Parse<CrewScope>(request.Scope, ignoreCase: true);
            _ = ParseCycleCapMode(request.MemberCycleCapMode);
            _ = ParseCycleCapMode(request.NonMemberCycleCapMode);
        }
        catch (ArgumentException)
        {
            return Failure("Invalid privacy, location type, or cycle cap mode.");
        }

        if (scope == CrewScope.Local)
        {
            if (string.IsNullOrWhiteSpace(request.ZipCode) || request.ZipCode.Trim().Length != 5 || !request.ZipCode.All(char.IsDigit))
            {
                return Failure("A valid 5-digit zip code is required for local crews.");
            }

            if (!request.RadiusMiles.HasValue || request.RadiusMiles is < 1 or > 500)
            {
                return Failure("Distance must be between 1 and 500 miles.");
            }
        }

        return null;
    }

    public static CycleCapMode ParseCycleCapMode(string value) =>
        Enum.Parse<CycleCapMode>(value, ignoreCase: true);

    private static CrewOperationResponse Failure(string message) =>
        new() { Success = false, Message = message };
}
