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

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Durable).Should().BeTrue();
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

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Durable).Should().BeFalse();
        request.Status.Should().Be(LibraryRequestStatus.Open);
    }

    [Fact]
    public void TryExpireOpenRequest_Consumable_NeverExpiresByDate()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddHours(-1),
            NeededByEnd = utcNow.AddHours(-1)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Consumable).Should().BeFalse();
        request.Status.Should().Be(LibraryRequestStatus.Open);
    }

    [Fact]
    public void ApplyExpiry_LeavesConsumableOpen_WhileExpiringDurable()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var consumable = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow,
            Unit = new LibraryUnit { Offering = new LibraryOffering { Kind = LibraryOfferingKind.Consumable } }
        };
        var durable = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddMinutes(-1),
            Unit = new LibraryUnit { Offering = new LibraryOffering { Kind = LibraryOfferingKind.Durable } }
        };

        LibraryRequestExpiryService.ApplyExpiry([consumable, durable], utcNow);

        consumable.Status.Should().Be(LibraryRequestStatus.Open);
        durable.Status.Should().Be(LibraryRequestStatus.Expired);
    }
}
