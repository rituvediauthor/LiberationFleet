using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryContributionGiftServiceTests
{
    [Fact]
    public async Task TryAwardCreatorForStockUseAsync_CreatesGiftWithContributionAndReceptionCredit()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var offering = new LibraryOffering
        {
            CrewId = fixture.Crew.Id,
            CreatorUserId = fixture.Alice.Id,
            CreatorUser = fixture.Alice,
            Title = "Eggs",
            Kind = LibraryOfferingKind.Consumable,
            FulfillmentMode = LibraryFulfillmentMode.OnDemand,
            ValuePerUnit = 2m,
            RemainingStock = 12,
            QuantityNotApplicable = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        fixture.Context.LibraryOfferings.Add(offering);
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var details = await service.TryAwardCreatorForStockUseAsync(
            fixture.Crew.Id,
            offering,
            quantity: 3,
            recipientUserId: fixture.Bob.Id,
            recipientUsername: fixture.Bob.Username,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        details.Should().NotBeNull();
        details!.Amount.Should().Be(6m);
        details.ContributorUserId.Should().Be(fixture.Alice.Id);
        details.RecipientUserId.Should().Be(fixture.Bob.Id);

        var gift = await fixture.Context.Gifts
            .Include(g => g.CrewPaymentPlatform)
            .SingleAsync(g => g.Id == details.GiftId);
        gift.CountsTowardContribution.Should().BeTrue();
        gift.CountsTowardReception.Should().BeTrue();
        gift.GiverUserId.Should().Be(fixture.Alice.Id);
        gift.RecipientUserId.Should().Be(fixture.Bob.Id);
        gift.CrewPaymentPlatform!.Name.Should().Be(LibraryContributionGiftService.InKindPlatformName);
        gift.LibraryItemTitle.Should().Be("Eggs");
    }

    [Fact]
    public async Task TryAwardCreatorForStockUseAsync_WhenCreatorIsRecipient_ReturnsNull()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var offering = new LibraryOffering
        {
            CrewId = fixture.Crew.Id,
            CreatorUserId = fixture.Alice.Id,
            CreatorUser = fixture.Alice,
            Title = "Eggs",
            Kind = LibraryOfferingKind.Consumable,
            FulfillmentMode = LibraryFulfillmentMode.OnDemand,
            ValuePerUnit = 2m,
            RemainingStock = 12,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        fixture.Context.LibraryOfferings.Add(offering);
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var details = await service.TryAwardCreatorForStockUseAsync(
            fixture.Crew.Id,
            offering,
            quantity: 1,
            recipientUserId: fixture.Alice.Id,
            recipientUsername: fixture.Alice.Username,
            CancellationToken.None);

        details.Should().BeNull();
    }

    [Fact]
    public async Task StockUseContributionGift_CountsTowardFinancialMembership_ButNotDisplayedAverage()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 0m;

        var offering = new LibraryOffering
        {
            CrewId = fixture.Crew.Id,
            CreatorUserId = fixture.Alice.Id,
            CreatorUser = fixture.Alice,
            Title = "Bread",
            Kind = LibraryOfferingKind.Consumable,
            FulfillmentMode = LibraryFulfillmentMode.OnDemand,
            ValuePerUnit = 30m,
            RemainingStock = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        fixture.Context.LibraryOfferings.Add(offering);
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var details = await service.TryAwardCreatorForStockUseAsync(
            fixture.Crew.Id,
            offering,
            quantity: 1,
            recipientUserId: fixture.Bob.Id,
            recipientUsername: fixture.Bob.Username,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();
        details.Should().NotBeNull();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);
        isMember.Should().BeTrue();

        var giftStats = await new GiftRepository(fixture.Context).GetCrewmateGiftStatsAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            fixture.SeasonStart,
            CancellationToken.None);
        giftStats.AverageMonthlyContributions.Should().Be(0m);
    }

    private static LibraryContributionGiftService CreateService(MutualAidSeasonFixture fixture) =>
        new(
            new CrewPaymentPlatformRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewGiftRecipientService(
                new MutualAidRepository(fixture.Context),
                new UserRepository(fixture.Context),
                new CrewMembershipRepository(fixture.Context),
                fixture.Context));
}
