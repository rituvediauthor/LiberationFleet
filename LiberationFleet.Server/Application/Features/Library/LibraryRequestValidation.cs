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

    public static string? ValidateDateRange(
        DateTime neededByStart,
        DateTime neededByEnd,
        string? timeZoneId = null)
    {
        var start = neededByStart.Date;
        var end = neededByEnd.Date;

        if (end < start)
        {
            return "Needed-by end must be on or after the start date.";
        }

        var today = GetLocalCalendarDate(DateTime.UtcNow, timeZoneId);
        if (end < today)
        {
            return "Needed-by end must be today or later.";
        }

        return null;
    }

    public static (DateTime Start, DateTime End) NormalizeDateRange(
        DateTime neededByStart,
        DateTime neededByEnd,
        string? timeZoneId = null)
    {
        var start = DateTime.SpecifyKind(neededByStart.Date, DateTimeKind.Utc);
        var endDate = DateTime.SpecifyKind(neededByEnd.Date, DateTimeKind.Utc);
        var timeZone = TryResolveTimeZone(timeZoneId);
        if (timeZone is null)
        {
            return (start, endDate);
        }

        DateTime exclusiveUtc;
        try
        {
            var localNextMidnight = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Unspecified);
            exclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(localNextMidnight, timeZone);
        }
        catch (ArgumentException)
        {
            return (start, endDate);
        }

        var offsetFromUtcMidnight = exclusiveUtc - endDate.AddDays(1);
        if (offsetFromUtcMidnight <= TimeSpan.Zero || offsetFromUtcMidnight >= TimeSpan.FromDays(1))
        {
            return (start, endDate);
        }

        return (start, DateTime.SpecifyKind(endDate.Add(offsetFromUtcMidnight), DateTimeKind.Utc));
    }

    public static DateTime GetLocalCalendarDate(DateTime utcNow, string? timeZoneId)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var timeZone = TryResolveTimeZone(timeZoneId);
        if (timeZone is null)
        {
            return utc.Date;
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone).Date;
    }

    public static TimeZoneInfo? TryResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var id = timeZoneId.Trim();
        if (TryFindTimeZone(id, out var timeZone))
        {
            return timeZone;
        }

        if (string.Equals(id, "America/New_York", StringComparison.OrdinalIgnoreCase)
            && TryFindTimeZone("Eastern Standard Time", out timeZone))
        {
            return timeZone;
        }

        return null;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo? timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null;
            return false;
        }
    }

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
        int viewerUserId,
        int viewerTier = 1,
        string? viewerCountryCode = null,
        string? viewerZip = null)
    {
        var offering = unit.Offering;
        var tierStock = LibraryOfferingRules.UsesPerTierStock(offering)
            ? LibraryOfferingRules.GetTierStock(offering, viewerTier)
            : offering.RemainingStock;
        var viewer = new LibraryUnitViewerContextDto
        {
            IsHolder = isHolder,
            ActiveRequestId = activeRequest?.Id,
            ActiveRequestStatus = activeRequest?.Status.ToString(),
            MaxRequestQuantity = offering.QuantityNotApplicable
                ? 1
                : Math.Max(1, tierStock ?? 1),
            BrokenPendingConfirmation = unit.BrokenPendingConfirmation,
            IsRetired = unit.IsRetired,
            CanReportBroken = LibraryUnitAccess.CanReportBroken(unit, viewerUserId),
            CanReportFixed = LibraryUnitAccess.CanReportFixed(unit, viewerUserId),
            CanConfirmBroken = LibraryUnitAccess.CanConfirmBroken(unit, viewerUserId),
            CanRecordMaintenance = LibraryUnitAccess.CanRecordMaintenance(unit, viewerUserId),
            CanReportLost = LibraryUnitAccess.CanReportLost(unit, viewerUserId)
        };

        if (unit.IsRetired
            || !LibraryOfferingRules.IsVisibleToViewerTier(offering, viewerTier)
            || !LibraryOfferingRules.IsVisibleToViewerZip(offering, viewerCountryCode, viewerZip))
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
                && LibraryOfferingRules.HasAvailableStockForTier(offering, viewerTier);
            return viewer;
        }

        var canRequest = CanUserRequestUnit(unit.Status, isHolder, hasOpenRequest)
            && !unit.BrokenPendingConfirmation;
        if (LibraryOfferingRules.IsStockBased(offering))
        {
            viewer.CanRequest = canRequest && LibraryOfferingRules.HasAvailableStockForTier(offering, viewerTier);
        }
        else
        {
            viewer.CanRequest = canRequest;
            viewer.MaxRequestQuantity = 1;
        }

        return viewer;
    }
}
