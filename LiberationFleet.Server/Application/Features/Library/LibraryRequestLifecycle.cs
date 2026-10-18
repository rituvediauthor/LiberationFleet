using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// Durable/Service requests stay open through the due date; consumables and digital
/// stay open until fulfilled/denied/cancelled (no date-gated expiry).
/// </summary>
public static class LibraryRequestLifecycle
{
    public static bool IsDateGated(LibraryOfferingKind kind) =>
        kind is LibraryOfferingKind.Durable or LibraryOfferingKind.Service;

    public static bool IsDateGated(LibraryOffering offering) => IsDateGated(offering.Kind);

    /// <summary>
    /// Exclusive UTC instant after which a date-gated request is past due.
    /// <see cref="LibraryRequest.NeededByEnd"/> is the due calendar date at UTC midnight,
    /// plus the requester's offset past the following UTC midnight so expiry lands at
    /// local end of that due date.
    /// </summary>
    public static DateTime GetExclusiveExpiryUtc(DateTime neededByEnd) =>
        neededByEnd.AddDays(1);

    public static bool IsWithinDueWindow(DateTime neededByEnd, DateTime utcNow) =>
        GetExclusiveExpiryUtc(neededByEnd) > utcNow;

    public static bool IsReservedAt(DateTime neededByStart, DateTime neededByEnd, DateTime utcNow) =>
        neededByStart <= utcNow && IsWithinDueWindow(neededByEnd, utcNow);

    public static bool IsOpenForFulfillment(
        LibraryRequestStatus status,
        LibraryOfferingKind offeringKind,
        DateTime neededByEnd,
        DateTime utcNow) =>
        status == LibraryRequestStatus.Open
        && (!IsDateGated(offeringKind) || IsWithinDueWindow(neededByEnd, utcNow));

    public static bool IsOpenForFulfillment(
        LibraryRequest request,
        LibraryOfferingKind offeringKind,
        DateTime utcNow) =>
        IsOpenForFulfillment(request.Status, offeringKind, request.NeededByEnd, utcNow);
}
