using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.AddPlaceholderCrewmate;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiberationFleet.Server.Tests.Application.Features.Crews;

public class PlaceholderCrewmateAndImpersonationTests
{
    [Fact]
    public void UserDisplay_PrefersDisplayNameWhenPresent()
    {
        var user = new User { Username = "ph_abc", DisplayName = "Alex" };
        UserDisplay.Name(user).Should().Be("Alex");
        UserDisplay.Name(new User { Username = "bob" }).Should().Be("bob");
        UserDisplay.Name("  Sam  ", "ph_x").Should().Be("Sam");
    }

    [Fact]
    public void UserDisplay_SyncAccountDisplayName_AlignsNonPlaceholders()
    {
        var account = new User { Username = "carol", DisplayName = null };
        UserDisplay.SyncAccountDisplayName(account);
        account.DisplayName.Should().Be("carol");

        var placeholder = new User
        {
            Username = "ph_x",
            DisplayName = "Pat",
            IsUnclaimedPlaceholder = true
        };
        UserDisplay.SyncAccountDisplayName(placeholder);
        placeholder.DisplayName.Should().Be("Pat");
    }

    [Fact]
    public async Task AddPlaceholder_WhenDisplayNameMatchesRealUsername_SucceedsWithSyntheticUsername()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var service = CreatePlaceholderService(fixture);

        var result = await service.AddPlaceholderAsync(
            fixture.Crew.Id,
            fixture.Alice.Id,
            "alice",
            [
                new PaymentPlatformAccountDto
                {
                    PlatformId = fixture.Platforms["PayPal"].Id,
                    Handle = "@ph-paypal",
                    IsPreferred = true
                }
            ],
            emergencyLevel: 0,
            peopleRepresentedCount: 1,
            disabilityLevel: 0,
            identityGroups: null,
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);
        var placeholder = await fixture.Context.Users.SingleAsync(u => u.Id == result.UserId);
        placeholder.DisplayName.Should().Be("alice");
        placeholder.Username.Should().StartWith("ph_");
        placeholder.Username.Should().NotBe("alice");
        placeholder.IsUnclaimedPlaceholder.Should().BeTrue();
        (await fixture.Context.Users.CountAsync(u => u.Username == "alice")).Should().Be(1);
    }

    [Fact]
    public async Task AddPlaceholderCommand_OrganizerCanCreatePreSeasonWithDraft()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Crew.SeasonStarted = false;
        fixture.Crew.CurrentSeasonStartDate = null;
        foreach (var memberRow in fixture.Context.CrewMemberships)
        {
            memberRow.IsInSeason = false;
            memberRow.IsSeasonReady = false;
        }

        var aliceMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == fixture.Alice.Id);
        aliceMembership.IsOrganizer = true;
        await fixture.Context.SaveChangesAsync();

        var handler = new AddPlaceholderCrewmateCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            CreatePlaceholderService(fixture),
            fixture.Context);

        var platforms = new List<PaymentPlatformAccountDto>
        {
            new()
            {
                PlatformId = fixture.Platforms["PayPal"].Id,
                Handle = "@sam",
                IsPreferred = true
            }
        };
        var accounting = new AidSeasonAccountingDto
        {
            CycleReceived = 10m,
            HasActiveCycle = true,
            AutoJoinSeasonOnStart = true,
            ReceptionOrder = 1
        };

        var result = await handler.Handle(
            new AddPlaceholderCrewmateCommand(
                Name: "Sam",
                PaymentPlatforms: platforms,
                InNeedOfAid: true,
                NeedsSurvivalAid: true,
                EstimatedMonthlyContribution: 40m,
                SeasonAccounting: accounting),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);
        var placeholderMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == result.UserId);
        placeholderMembership.IsPlaceholderMember.Should().BeTrue();
        placeholderMembership.IsHonoraryMember.Should().BeFalse();
        placeholderMembership.IsSeasonReady.Should().BeTrue();
        placeholderMembership.EstimatedMonthlyContribution.Should().Be(40m);
        placeholderMembership.AidStatDraftJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddPlaceholderCommand_OrganizerCanMarkFinancialMember()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        foreach (var memberRow in fixture.Context.CrewMemberships)
        {
            memberRow.IsInSeason = false;
            memberRow.IsSeasonReady = false;
        }

        var aliceMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == fixture.Alice.Id);
        aliceMembership.IsOrganizer = true;
        await fixture.Context.SaveChangesAsync();

        var handler = new AddPlaceholderCrewmateCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            CreatePlaceholderService(fixture),
            fixture.Context);

        var platforms = new List<PaymentPlatformAccountDto>
        {
            new()
            {
                PlatformId = fixture.Platforms["PayPal"].Id,
                Handle = "@pat",
                IsPreferred = true
            }
        };

        var result = await handler.Handle(
            new AddPlaceholderCrewmateCommand(
                Name: "Member Pat",
                PaymentPlatforms: platforms,
                InNeedOfAid: true,
                IsFinancialMember: true),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("financial member");
        var placeholderMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == result.UserId);
        placeholderMembership.IsPlaceholderMember.Should().BeTrue();
        placeholderMembership.IsHonoraryMember.Should().BeTrue();
    }

    [Fact]
    public async Task AddPlaceholderCommand_NonManagerRichCreate_IsRejected()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var bobMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == fixture.Bob.Id);
        bobMembership.IsOrganizer = false;
        bobMembership.IsAccountant = false;
        await fixture.Context.SaveChangesAsync();

        var handler = new AddPlaceholderCrewmateCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Bob.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            CreatePlaceholderService(fixture),
            fixture.Context);

        var platforms = new List<PaymentPlatformAccountDto>
        {
            new()
            {
                PlatformId = fixture.Platforms["PayPal"].Id,
                Handle = "@sam",
                IsPreferred = true
            }
        };

        var result = await handler.Handle(
            new AddPlaceholderCrewmateCommand(
                Name: "Sam",
                PaymentPlatforms: platforms,
                InNeedOfAid: false),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("organizers and accountants");
    }

    [Fact]
    public async Task RecordGifts_WhenImpersonating_SetsGiverAndImpersonatedBy()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var aliceMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == fixture.Alice.Id);
        aliceMembership.IsOrganizer = true;
        await fixture.Context.SaveChangesAsync();

        var handler = new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            new UserRepository(fixture.Context),
            new MutualAidRepository(fixture.Context),
            fixture.Service,
            HandlerTestFixture.CreateCustomGiftRecordingService(fixture.Context, fixture.Service),
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context,
            NullLogger<RecordGiftsCommandHandler>.Instance);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    25m,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "cycle")
            ],
            ImpersonateAsUserId: fixture.Carol.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);
        var gift = await fixture.Context.Gifts.SingleAsync();
        gift.GiverUserId.Should().Be(fixture.Carol.Id);
        gift.ImpersonatedByUserId.Should().Be(fixture.Alice.Id);

        var mapped = GiftMapper.MapGift(gift);
        mapped.GiverId.Should().Be(fixture.Carol.Id);
        mapped.ImpersonatedByUserId.Should().Be(fixture.Alice.Id);
    }

    [Fact]
    public async Task MergePlaceholder_InheritsPaymentPlatformsClaimantDoesNotHave()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        // Alice has PayPal only; placeholder adds Venmo (new) and PayPal (duplicate — keep Alice's).
        var placeholder = new User
        {
            Username = "ph_platforms",
            DisplayName = "Pat",
            Email = "ph-platforms@example.com",
            PasswordHash = "hash",
            IsActive = true,
            IsUnclaimedPlaceholder = true
        };
        fixture.Context.Users.Add(placeholder);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.CrewMemberships.Add(new CrewMembership
        {
            UserId = placeholder.Id,
            CrewId = fixture.Crew.Id,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true,
            IsSeasonReady = true,
            IsHonoraryMember = true
        });
        fixture.Context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform
            {
                UserId = placeholder.Id,
                CrewPaymentPlatformId = fixture.Platforms["Venmo"].Id,
                PlatformName = "Venmo",
                Handle = "@pat-venmo",
                IsPreferred = true
            },
            new UserPaymentPlatform
            {
                UserId = placeholder.Id,
                CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
                PlatformName = "PayPal",
                Handle = "@pat-paypal",
                IsPreferred = false
            });
        await fixture.Context.SaveChangesAsync();

        var alicePayPalBefore = await fixture.Context.UserPaymentPlatforms.SingleAsync(p =>
            p.UserId == fixture.Alice.Id && p.CrewPaymentPlatformId == fixture.Platforms["PayPal"].Id);
        var alicePayPalHandle = alicePayPalBefore.Handle;

        var service = CreatePlaceholderService(fixture);
        await service.MergePlaceholderIntoClaimantAsync(
            fixture.Crew.Id,
            placeholder.Id,
            fixture.Alice.Id,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var alicePlatforms = await fixture.Context.UserPaymentPlatforms
            .Where(p => p.UserId == fixture.Alice.Id)
            .ToListAsync();
        alicePlatforms.Should().Contain(p =>
            p.CrewPaymentPlatformId == fixture.Platforms["Venmo"].Id && p.Handle == "@pat-venmo");
        alicePlatforms.Count(p => p.CrewPaymentPlatformId == fixture.Platforms["PayPal"].Id)
            .Should().Be(1);
        alicePlatforms.Single(p => p.CrewPaymentPlatformId == fixture.Platforms["PayPal"].Id)
            .Handle.Should().Be(alicePayPalHandle);
        // Alice already had a preferred platform — inherited Venmo should not steal preferred.
        alicePlatforms.Single(p => p.CrewPaymentPlatformId == fixture.Platforms["Venmo"].Id)
            .IsPreferred.Should().BeFalse();
        (await fixture.Context.Users.AnyAsync(u => u.Id == placeholder.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task MergePlaceholder_WhenPlaceholderHasSeasonEntries_PullsClaimantIntoSeason()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        // Claimant already in crew but not marked in-season (e.g. joined mid-season without flags).
        var claimantMembership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Carol.Id && m.CrewId == fixture.Crew.Id);
        claimantMembership.IsInSeason = false;
        claimantMembership.IsSeasonReady = false;
        claimantMembership.GivingSeasonJoinedAt = null;

        // Remove claimant's existing season cycles so only the placeholder's entries remain.
        var carolCycles = await fixture.Context.SeasonCycles
            .Where(c => c.UserId == fixture.Carol.Id)
            .ToListAsync();
        fixture.Context.SeasonCycles.RemoveRange(carolCycles);

        var placeholder = new User
        {
            Username = "ph_season",
            DisplayName = "Pat",
            Email = "ph-season@example.com",
            PasswordHash = "hash",
            IsActive = true,
            IsUnclaimedPlaceholder = true
        };
        fixture.Context.Users.Add(placeholder);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.CrewMemberships.Add(new CrewMembership
        {
            UserId = placeholder.Id,
            CrewId = fixture.Crew.Id,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true,
            // Flags out of sync with entries — entries alone mean "in season".
            IsInSeason = false,
            IsSeasonReady = false,
            IsHonoraryMember = true
        });
        fixture.Context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = placeholder.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 100m,
            CycleReceived = 15m,
            TotalReceptionAmount = 15m,
            ReceptionOrderPosition = 9,
            PriorityScoreAtSeasonStart = 50m
        });
        await fixture.Context.SaveChangesAsync();

        var service = CreatePlaceholderService(fixture);
        await service.MergePlaceholderIntoClaimantAsync(
            fixture.Crew.Id,
            placeholder.Id,
            fixture.Carol.Id,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var mergedMembership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Carol.Id && m.CrewId == fixture.Crew.Id);
        mergedMembership.IsInSeason.Should().BeTrue();
        mergedMembership.IsSeasonReady.Should().BeTrue();
        mergedMembership.GivingSeasonJoinedAt.Should().NotBeNull();

        (await fixture.Context.SeasonCycles.CountAsync(c =>
            c.UserId == fixture.Carol.Id && c.SeasonStartDate == fixture.SeasonStart))
            .Should().BeGreaterThan(0);
        (await fixture.Context.Users.AnyAsync(u => u.Id == placeholder.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ReassignPlaceholderGifts_MovesGiverRecipientAndMiddleman()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var placeholder = new User
        {
            Username = "ph_test",
            DisplayName = "Pat",
            Email = "ph@example.com",
            PasswordHash = "hash",
            IsActive = true,
            IsUnclaimedPlaceholder = true
        };
        fixture.Context.Users.Add(placeholder);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.AddRange(
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = placeholder.Id,
                RecipientUserId = fixture.Bob.Id,
                ImpersonatedByUserId = fixture.Alice.Id,
                Type = GiftType.Direct,
                Amount = 10m,
                CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
                VerificationStatus = GiftVerificationStatus.Verified,
                CreatedAt = DateTime.UtcNow
            },
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = fixture.Alice.Id,
                RecipientUserId = placeholder.Id,
                Type = GiftType.Direct,
                Amount = 5m,
                CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
                VerificationStatus = GiftVerificationStatus.Verified,
                CreatedAt = DateTime.UtcNow
            });
        await fixture.Context.SaveChangesAsync();

        var repo = new GiftRepository(fixture.Context);
        await repo.ReassignPlaceholderGiftRecipientsAsync(
            fixture.Crew.Id,
            placeholder.Id,
            fixture.Carol.Id,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var gifts = await fixture.Context.Gifts.OrderBy(g => g.Amount).ToListAsync();
        gifts[0].RecipientUserId.Should().Be(fixture.Carol.Id);
        gifts[1].GiverUserId.Should().Be(fixture.Carol.Id);
        gifts[1].ImpersonatedByUserId.Should().Be(fixture.Alice.Id);
    }

    private static PlaceholderCrewmateService CreatePlaceholderService(MutualAidSeasonFixture fixture) =>
        new(
            new UserRepository(fixture.Context),
            new CrewMembershipRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            new MutualAidRepository(fixture.Context),
            fixture.Service,
            new GiftRepository(fixture.Context),
            fixture.Context);
}
