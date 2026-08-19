using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestExpiryServiceTests
{
    [Fact]
    public void TryExpireOpenRequest_WhenStartDatePassed_MarksExpired()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddDays(-1)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow).Should().BeTrue();
        request.Status.Should().Be(LibraryRequestStatus.Expired);
        request.UpdatedAt.Should().Be(utcNow);
    }

    [Fact]
    public void TryExpireOpenRequest_WhenStartDateStillFuture_DoesNothing()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddDays(1)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow).Should().BeFalse();
        request.Status.Should().Be(LibraryRequestStatus.Open);
    }
}
