using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryRequestExpiryService
{
    public static bool TryExpireDeniedRequest(LibraryRequest request, DateTime utcNow)
    {
        if (request.Status != LibraryRequestStatus.Denied || !request.DeniedAt.HasValue)
        {
            return false;
        }

        if (request.DeniedAt.Value.AddDays(2) > utcNow)
        {
            return false;
        }

        request.Status = LibraryRequestStatus.Expired;
        request.UpdatedAt = utcNow;
        return true;
    }

    public static void ApplyExpiry(IEnumerable<LibraryRequest> requests, DateTime utcNow)
    {
        foreach (var request in requests)
        {
            TryExpireDeniedRequest(request, utcNow);
        }
    }
}
