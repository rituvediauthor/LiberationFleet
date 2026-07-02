using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryUnitAccess
{
    public static bool IsDurable(LibraryUnit unit) =>
        unit.Offering.Kind == LibraryOfferingKind.Durable;

    public static bool IsHolder(LibraryUnit unit, int userId) =>
        unit.CurrentPossessorUserId == userId;

    public static bool CanReportBroken(LibraryUnit unit, int userId) =>
        IsDurable(unit)
        && IsHolder(unit, userId)
        && !unit.IsRetired
        && unit.Status == LibraryUnitStatus.Available
        && !unit.BrokenPendingConfirmation;

    public static bool CanReportFixed(LibraryUnit unit, int userId) =>
        IsDurable(unit)
        && IsHolder(unit, userId)
        && !unit.IsRetired
        && unit.Status == LibraryUnitStatus.Broken
        && unit.BrokenPendingConfirmation;

    public static bool CanConfirmBroken(LibraryUnit unit, int userId) =>
        IsDurable(unit)
        && !unit.IsRetired
        && unit.Status == LibraryUnitStatus.Broken
        && unit.BrokenPendingConfirmation;

    public static bool CanRecordMaintenance(LibraryUnit unit, int userId) =>
        IsDurable(unit)
        && IsHolder(unit, userId)
        && !unit.IsRetired
        && unit.Status == LibraryUnitStatus.Available
        && !unit.BrokenPendingConfirmation;

    public static bool CanReportLost(LibraryUnit unit, int userId) =>
        IsDurable(unit)
        && IsHolder(unit, userId)
        && !unit.IsRetired
        && unit.Status == LibraryUnitStatus.Available
        && !unit.BrokenPendingConfirmation;

    public static bool CanEditOffering(LibraryOffering offering, int userId) =>
        offering.CreatorUserId == userId
        && LibraryOfferingRules.IsStockBased(offering)
        && !offering.IsDeleted;

    public static bool CanDeleteOffering(LibraryOffering offering, int userId) =>
        CanEditOffering(offering, userId);
}
