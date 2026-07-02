using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

internal static class LibraryRequestValidation
{
    private const int MaxPurposePreviewLength = 200;

    public static string NormalizePurposePreview(string purposePreview)
    {
        var preview = purposePreview.Trim();
        if (preview.Length > MaxPurposePreviewLength)
        {
            preview = preview[..MaxPurposePreviewLength];
        }

        return preview;
    }

    public static string? ValidateDateRange(DateTime neededByStart, DateTime neededByEnd)
    {
        var start = DateTime.SpecifyKind(neededByStart.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(neededByEnd.Date, DateTimeKind.Utc);

        if (end < start)
        {
            return "Needed-by end must be on or after the start date.";
        }

        var today = DateTime.UtcNow.Date;
        if (end < today)
        {
            return "Needed-by end must be today or later.";
        }

        return null;
    }

    public static (DateTime Start, DateTime End) NormalizeDateRange(DateTime neededByStart, DateTime neededByEnd) =>
        (
            DateTime.SpecifyKind(neededByStart.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(neededByEnd.Date, DateTimeKind.Utc));

    public static bool DateRangesOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2) =>
        start1 <= end2 && start2 <= end1;

    public const string OverlappingRequestDatesMessage =
        "Those dates overlap with another open request for this item.";

    public static bool CanUserRequestUnit(LibraryUnitStatus status, bool isHolder, bool hasOpenRequest) =>
        !isHolder
        && status == LibraryUnitStatus.Available
        && !hasOpenRequest;

    public static LibraryUnitViewerContextDto BuildViewerContext(
        LibraryUnit unit,
        bool isHolder,
        bool hasOpenRequest,
        LibraryRequest? activeRequest,
        int viewerUserId)
    {
        var offering = unit.Offering;
        var viewer = new LibraryUnitViewerContextDto
        {
            IsHolder = isHolder,
            ActiveRequestId = activeRequest?.Id,
            ActiveRequestStatus = activeRequest?.Status.ToString(),
            MaxRequestQuantity = offering.QuantityNotApplicable
                ? 1
                : Math.Max(1, offering.RemainingStock ?? 1),
            BrokenPendingConfirmation = unit.BrokenPendingConfirmation,
            IsRetired = unit.IsRetired,
            CanReportBroken = LibraryUnitAccess.CanReportBroken(unit, viewerUserId),
            CanReportFixed = LibraryUnitAccess.CanReportFixed(unit, viewerUserId),
            CanConfirmBroken = LibraryUnitAccess.CanConfirmBroken(unit, viewerUserId),
            CanRecordMaintenance = LibraryUnitAccess.CanRecordMaintenance(unit, viewerUserId),
            CanReportLost = LibraryUnitAccess.CanReportLost(unit, viewerUserId)
        };

        if (unit.IsRetired)
        {
            viewer.CanRequest = false;
            viewer.CanRecordAcquisition = false;
            return viewer;
        }

        if (LibraryOfferingRules.IsOnDemand(offering))
        {
            viewer.CanRequest = false;
            viewer.CanRecordAcquisition = !isHolder
                && unit.Status == LibraryUnitStatus.Available
                && LibraryOfferingRules.HasAvailableStock(offering);
            return viewer;
        }

        var canRequest = CanUserRequestUnit(unit.Status, isHolder, hasOpenRequest)
            && !unit.BrokenPendingConfirmation;
        if (LibraryOfferingRules.IsStockBased(offering))
        {
            viewer.CanRequest = canRequest && LibraryOfferingRules.HasAvailableStock(offering);
        }
        else
        {
            viewer.CanRequest = canRequest;
            viewer.MaxRequestQuantity = 1;
        }

        return viewer;
    }
}
