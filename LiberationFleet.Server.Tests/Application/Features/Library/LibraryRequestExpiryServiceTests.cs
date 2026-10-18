using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestExpiryServiceTests
{
    [Fact]
    public void TryExpireOpenRequest_WhenDueDateHasEnded_MarksExpired()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            NeededByEnd = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Durable).Should().BeTrue();
        request.Status.Should().Be(LibraryRequestStatus.Expired);
        request.UpdatedAt.Should().Be(utcNow);
    }

    [Fact]
    public void TryExpireOpenRequest_SameDayDueDate_StaysOpenUntilEndOfDueDay()
    {
        var utcNow = new DateTime(2026, 8, 18, 16, 30, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc),
            NeededByEnd = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Service).Should().BeFalse();
        request.Status.Should().Be(LibraryRequestStatus.Open);
    }

    [Fact]
    public void TryExpireOpenRequest_AtExclusiveEndOfDueDay_MarksExpired()
    {
        var utcNow = new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc),
            NeededByEnd = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc)
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(request, utcNow, LibraryOfferingKind.Service).Should().BeTrue();
        request.Status.Should().Be(LibraryRequestStatus.Expired);
    }

    [Fact]
    public void TryExpireOpenRequest_RequesterTimeZoneOffset_StaysOpenUntilLocalMidnight()
    {
        // America/New_York in August is UTC-4; exclusive expiry is 04:00 UTC the next day.
        var neededByEnd = new DateTime(2026, 8, 18, 4, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc),
            NeededByEnd = neededByEnd
        };

        LibraryRequestExpiryService.TryExpireOpenRequest(
            request,
            new DateTime(2026, 8, 19, 3, 59, 0, DateTimeKind.Utc),
            LibraryOfferingKind.Service).Should().BeFalse();
        request.Status.Should().Be(LibraryRequestStatus.Open);

        LibraryRequestExpiryService.TryExpireOpenRequest(
            request,
            new DateTime(2026, 8, 19, 4, 0, 0, DateTimeKind.Utc),
            LibraryOfferingKind.Service).Should().BeTrue();
        request.Status.Should().Be(LibraryRequestStatus.Expired);
    }

    [Fact]
    public void TryExpireOpenRequest_WhenDueDateStillFuture_DoesNothing()
    {
        var utcNow = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var request = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddDays(1),
            NeededByEnd = utcNow.AddDays(1)
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
            NeededByEnd = utcNow,
            Unit = new LibraryUnit { Offering = new LibraryOffering { Kind = LibraryOfferingKind.Consumable } }
        };
        var durable = new LibraryRequest
        {
            Status = LibraryRequestStatus.Open,
            NeededByStart = utcNow.AddDays(-1),
            NeededByEnd = utcNow.AddDays(-1),
            Unit = new LibraryUnit { Offering = new LibraryOffering { Kind = LibraryOfferingKind.Durable } }
        };

        LibraryRequestExpiryService.ApplyExpiry([consumable, durable], utcNow);

        consumable.Status.Should().Be(LibraryRequestStatus.Open);
        durable.Status.Should().Be(LibraryRequestStatus.Expired);
    }
}
