using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using FluentAssertions;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts;

public class GiftSeasonAccessTests
{
    [Fact]
    public void IsSeasonLocked_WhenNoCurrentSeason_ReturnsFalse()
    {
        var gift = new Gift { CreatedAt = DateTime.UtcNow.AddMonths(-6) };

        GiftSeasonAccess.IsSeasonLocked(gift, null, null).Should().BeFalse();
    }

    [Fact]
    public void IsSeasonLocked_WhenGiftSeasonMatchesCurrent_ReturnsFalse()
    {
        var seasonStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gift = new Gift { CreatedAt = seasonStart.AddDays(10) };

        GiftSeasonAccess.IsSeasonLocked(gift, seasonStart, seasonStart).Should().BeFalse();
    }

    [Fact]
    public void IsSeasonLocked_WhenGiftSeasonIsOlder_ReturnsTrue()
    {
        var current = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var prior = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gift = new Gift { CreatedAt = prior.AddDays(5) };

        GiftSeasonAccess.IsSeasonLocked(gift, current, prior).Should().BeTrue();
    }

    [Fact]
    public void IsSeasonLocked_WhenNoCycleAndCreatedBeforeCurrentSeason_ReturnsTrue()
    {
        var current = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var gift = new Gift { CreatedAt = current.AddDays(-1) };

        GiftSeasonAccess.IsSeasonLocked(gift, current, null).Should().BeTrue();
    }

    [Fact]
    public void CanMutateVerification_AllowsAccountantWhenLocked()
    {
        GiftSeasonAccess.CanMutateVerification(isAccountant: true, isSeasonLocked: true).Should().BeTrue();
        GiftSeasonAccess.CanMutateVerification(isAccountant: false, isSeasonLocked: true).Should().BeFalse();
        GiftSeasonAccess.CanMutateVerification(isAccountant: false, isSeasonLocked: false).Should().BeTrue();
    }
}
