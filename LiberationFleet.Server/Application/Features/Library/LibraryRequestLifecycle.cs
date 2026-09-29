using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// Durable/Service requests use a future needed-by window; consumables stay open until
/// fulfilled/denied/cancelled (no date-gated expiry).
/// </summary>
public static class LibraryRequestLifecycle
{
    public static bool IsDateGated(LibraryOfferingKind kind) =>
        kind is LibraryOfferingKind.Durable or LibraryOfferingKind.Service;

    public static bool IsDateGated(LibraryOffering offering) => IsDateGated(offering.Kind);

    public static bool IsOpenForFulfillment(
        LibraryRequestStatus status,
        LibraryOfferingKind offeringKind,
        DateTime neededByStart,
        DateTime utcNow) =>
        status == LibraryRequestStatus.Open
        && (!IsDateGated(offeringKind) || neededByStart > utcNow);

    public static bool IsOpenForFulfillment(
        LibraryRequest request,
        LibraryOfferingKind offeringKind,
        DateTime utcNow) =>
        IsOpenForFulfillment(request.Status, offeringKind, request.NeededByStart, utcNow);
}
