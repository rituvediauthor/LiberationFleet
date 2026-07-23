using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetReceptionOrder;

public sealed class FleetReceptionOrderEntryDto : ReceptionOrderEntryDto
{
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
}

public record GetFleetReceptionOrderQuery(
    int Limit = 30,
    bool ForRecordGift = true,
    bool ExcludeSelfAsRecipient = true) : IRequest<IReadOnlyList<FleetReceptionOrderEntryDto>>;

public class GetFleetReceptionOrderQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService) : IRequestHandler<GetFleetReceptionOrderQuery, IReadOnlyList<FleetReceptionOrderEntryDto>>
{
    public async Task<IReadOnlyList<FleetReceptionOrderEntryDto>> Handle(
        GetFleetReceptionOrderQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<FleetReceptionOrderEntryDto>();
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return Array.Empty<FleetReceptionOrderEntryDto>();
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return Array.Empty<FleetReceptionOrderEntryDto>();
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        if (fleetCrews.Count == 0)
        {
            return Array.Empty<FleetReceptionOrderEntryDto>();
        }

        var fleetMembers = new List<CrewMemberPlatforms>();
        var seenUserIds = new HashSet<int>();
        foreach (var fleetCrew in fleetCrews)
        {
            var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(fleetCrew.CrewId, cancellationToken);
            foreach (var member in members)
            {
                if (seenUserIds.Add(member.UserId))
                {
                    fleetMembers.Add(CrewPaymentPlatformService.MapCrewMemberPlatforms(member));
                }
            }
        }

        var survivalEntries = new List<FleetReceptionOrderEntryDto>();
        var representativeEntries = new List<FleetReceptionOrderEntryDto>();
        var cycleEntries = new List<FleetReceptionOrderEntryDto>();

        foreach (var fleetCrew in fleetCrews)
        {
            var crewName = fleetCrew.Crew?.Name ?? "Crew";
            var entries = await mutualAidService.GetReceptionOrderForCrewAsGiverAsync(
                fleetCrew.CrewId,
                userId,
                forRecordGift: request.ForRecordGift,
                excludeSelfAsRecipient: request.ExcludeSelfAsRecipient,
                additionalMembersForMiddlemen: fleetMembers,
                cancellationToken);

            foreach (var entry in entries.Where(e => e.IsUnlimitedNeed || e.AmountNeeded > 0))
            {
                var mapped = MapEntry(entry, fleetCrew.CrewId, crewName);
                if (string.Equals(entry.EntryType, "survivalThreshold", StringComparison.OrdinalIgnoreCase))
                {
                    survivalEntries.Add(mapped);
                }
                else if (string.Equals(entry.EntryType, "representative", StringComparison.OrdinalIgnoreCase))
                {
                    representativeEntries.Add(mapped);
                }
                else
                {
                    cycleEntries.Add(mapped);
                }
            }
        }

        var limit = request.Limit <= 0 ? 30 : request.Limit;
        return survivalEntries
            .Concat(representativeEntries)
            .Concat(cycleEntries)
            .Take(limit)
            .ToList();
    }

    private static FleetReceptionOrderEntryDto MapEntry(
        ReceptionOrderEntryDto entry,
        int crewId,
        string crewName) => new()
    {
        UserId = entry.UserId,
        Username = entry.Username,
        AmountNeeded = entry.AmountNeeded,
        EntryType = entry.EntryType,
        ThresholdId = entry.ThresholdId,
        CycleUserId = entry.CycleUserId,
        SeasonCycleId = entry.SeasonCycleId,
        MiddlemanOptions = entry.MiddlemanOptions,
        DefaultMiddlemanId = entry.DefaultMiddlemanId,
        NoSuitableMiddleman = entry.NoSuitableMiddleman,
        GiverPlatformIds = entry.GiverPlatformIds,
        RecipientPlatformIds = entry.RecipientPlatformIds,
        CommonPlatformIds = entry.CommonPlatformIds,
        RecipientPreferredPlatformName = entry.RecipientPreferredPlatformName,
        RecipientPreferredPlatformHandle = entry.RecipientPreferredPlatformHandle,
        RecipientPlatformAccounts = entry.RecipientPlatformAccounts,
        HasUnverifiedPending = entry.HasUnverifiedPending,
        PendingUnverifiedAmount = entry.PendingUnverifiedAmount,
        IsUnlimitedNeed = entry.IsUnlimitedNeed,
        CrewId = crewId,
        CrewName = crewName
    };
}
