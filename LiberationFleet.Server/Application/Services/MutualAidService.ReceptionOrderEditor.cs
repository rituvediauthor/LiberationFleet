using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public partial class MutualAidService
{
    public async Task<ReceptionOrderEditorResponse> GetReceptionOrderEditorAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !CrewRoleAuthorizationService.CanEditReceptionOrder(membership))
        {
            return new ReceptionOrderEditorResponse
            {
                Success = false,
                Message = "Only organizers and accountants can edit the reception order."
            };
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ReceptionOrderEditorResponse { Success = false, Message = "Crew not found." };
        }

        if (!crew.SeasonStarted || crew.CurrentSeasonStartDate is null)
        {
            return new ReceptionOrderEditorResponse
            {
                Success = false,
                Message = "A giving season must be active to edit the reception order.",
                SeasonStarted = false
            };
        }

        return await BuildEditorResponseAsync(crew, cancellationToken);
    }

    public async Task<SaveReceptionOrderEditorResponse> SaveReceptionOrderEditorAsync(
        int userId,
        SaveReceptionOrderEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !CrewRoleAuthorizationService.CanEditReceptionOrder(membership))
        {
            return new SaveReceptionOrderEditorResponse
            {
                Success = false,
                Message = "Only organizers and accountants can edit the reception order."
            };
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || crew.CurrentSeasonStartDate is null)
        {
            return new SaveReceptionOrderEditorResponse
            {
                Success = false,
                Message = "A giving season must be active to edit the reception order."
            };
        }

        var validationError = ValidateEditorRequest(request);
        if (validationError is not null)
        {
            return new SaveReceptionOrderEditorResponse { Success = false, Message = validationError };
        }

        var seasonStart = crew.CurrentSeasonStartDate.Value;
        var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(crew.Id, cancellationToken);
        var memberByUserId = members.ToDictionary(m => m.UserId);

        var existingThresholds = (await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crew.Id, cancellationToken))
            .ToDictionary(t => t.Id);
        // Also load any satisfied rows targeted by id so removals/edits still work.
        foreach (var draft in request.Thresholds.Where(t => t.Id is > 0))
        {
            if (!existingThresholds.ContainsKey(draft.Id!.Value))
            {
                var row = await mutualAidRepository.GetThresholdByIdAsync(draft.Id.Value, cancellationToken);
                if (row is not null && row.CrewId == crew.Id)
                {
                    existingThresholds[row.Id] = row;
                }
            }
        }

        var existingCycles = (await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, seasonStart, cancellationToken))
            .ToDictionary(c => c.Id);

        foreach (var removeId in request.RemovedThresholdIds.Distinct())
        {
            if (!existingThresholds.TryGetValue(removeId, out var toRemove))
            {
                continue;
            }

            if (await mutualAidRepository.HasGiftsForThresholdAsync(removeId, cancellationToken))
            {
                return new SaveReceptionOrderEditorResponse
                {
                    Success = false,
                    Message = $"Cannot remove survival threshold #{removeId} because gifts are linked to it. Set remaining to $0 instead."
                };
            }

            mutualAidRepository.RemoveThreshold(toRemove);
            existingThresholds.Remove(removeId);
        }

        foreach (var removeId in request.RemovedCycleIds.Distinct())
        {
            if (!existingCycles.TryGetValue(removeId, out var toRemove))
            {
                continue;
            }

            if (toRemove.EmergencyRequestId.HasValue || toRemove.EmergencySplitOfferId.HasValue)
            {
                return new SaveReceptionOrderEditorResponse
                {
                    Success = false,
                    Message = $"Cannot remove emergency/payback cycle #{removeId} from this editor."
                };
            }

            if (await mutualAidRepository.HasGiftsForSeasonCycleAsync(removeId, cancellationToken))
            {
                return new SaveReceptionOrderEditorResponse
                {
                    Success = false,
                    Message = $"Cannot remove cycle #{removeId} because gifts are linked to it. Mark it completed instead."
                };
            }

            mutualAidRepository.RemoveSeasonCycle(toRemove);
            existingCycles.Remove(removeId);
        }

        var now = DateTime.UtcNow;
        var assignedMonths = new HashSet<(int UserId, int Year, int Month)>();
        foreach (var row in existingThresholds.Values)
        {
            assignedMonths.Add((row.UserId, row.Year, row.Month));
        }

        // Ids already represented in the editor payload (must not be "reused" as a new row).
        var draftThresholdIds = request.Thresholds
            .Where(t => t.Id is > 0)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Prefer explicit ReceptionOrder; fall back to the request list order (not Id)
        // so the accountant's arrangement is preserved even when orders are missing/tied.
        var orderedThresholdDrafts = request.Thresholds
            .Select((t, index) => (Draft: t, Index: index))
            .OrderBy(x => x.Draft.ReceptionOrder <= 0 ? int.MaxValue : x.Draft.ReceptionOrder)
            .ThenBy(x => x.Index)
            .Select(x => x.Draft)
            .ToList();

        var thresholdRows = new List<MonthlySurvivalThreshold>(orderedThresholdDrafts.Count);
        foreach (var draft in orderedThresholdDrafts)
        {
            if (!memberByUserId.ContainsKey(draft.UserId))
            {
                return new SaveReceptionOrderEditorResponse
                {
                    Success = false,
                    Message = $"Crewmate {draft.UserId} is not an active member."
                };
            }

            var amountNeeded = Math.Max(0m, draft.AmountNeeded);
            var amountRemaining = Math.Max(0m, draft.AmountRemaining);
            if (amountRemaining > amountNeeded)
            {
                amountNeeded = amountRemaining;
            }

            MonthlySurvivalThreshold row;
            if (draft.Id is int id && existingThresholds.TryGetValue(id, out var existing))
            {
                row = existing;
                if (row.UserId != draft.UserId)
                {
                    return new SaveReceptionOrderEditorResponse
                    {
                        Success = false,
                        Message = $"Cannot change the owner of survival threshold #{id}."
                    };
                }
            }
            else
            {
                if (amountNeeded <= 0m)
                {
                    return new SaveReceptionOrderEditorResponse
                    {
                        Success = false,
                        Message = "New survival thresholds need an amount greater than $0."
                    };
                }

                // New editor rows often leave remaining at 0 while setting need. That means
                // "fully open", not "already satisfied" — otherwise the row is created,
                // marked Satisfied, and immediately hidden from this unsatisfied-only editor.
                if (amountRemaining <= 0m)
                {
                    amountRemaining = amountNeeded;
                }

                // Include satisfied/hidden months so we never INSERT over the unique
                // (CrewId, UserId, Year, Month) index for a row the editor did not load.
                var userThresholds = await mutualAidRepository.GetThresholdsForUserAsync(
                    crew.Id,
                    draft.UserId,
                    cancellationToken);
                foreach (var t in userThresholds)
                {
                    assignedMonths.Add((t.UserId, t.Year, t.Month));
                }

                var claimedIds = thresholdRows.Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();
                // Prefer reopening a hidden/satisfied row with no reception applied, instead of
                // INSERTing into a month that already exists (unique index) or inventing gaps.
                var reusable = userThresholds
                    .Where(t => !draftThresholdIds.Contains(t.Id) && !claimedIds.Contains(t.Id))
                    .Where(t => t.ReceivedAmount <= 0m)
                    .OrderByDescending(t => t.Year == now.Year && t.Month == now.Month)
                    .ThenByDescending(t => t.Satisfied)
                    .ThenByDescending(t => t.Year)
                    .ThenByDescending(t => t.Month)
                    .FirstOrDefault();

                if (reusable is not null)
                {
                    row = reusable;
                    if (!existingThresholds.ContainsKey(row.Id))
                    {
                        existingThresholds[row.Id] = row;
                    }
                }
                else
                {
                    var (year, month) = AllocateFreeThresholdMonthForUser(
                        draft.UserId,
                        now.Year,
                        now.Month,
                        assignedMonths);

                    if (assignedMonths.Contains((draft.UserId, year, month)))
                    {
                        return new SaveReceptionOrderEditorResponse
                        {
                            Success = false,
                            Message =
                                $"Could not allocate a free survival-threshold month for this crewmate. " +
                                "Edit an existing row instead of adding another."
                        };
                    }

                    row = new MonthlySurvivalThreshold
                    {
                        CrewId = crew.Id,
                        UserId = draft.UserId,
                        Year = year,
                        Month = month
                    };
                    await mutualAidRepository.AddThresholdAsync(row, cancellationToken);
                }
            }

            row.ThresholdAmount = amountNeeded;
            row.ReceivedAmount = Math.Clamp(amountNeeded - amountRemaining, 0m, amountNeeded);
            row.IsCustomAmount = true;
            row.Satisfied = row.ReceivedAmount >= row.ThresholdAmount;
            assignedMonths.Add((row.UserId, row.Year, row.Month));
            thresholdRows.Add(row);
        }

        for (var i = 0; i < thresholdRows.Count; i++)
        {
            thresholdRows[i].ReceptionOrderPosition = i;
        }

        var orderedCycleDrafts = request.Cycles
            .Select((c, index) => (Draft: c, Index: index))
            .OrderBy(x => x.Draft.ReceptionOrder <= 0 ? int.MaxValue : x.Draft.ReceptionOrder)
            .ThenBy(x => x.Index)
            .Select(x => x.Draft)
            .ToList();

        var pendingPrimaryUserIds = new HashSet<int>();
        var cycleRows = new List<SeasonCycle>(orderedCycleDrafts.Count);
        foreach (var draft in orderedCycleDrafts)
        {
            if (!memberByUserId.TryGetValue(draft.UserId, out var targetMembership))
            {
                return new SaveReceptionOrderEditorResponse
                {
                    Success = false,
                    Message = $"Crewmate {draft.UserId} is not an active member."
                };
            }

            var amount = Math.Max(0m, draft.Amount);
            var amountRemaining = Math.Max(0m, draft.AmountRemaining);
            if (amountRemaining > amount)
            {
                amount = amountRemaining;
            }

            var cycleReceived = Math.Clamp(amount - amountRemaining, 0m, amount);
            var isActive = draft.IsActive && !draft.CycleCompleted;
            var completed = draft.CycleCompleted || (amount > 0m && cycleReceived >= amount);

            SeasonCycle cycle;
            if (draft.Id is int id && existingCycles.TryGetValue(id, out var existing))
            {
                cycle = existing;
                if (cycle.UserId != draft.UserId)
                {
                    return new SaveReceptionOrderEditorResponse
                    {
                        Success = false,
                        Message = $"Cannot change the owner of cycle #{id}."
                    };
                }

                if (cycle.EmergencyRequestId is null && cycle.EmergencySplitOfferId is null)
                {
                    pendingPrimaryUserIds.Add(cycle.UserId);
                }
            }
            else
            {
                var hasPrimary = pendingPrimaryUserIds.Contains(draft.UserId)
                    || existingCycles.Values.Any(c =>
                        c.UserId == draft.UserId
                        && c.EmergencyRequestId is null
                        && c.EmergencySplitOfferId is null);
                if (hasPrimary)
                {
                    return new SaveReceptionOrderEditorResponse
                    {
                        Success = false,
                        Message = $"{UserDisplay.Name(targetMembership.User)} already has a primary cycle. Edit that row instead of adding another."
                    };
                }

                var isMember = await IsFinancialMemberAsync(
                    draft.UserId,
                    crew.Id,
                    targetMembership,
                    cancellationToken);
                var defaultCap = isMember ? crew.SeasonMemberCycleCap : crew.SeasonNonMemberCycleCap;
                if (defaultCap <= 0m)
                {
                    defaultCap = amount > 0m ? amount : 1m;
                }

                cycle = new SeasonCycle
                {
                    CrewId = crew.Id,
                    UserId = draft.UserId,
                    SeasonStartDate = seasonStart,
                    CycleCapAtStart = amount > 0m ? amount : defaultCap,
                    CapIsProvisional = false,
                    UsesSegmentCap = amount > 0m,
                    PriorityScoreAtSeasonStart = targetMembership.CurrentPriorityScore
                };
                await mutualAidRepository.AddSeasonCycleAsync(cycle, cancellationToken);
                pendingPrimaryUserIds.Add(draft.UserId);
            }

            if (amount > 0m)
            {
                cycle.UsesSegmentCap = true;
                cycle.CycleCapAtStart = amount;
                cycle.CapIsProvisional = false;
            }

            cycle.CycleReceived = cycleReceived;
            cycle.HasCycleStarted = isActive;
            cycle.CycleCompleted = completed;
            if (completed)
            {
                cycle.CycleCompletedAt ??= now;
                cycle.HasCycleStarted = false;
                cycle.CycleCapAtCompletion = amount > 0m ? amount : cycle.CycleCapAtStart;
            }
            else
            {
                cycle.CycleCompletedAt = null;
            }

            var allUserThresholds = await mutualAidRepository.GetThresholdsForUserAsync(
                crew.Id,
                cycle.UserId,
                cancellationToken);
            var survivalReceived = allUserThresholds.Sum(t => t.ReceivedAmount);
            cycle.SurvivalThresholdReceived = survivalReceived;
            cycle.TotalReceptionAmount = cycle.CycleReceived + survivalReceived;
            cycleRows.Add(cycle);
        }

        for (var i = 0; i < cycleRows.Count; i++)
        {
            cycleRows[i].ReceptionOrderPosition = i;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RefreshHasCycleStartedForCrewAsync(crew, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var editor = await BuildEditorResponseAsync(crew, cancellationToken);
        return new SaveReceptionOrderEditorResponse
        {
            Success = true,
            Message = "Reception order saved.",
            Editor = editor
        };
    }

    private async Task<ReceptionOrderEditorResponse> BuildEditorResponseAsync(
        Crew crew,
        CancellationToken cancellationToken)
    {
        var seasonStart = crew.CurrentSeasonStartDate!.Value;
        var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(crew.Id, cancellationToken);
        var cycles = (await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, seasonStart, cancellationToken))
            .OrderBy(c => c.ReceptionOrderPosition)
            .ThenBy(c => c.Id)
            .ToList();
        var primaryUserIds = cycles
            .Where(c => c.EmergencyRequestId is null && c.EmergencySplitOfferId is null)
            .Select(c => c.UserId)
            .ToHashSet();

        var thresholds = (await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crew.Id, cancellationToken))
            .OrderBy(t => t.ReceptionOrderPosition)
            .ThenBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ThenBy(t => t.Id)
            .ToList();

        var thresholdDtos = new List<ReceptionOrderEditorThresholdDto>(thresholds.Count);
        for (var i = 0; i < thresholds.Count; i++)
        {
            var t = thresholds[i];
            thresholdDtos.Add(new ReceptionOrderEditorThresholdDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Username = UserDisplay.Name(t.User),
                AmountNeeded = t.ThresholdAmount,
                AmountRemaining = Math.Max(0m, t.ThresholdAmount - t.ReceivedAmount),
                ReceptionOrder = i + 1
            });
        }

        var cycleDtos = new List<ReceptionOrderEditorCycleDto>(cycles.Count);
        for (var i = 0; i < cycles.Count; i++)
        {
            var c = cycles[i];
            var isPrimary = c.EmergencyRequestId is null && c.EmergencySplitOfferId is null;
            var isMember = members.FirstOrDefault(m => m.UserId == c.UserId) is { } m
                && await IsFinancialMemberAsync(c.UserId, crew.Id, m, cancellationToken);
            var amount = EmergencySplitService.ResolveSegmentCap(
                c,
                isMember,
                crew.SeasonMemberCycleCap,
                crew.SeasonNonMemberCycleCap);
            if (amount <= 0m)
            {
                amount = Math.Max(c.CycleCapAtStart, c.CycleReceived);
            }

            var remaining = Math.Max(0m, amount - c.CycleReceived);
            var canRemove = isPrimary
                && !await mutualAidRepository.HasGiftsForSeasonCycleAsync(c.Id, cancellationToken);

            cycleDtos.Add(new ReceptionOrderEditorCycleDto
            {
                Id = c.Id,
                UserId = c.UserId,
                Username = UserDisplay.Name(c.User),
                Amount = amount,
                AmountRemaining = remaining,
                IsActive = c.HasCycleStarted && !c.CycleCompleted,
                IsFrozen = !c.HasCycleStarted && !c.CycleCompleted,
                ReceptionOrder = i + 1,
                CycleCompleted = c.CycleCompleted,
                IsEmergencyCycle = c.EmergencyRequestId.HasValue,
                IsPaybackCycle = c.EmergencySplitOfferId.HasValue,
                IsPrimary = isPrimary,
                CanRemove = canRemove
            });
        }

        return new ReceptionOrderEditorResponse
        {
            Success = true,
            Message = "Reception order editor loaded.",
            SeasonStarted = true,
            SeasonStartDate = seasonStart,
            Members = members
                .OrderBy(m => UserDisplay.Name(m.User), StringComparer.OrdinalIgnoreCase)
                .Select(m => new ReceptionOrderEditorMemberDto
                {
                    UserId = m.UserId,
                    Username = UserDisplay.Name(m.User),
                    HasPrimaryCycle = primaryUserIds.Contains(m.UserId)
                })
                .ToList(),
            Thresholds = thresholdDtos,
            Cycles = cycleDtos
        };
    }

    private static string? ValidateEditorRequest(SaveReceptionOrderEditorRequest request)
    {
        foreach (var t in request.Thresholds)
        {
            if (t.UserId <= 0)
            {
                return "Each survival threshold needs an owner.";
            }

            if (t.AmountNeeded < 0 || t.AmountRemaining < 0)
            {
                return "Survival threshold amounts cannot be negative.";
            }

            if (t.Id is null && t.AmountNeeded <= 0m)
            {
                return "New survival thresholds need an amount greater than $0.";
            }
        }

        foreach (var c in request.Cycles)
        {
            if (c.UserId <= 0)
            {
                return "Each cycle needs an owner.";
            }

            if (c.Amount < 0 || c.AmountRemaining < 0)
            {
                return "Cycle amounts cannot be negative.";
            }
        }

        return null;
    }

    private static (int Year, int Month) AllocateFreeThresholdMonthForUser(
        int userId,
        int startYear,
        int startMonth,
        ISet<(int UserId, int Year, int Month)> assignedMonths)
    {
        var year = startYear;
        var month = startMonth;
        for (var i = 0; i < 120; i++)
        {
            if (!assignedMonths.Contains((userId, year, month)))
            {
                return (year, month);
            }

            month--;
            if (month < 1)
            {
                month = 12;
                year--;
            }
        }

        return (startYear, startMonth);
    }
}
