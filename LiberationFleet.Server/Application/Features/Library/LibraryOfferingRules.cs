using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryOfferingRules
{
    public static bool IsStockBased(LibraryOffering offering) =>
        offering.Kind is LibraryOfferingKind.Consumable or LibraryOfferingKind.Service;

    public static bool IsOnDemand(LibraryOffering offering) =>
        offering.FulfillmentMode == LibraryFulfillmentMode.OnDemand;

    public static bool IsOutOfStock(LibraryOffering offering) =>
        offering.IsOutOfStock
        || (IsStockBased(offering)
            && !offering.QuantityNotApplicable
            && offering.RemainingStock is <= 0);

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

    public static bool HasSufficientStock(LibraryOffering offering, int quantity) =>
        HasAvailableStock(offering)
        && (offering.QuantityNotApplicable || (offering.RemainingStock is int stock && stock >= quantity));

    public static void ReduceStock(LibraryOffering offering, int quantity)
    {
        if (offering.QuantityNotApplicable || offering.RemainingStock is null)
        {
            return;
        }

        offering.RemainingStock -= quantity;
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
