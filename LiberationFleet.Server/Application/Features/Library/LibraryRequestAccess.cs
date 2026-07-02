using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryRequestAccess
{
    public static int GetPossessorUserId(LibraryRequest request) =>
        LibraryOfferingRules.IsStockBased(request.Unit.Offering)
            ? request.Unit.Offering.CreatorUserId
            : request.Unit.CurrentPossessorUserId;

    public static bool IsPossessor(LibraryRequest request, int userId) =>
        GetPossessorUserId(request) == userId;

    public static bool IsRequester(LibraryRequest request, int userId) =>
        request.RequesterUserId == userId;

    public static bool CanView(LibraryRequest request, int userId) =>
        IsRequester(request, userId) || IsPossessor(request, userId);

    public static bool CanMessage(LibraryRequest request, int userId) =>
        CanView(request, userId)
        && (request.Status == LibraryRequestStatus.Open || request.Status == LibraryRequestStatus.Denied);

    public static decimal CalculateCompletionGiftAmount(LibraryRequest request) =>
        LibraryOfferingRules.IsStockBased(request.Unit.Offering)
            ? LibraryOfferingRules.CalculateStockCompletionGift(request)
            : 0.10m * request.Unit.Offering.ValuePerUnit * request.Quantity;
}
