using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryOfferingRules
{
    public static bool IsStockBased(LibraryOffering offering) =>
        offering.Kind is LibraryOfferingKind.Consumable
            or LibraryOfferingKind.Service
            or LibraryOfferingKind.Digital;

    public static bool IsDigital(LibraryOffering offering) =>
        offering.Kind == LibraryOfferingKind.Digital;

    public static bool IsOnDemand(LibraryOffering offering) =>
        offering.FulfillmentMode == LibraryFulfillmentMode.OnDemand;

    public static bool UsesPerTierStock(LibraryOffering offering) =>
        offering.Kind == LibraryOfferingKind.Consumable && !offering.QuantityNotApplicable;

    public static int? GetTierStock(LibraryOffering offering, int tier)
    {
        return LibraryPriorityTier.ClampTier(tier) switch
        {
            1 => offering.RemainingStockTier1,
            2 => offering.RemainingStockTier2,
            3 => offering.RemainingStockTier3,
            4 => offering.RemainingStockTier4,
            _ => offering.RemainingStockTier5
        };
    }

    public static void SetTierStock(LibraryOffering offering, int tier, int? value)
    {
        switch (LibraryPriorityTier.ClampTier(tier))
        {
            case 1:
                offering.RemainingStockTier1 = value;
                break;
            case 2:
                offering.RemainingStockTier2 = value;
                break;
            case 3:
                offering.RemainingStockTier3 = value;
                break;
            case 4:
                offering.RemainingStockTier4 = value;
                break;
            default:
                offering.RemainingStockTier5 = value;
                break;
        }
    }

    public static void SetTierStocks(
        LibraryOffering offering,
        int tier1,
        int tier2,
        int tier3,
        int tier4,
        int tier5)
    {
        offering.RemainingStockTier1 = Math.Max(0, tier1);
        offering.RemainingStockTier2 = Math.Max(0, tier2);
        offering.RemainingStockTier3 = Math.Max(0, tier3);
        offering.RemainingStockTier4 = Math.Max(0, tier4);
        offering.RemainingStockTier5 = Math.Max(0, tier5);
        SyncAggregateRemainingStock(offering);
    }

    public static void ClearTierStocks(LibraryOffering offering)
    {
        offering.RemainingStockTier1 = null;
        offering.RemainingStockTier2 = null;
        offering.RemainingStockTier3 = null;
        offering.RemainingStockTier4 = null;
        offering.RemainingStockTier5 = null;
    }

    public static void SyncAggregateRemainingStock(LibraryOffering offering)
    {
        if (!UsesPerTierStock(offering))
        {
            return;
        }

        offering.RemainingStock =
            (offering.RemainingStockTier1 ?? 0)
            + (offering.RemainingStockTier2 ?? 0)
            + (offering.RemainingStockTier3 ?? 0)
            + (offering.RemainingStockTier4 ?? 0)
            + (offering.RemainingStockTier5 ?? 0);
    }

    public static bool IsOutOfStock(LibraryOffering offering) =>
        offering.IsOutOfStock
        || (IsStockBased(offering)
            && !offering.QuantityNotApplicable
            && offering.RemainingStock is <= 0);

    public static bool IsOutOfStockForTier(LibraryOffering offering, int viewerTier)
    {
        if (offering.IsOutOfStock)
        {
            return true;
        }

        if (!UsesPerTierStock(offering))
        {
            return IsOutOfStock(offering);
        }

        return GetTierStock(offering, viewerTier) is not > 0;
    }

    public static bool HasAvailableStock(LibraryOffering offering)
    {
        if (IsOutOfStock(offering))
        {
            return false;
        }

        if (!IsStockBased(offering))
        {
            return true;
        }

        return offering.QuantityNotApplicable || offering.RemainingStock is > 0;
    }

    public static bool HasAvailableStockForTier(LibraryOffering offering, int viewerTier)
    {
        if (offering.Kind == LibraryOfferingKind.Service
            && viewerTier < LibraryPriorityTier.ClampTier(offering.MinimumViewerTier))
        {
            return false;
        }

        if (!UsesPerTierStock(offering))
        {
            return HasAvailableStock(offering);
        }

        if (offering.IsOutOfStock)
        {
            return false;
        }

        return GetTierStock(offering, viewerTier) is > 0;
    }

    public static bool HasSufficientStock(LibraryOffering offering, int quantity) =>
        HasAvailableStock(offering)
        && (offering.QuantityNotApplicable || (offering.RemainingStock is int stock && stock >= quantity));

    public static bool HasSufficientStockForTier(LibraryOffering offering, int quantity, int viewerTier)
    {
        if (offering.Kind == LibraryOfferingKind.Service
            && viewerTier < LibraryPriorityTier.ClampTier(offering.MinimumViewerTier))
        {
            return false;
        }

        if (!UsesPerTierStock(offering))
        {
            return HasSufficientStock(offering, quantity);
        }

        if (offering.IsOutOfStock)
        {
            return false;
        }

        return GetTierStock(offering, viewerTier) is int stock && stock >= quantity;
    }

    public static void ReduceStock(LibraryOffering offering, int quantity)
    {
        if (offering.QuantityNotApplicable || offering.RemainingStock is null)
        {
            return;
        }

        offering.RemainingStock -= quantity;
    }

    public static void ReduceStockForTier(LibraryOffering offering, int quantity, int viewerTier)
    {
        if (offering.QuantityNotApplicable)
        {
            return;
        }

        if (!UsesPerTierStock(offering))
        {
            ReduceStock(offering, quantity);
            return;
        }

        var tier = LibraryPriorityTier.ClampTier(viewerTier);
        var current = GetTierStock(offering, tier) ?? 0;
        SetTierStock(offering, tier, Math.Max(0, current - quantity));
        SyncAggregateRemainingStock(offering);
    }

    public static bool IsVisibleToViewerTier(LibraryOffering offering, int viewerTier)
    {
        if (offering.Kind != LibraryOfferingKind.Service)
        {
            return true;
        }

        return viewerTier >= LibraryPriorityTier.ClampTier(offering.MinimumViewerTier);
    }

    public static decimal CalculateCreatorContributionAmount(LibraryOffering offering, int quantity) =>
        offering.ValuePerUnit * quantity;

    public static decimal CalculateCompleterDurableContributionAmount(LibraryOffering offering, int quantity) =>
        0.10m * offering.ValuePerUnit * quantity;

    public static bool ShouldCreditCreatorForStockUse(LibraryOffering offering, int recipientUserId) =>
        recipientUserId != offering.CreatorUserId;

    public static bool ShouldCreditCreatorForFirstDurableTransfer(
        LibraryUnit unit,
        LibraryOffering offering,
        int newPossessorUserId) =>
        !unit.CreatorContributionCredited
        && newPossessorUserId != offering.CreatorUserId;
}
