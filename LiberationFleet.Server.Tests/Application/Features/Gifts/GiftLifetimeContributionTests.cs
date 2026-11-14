using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts;

public class GiftLifetimeContributionTests
{
    [Fact]
    public void CountsTowardLifetimeTotal_RequiresVerifiedDirectOrCompleted()
    {
        GiftLifetimeContribution.CountsTowardLifetimeTotal(
            countsTowardContribution: true,
            GiftType.Direct,
            GiftVerificationStatus.Pending,
            10m).Should().BeFalse();

        GiftLifetimeContribution.CountsTowardLifetimeTotal(
            countsTowardContribution: true,
            GiftType.Direct,
            GiftVerificationStatus.Verified,
            10m).Should().BeTrue();

        GiftLifetimeContribution.CountsTowardLifetimeTotal(
            countsTowardContribution: true,
            GiftType.Initiated,
            GiftVerificationStatus.Verified,
            10m).Should().BeFalse();
    }

    [Fact]
    public async Task GetCrewmateGiftStats_IgnoresPendingGiftsUntilVerified()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var repo = new GiftRepository(fixture.Context);

        fixture.Context.Gifts.Add(new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 40m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var before = await repo.GetCrewmateGiftStatsAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            fixture.SeasonStart,
            CancellationToken.None);
        before.LifetimeContributions.Should().Be(0m);

        var gift = await fixture.Context.Gifts.SingleAsync();
        gift.VerificationStatus = GiftVerificationStatus.Verified;
        await fixture.Context.SaveChangesAsync();

        var after = await repo.GetCrewmateGiftStatsAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            fixture.SeasonStart,
            CancellationToken.None);
        after.LifetimeContributions.Should().Be(40m);
    }

    [Fact]
    public async Task ConfirmReceived_BumpsLifetimeContributionOverride()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var aliceMembership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        aliceMembership.LifetimeContributionOverride = 100m;
        aliceMembership.IsInSeason = true;

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 25m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardContribution = true,
            CountsTowardReception = true,
            VerificationStatus = GiftVerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.Gifts.Add(gift);
        await fixture.Context.SaveChangesAsync();

        var currentUser = new Mock<LiberationFleet.Server.Application.Common.Interfaces.ICurrentUserService>();
        currentUser.Setup(c => c.UserId).Returns(fixture.Bob.Id);

        var handler = new VerifyGiftCommandHandler(
            currentUser.Object,
            new CrewMembershipRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            fixture.Service,
            fixture.Context,
            NullLogger<VerifyGiftCommandHandler>.Instance);

        var result = await handler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);

        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.LifetimeContributionOverride.Should().Be(125m);

        var stats = await new GiftRepository(fixture.Context).GetCrewmateGiftStatsAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            fixture.SeasonStart,
            CancellationToken.None);
        stats.LifetimeContributions.Should().Be(125m);
    }

    [Fact]
    public async Task CreditLifetimeContributionForGift_NoopsWhenOverrideAbsent()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 15m,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };

        await fixture.Service.CreditLifetimeContributionForGiftAsync(gift, CancellationToken.None);

        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.LifetimeContributionOverride.Should().BeNull();
    }
}
