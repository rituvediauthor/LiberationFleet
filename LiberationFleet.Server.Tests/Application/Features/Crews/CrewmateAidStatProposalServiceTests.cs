using System.Text.Json;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Crews;

public class CrewmateAidStatProposalServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task TryApply_WhenCycleReceivedExceedsCap_DoesNotApply()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var service = CreateService(fixture);

        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.CycleReceived, NewValue = "150" }]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var change = await fixture.Context.ProposalCrewmateAidStatChanges
            .SingleAsync(c => c.ProposalId == proposal.Id);
        change.IsApplied.Should().BeTrue();
        change.Description.Should().Contain("exceeds effective cap");

        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        primary.CycleReceived.Should().Be(0m);
    }

    [Fact]
    public async Task TryApply_WhenPrimaryAndSegmentExist_EditsPrimaryOnly()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        fixture.Context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Bob.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 40m,
            UsesSegmentCap = true,
            EmergencySplitOfferId = 99,
            CycleReceived = 10m,
            ReceptionOrderPosition = primary.ReceptionOrderPosition,
            PriorityScoreAtSeasonStart = primary.PriorityScoreAtSeasonStart
        });
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.CycleReceived, NewValue = "25" }]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var reloadedPrimary = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        reloadedPrimary.CycleReceived.Should().Be(25m);
        var segment = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.EmergencySplitOfferId == 99);
        segment.CycleReceived.Should().Be(10m);
    }

    [Fact]
    public async Task TryApply_WhenCycleReceivedMeetsCap_MarksCompletedFromReception()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        // Split reduced remaining primary via UsesSegmentCap.
        primary.UsesSegmentCap = true;
        primary.CycleCapAtStart = 60m;
        primary.CycleReceived = 10m;
        primary.CycleCompleted = false;
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.CycleReceived, NewValue = "60" }]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        reloaded.CycleReceived.Should().Be(60m);
        reloaded.CycleCompleted.Should().BeTrue();
        reloaded.CycleCapAtCompletion.Should().Be(60m);
    }

    [Fact]
    public async Task TryApply_WhenCycleReceivedBelowCap_ClearsCompleted()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        primary.CycleReceived = 100m;
        primary.CycleCompleted = true;
        primary.CycleCompletedAt = DateTime.UtcNow;
        primary.CycleCapAtCompletion = 100m;
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.CycleReceived, NewValue = "40" }]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        reloaded.CycleReceived.Should().Be(40m);
        reloaded.CycleCompleted.Should().BeFalse();
        reloaded.CycleCompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task TryApply_WhenSeasonAccounting_UpdatesCycleOrderAndThresholds()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        primary.ReceptionOrderPosition = 2;
        primary.HasCycleStarted = false;

        var existingForBob = await fixture.Context.MonthlySurvivalThresholds
            .Where(t => t.UserId == fixture.Bob.Id)
            .ToListAsync();
        fixture.Context.MonthlySurvivalThresholds.RemoveRange(existingForBob);
        fixture.Context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Bob.Id,
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            ThresholdAmount = 50m,
            ReceivedAmount = 10m,
            ReceptionOrderPosition = 0,
            Satisfied = false
        });
        await fixture.Context.SaveChangesAsync();

        var existingThreshold = await fixture.Context.MonthlySurvivalThresholds
            .SingleAsync(t => t.UserId == fixture.Bob.Id);
        var accounting = new AidSeasonAccountingDto
        {
            CycleReceived = 35m,
            HasActiveCycle = true,
            ReceptionOrder = 1,
            AutoJoinSeasonOnStart = true,
            SurvivalThresholds =
            [
                new AidSurvivalThresholdDraftDto
                {
                    Id = existingThreshold.Id,
                    ThresholdAmount = 50m,
                    AmountRemaining = 20m,
                    Order = 1
                }
            ]
        };

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [
                new CrewmateAidStatChangeItem
                {
                    Field = CrewmateAidStatField.SeasonAccounting,
                    NewValue = AidStatDraftSerializer.Serialize(accounting)
                }
            ]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        reloaded.CycleReceived.Should().Be(35m);
        reloaded.HasCycleStarted.Should().BeTrue();
        reloaded.ReceptionOrderPosition.Should().Be(0);
        reloaded.SurvivalThresholdReceived.Should().Be(30m);
        reloaded.TotalReceptionAmount.Should().Be(65m);

        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Bob.Id && m.CrewId == fixture.Crew.Id);
        membership.AutoJoinSeasonOnStart.Should().BeTrue();
        membership.AidStatDraftJson.Should().BeNull();

        var threshold = await fixture.Context.MonthlySurvivalThresholds
            .SingleAsync(t => t.UserId == fixture.Bob.Id);
        threshold.ReceivedAmount.Should().Be(30m);
        threshold.ThresholdAmount.Should().BeGreaterThanOrEqualTo(30m);
    }

    [Fact]
    public async Task TryApply_WhenSeasonAccountingBeforeSeason_PersistsDraft()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        fixture.Crew.SeasonStarted = false;
        fixture.Crew.CurrentSeasonStartDate = null;
        await fixture.Context.SaveChangesAsync();

        var accounting = new AidSeasonAccountingDto
        {
            CycleReceived = 12m,
            HasActiveCycle = false,
            ReceptionOrder = 2,
            AutoJoinSeasonOnStart = true,
            SurvivalThresholds =
            [
                new AidSurvivalThresholdDraftDto
                {
                    ThresholdAmount = 25m,
                    AmountRemaining = 15m,
                    Order = 1
                }
            ]
        };

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Bob.Id,
            [
                new CrewmateAidStatChangeItem
                {
                    Field = CrewmateAidStatField.SeasonAccounting,
                    NewValue = AidStatDraftSerializer.Serialize(accounting)
                }
            ]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Bob.Id && m.CrewId == fixture.Crew.Id);
        membership.AutoJoinSeasonOnStart.Should().BeTrue();
        membership.AidStatDraftJson.Should().NotBeNullOrWhiteSpace();
        var draft = AidStatDraftSerializer.Deserialize(membership.AidStatDraftJson);
        draft.Should().NotBeNull();
        draft!.CycleReceived.Should().Be(12m);
        draft.SurvivalThresholds.Should().HaveCount(1);
    }

    [Fact]
    public async Task TryApply_WhenPrimaryMissing_CreatesWithReceptionOrder()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Carol.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        fixture.Context.SeasonCycles.Remove(primary);
        await fixture.Context.SaveChangesAsync();

        var service = CreateService(fixture);
        var proposal = await CreateApprovedProposalAsync(
            fixture,
            fixture.Carol.Id,
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.CycleReceived, NewValue = "10" }]);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var created = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Carol.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        created.CycleReceived.Should().Be(10m);
        created.ReceptionOrderPosition.Should().BeGreaterThanOrEqualTo(0);
        // Not hardcoded colliding at front of locked leaders without insert logic.
        var positions = await fixture.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fixture.SeasonStart && c.EmergencyRequestId == null && c.EmergencySplitOfferId == null)
            .Select(c => c.ReceptionOrderPosition)
            .ToListAsync();
        positions.Should().OnlyHaveUniqueItems();
    }

    private static CrewmateAidStatProposalService CreateService(MutualAidSeasonFixture fixture) =>
        new(
            new ProposalRepository(fixture.Context),
            new FleetRepository(fixture.Context),
            new CrewRepository(fixture.Context),
            new CrewMembershipRepository(fixture.Context),
            new UserRepository(fixture.Context),
            new MutualAidRepository(fixture.Context),
            fixture.Service,
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context);

    private static async Task<Proposal> CreateApprovedProposalAsync(
        MutualAidSeasonFixture fixture,
        int targetUserId,
        IReadOnlyList<CrewmateAidStatChangeItem> items)
    {
        var proposal = new Proposal
        {
            CrewId = fixture.Crew.Id,
            AuthorUserId = fixture.Alice.Id,
            Kind = ProposalKind.CrewmateAidStatChange,
            Status = ProposalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        fixture.Context.Proposals.Add(proposal);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.ProposalCrewmateAidStatChanges.Add(new ProposalCrewmateAidStatChange
        {
            ProposalId = proposal.Id,
            TargetUserId = targetUserId,
            ChangesJson = JsonSerializer.Serialize(items, JsonOptions),
            Title = "Test",
            Description = "Test aid stat change",
            IsApplied = false
        });
        await fixture.Context.SaveChangesAsync();
        return proposal;
    }
}
