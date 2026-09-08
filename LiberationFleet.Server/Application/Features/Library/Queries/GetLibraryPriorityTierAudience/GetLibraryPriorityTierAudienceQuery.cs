using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryPriorityTierAudience;

/// <param name="Visibility">CrewOnly or FleetWide.</param>
/// <param name="Tier">1–5. Exact match, or minimum when <paramref name="MatchMode"/> is MinimumOrHigher.</param>
/// <param name="MatchMode">Exact (consumable stock tier) or MinimumOrHigher (service visibility).</param>
public record GetLibraryPriorityTierAudienceQuery(
    string? Visibility,
    int Tier,
    string? MatchMode) : IRequest<LibraryPriorityTierAudienceResponse>;

public class GetLibraryPriorityTierAudienceQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    LibraryPriorityTierService priorityTierService,
    FleetAvatarVisibilityService fleetAvatarVisibility) : IRequestHandler<GetLibraryPriorityTierAudienceQuery, LibraryPriorityTierAudienceResponse>
{
    public async Task<LibraryPriorityTierAudienceResponse> Handle(
        GetLibraryPriorityTierAudienceQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return Fail("You are not in a crew.");
        }

        var tier = LibraryPriorityTier.ClampTier(request.Tier);
        var fleetWide = string.Equals(request.Visibility, "FleetWide", StringComparison.OrdinalIgnoreCase);
        var minimumOrHigher = string.Equals(
            request.MatchMode,
            "MinimumOrHigher",
            StringComparison.OrdinalIgnoreCase);

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleetWide && fleet is null)
        {
            fleetWide = false;
        }

        var audience = await priorityTierService.GetAudienceMembersForViewerAsync(
            membership.CrewId,
            userId,
            fleetWide,
            cancellationToken);

        var filtered = audience
            .Where(m => minimumOrHigher ? m.Tier >= tier : m.Tier == tier)
            .ToList();

        IReadOnlySet<int>? avatarAllowed = null;
        if (fleetWide && fleet is not null)
        {
            avatarAllowed = await fleetAvatarVisibility.GetAllowedUserIdsAsync(
                fleet,
                filtered.Select(m => m.Membership).ToList(),
                cancellationToken);
        }

        var items = filtered
            .Select(m => new LibraryPriorityTierAudienceMemberDto
            {
                UserId = m.UserId,
                Username = m.Username,
                AvatarResourceId = fleetWide
                    ? FleetAvatarVisibilityService.Filter(m.AvatarResourceId, m.UserId, avatarAllowed)
                    : m.AvatarResourceId,
                Tier = m.Tier
            })
            .ToList();

        return new LibraryPriorityTierAudienceResponse
        {
            Success = true,
            Message = "Audience loaded.",
            Visibility = fleetWide
                ? nameof(LibraryOfferingVisibility.FleetWide)
                : nameof(LibraryOfferingVisibility.CrewOnly),
            Tier = tier,
            MatchMode = minimumOrHigher ? "MinimumOrHigher" : "Exact",
            CrewId = membership.CrewId,
            FleetId = fleetWide ? fleet?.Id : null,
            Items = items
        };
    }

    private static LibraryPriorityTierAudienceResponse Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
