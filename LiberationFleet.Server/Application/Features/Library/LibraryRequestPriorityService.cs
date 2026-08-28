using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public class LibraryRequestPriorityService(IMutualAidService mutualAidService)
{
    public async Task ApplyPossessorPriorityAsync(
        IList<LibraryRequestListItemDto> items,
        IReadOnlyList<LibraryRequest> sourceRequests,
        int crewId,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var openByUnit = sourceRequests
            .Where(r => r.Status == LibraryRequestStatus.Open && r.NeededByStart > utcNow)
            .GroupBy(r => r.UnitId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in items)
        {
            item.RequesterPriorityScore = await GetAlignedPriorityScoreAsync(
                item.RequesterUserId,
                crewId,
                cancellationToken);
        }

        foreach (var unitRequests in openByUnit.Values)
        {
            if (unitRequests.Count <= 1)
            {
                var sole = items.First(i => i.RequestId == unitRequests[0].Id);
                sole.HasHighestPriorityAmongOpenRequests = true;
                continue;
            }

            var highest = unitRequests
                .Select(r => (Request: r, Score: items.First(i => i.RequestId == r.Id).RequesterPriorityScore ?? 0m))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Request.CreatedAt)
                .First();

            foreach (var unitRequest in unitRequests)
            {
                var dto = items.First(i => i.RequestId == unitRequest.Id);
                dto.HasHighestPriorityAmongOpenRequests = unitRequest.Id == highest.Request.Id;
                if (unitRequest.Id != highest.Request.Id)
                {
                    dto.HigherPriorityRequestId = highest.Request.Id;
                    dto.HigherPriorityRequesterUsername = highest.Request.RequesterUser?.Username ?? string.Empty;
                }
            }
        }
    }

    public async Task ApplyPossessorPriorityToDetailAsync(
        LibraryRequestDetailDto detail,
        LibraryRequest request,
        IReadOnlyList<LibraryRequest> unitOpenRequests,
        int crewId,
        CancellationToken cancellationToken)
    {
        detail.RequesterPriorityScore = await GetAlignedPriorityScoreAsync(
            request.RequesterUserId,
            crewId,
            cancellationToken);

        var utcNow = DateTime.UtcNow;
        var open = unitOpenRequests
            .Where(r => r.Status == LibraryRequestStatus.Open && r.NeededByStart > utcNow)
            .ToList();
        if (open.Count <= 1)
        {
            detail.HasHighestPriorityAmongOpenRequests = true;
            return;
        }

        var scores = new Dictionary<int, decimal>();
        foreach (var openRequest in open)
        {
            scores[openRequest.Id] = openRequest.Id == request.Id
                ? detail.RequesterPriorityScore.Value
                : await GetAlignedPriorityScoreAsync(
                    openRequest.RequesterUserId,
                    crewId,
                    cancellationToken);
        }

        var highest = open
            .OrderByDescending(r => scores[r.Id])
            .ThenBy(r => r.CreatedAt)
            .First();

        detail.HasHighestPriorityAmongOpenRequests = request.Id == highest.Id;
        if (request.Id != highest.Id)
        {
            detail.HigherPriorityRequestId = highest.Id;
            detail.HigherPriorityRequesterUsername = highest.RequesterUser?.Username ?? string.Empty;
        }
    }

    private async Task<decimal> GetAlignedPriorityScoreAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken)
    {
        var score = await mutualAidService.GetPriorityScoreForUserAsync(
            userId,
            crewId,
            cancellationToken,
            assumeInNeedNonOrganizerForLot: true);
        // Match profile/crewmate display rounding ((int)Math.Round(...)).
        return Math.Round(score, MidpointRounding.AwayFromZero);
    }
}
