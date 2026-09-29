using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestLifecycleTests
{
    [Theory]
    [InlineData(LibraryOfferingKind.Durable, true)]
    [InlineData(LibraryOfferingKind.Service, true)]
    [InlineData(LibraryOfferingKind.Consumable, false)]
    [InlineData(LibraryOfferingKind.Digital, false)]
    public void IsDateGated_MatchesKind(LibraryOfferingKind kind, bool expected) =>
        LibraryRequestLifecycle.IsDateGated(kind).Should().Be(expected);

    [Fact]
    public void IsOpenForFulfillment_ConsumableOpenToday_IsOpen()
    {
        var utcNow = new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc);
        LibraryRequestLifecycle.IsOpenForFulfillment(
            LibraryRequestStatus.Open,
            LibraryOfferingKind.Consumable,
            utcNow,
            utcNow).Should().BeTrue();
    }

    [Fact]
    public void IsOpenForFulfillment_DurableNeededByPassed_IsNotOpen()
    {
        var utcNow = new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc);
        LibraryRequestLifecycle.IsOpenForFulfillment(
            LibraryRequestStatus.Open,
            LibraryOfferingKind.Durable,
            utcNow.AddHours(-1),
            utcNow).Should().BeFalse();
    }

    [Fact]
    public void IsOpenForFulfillment_DurableNeededByFuture_IsOpen()
    {
        var utcNow = new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc);
        LibraryRequestLifecycle.IsOpenForFulfillment(
            LibraryRequestStatus.Open,
            LibraryOfferingKind.Durable,
            utcNow.AddDays(1),
            utcNow).Should().BeTrue();
    }
}
