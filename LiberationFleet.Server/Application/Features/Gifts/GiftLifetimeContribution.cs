using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

/// <summary>
/// Rules for when a gift amount is included in a crewmate's lifetime contributions.
/// </summary>
public static class GiftLifetimeContribution
{
    /// <summary>
    /// Verified direct/completed gifts that count toward contribution.
    /// Initiated rows are excluded so middleman flows are not double-counted with their Completed child.
    /// </summary>
    public static bool CountsTowardLifetimeTotal(Gift gift) =>
        gift.CountsTowardContribution
        && gift.Amount > 0m
        && gift.VerificationStatus == GiftVerificationStatus.Verified
        && (gift.Type == GiftType.Direct || gift.Type == GiftType.Completed);

    public static bool CountsTowardLifetimeTotal(
        bool countsTowardContribution,
        GiftType type,
        GiftVerificationStatus verificationStatus,
        decimal amount) =>
        countsTowardContribution
        && amount > 0m
        && verificationStatus == GiftVerificationStatus.Verified
        && (type == GiftType.Direct || type == GiftType.Completed);
}
