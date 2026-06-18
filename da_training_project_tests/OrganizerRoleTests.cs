using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;
using LiberationFleet.Server.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class OrganizerRoleTests
{
    [Fact]
    public async Task CreateCrew_FirstMember_IsAssignedOrganizer()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var creator = new LiberationFleet.Server.Domain.Entities.User
        {
            Username = "Creator",
            Email = $"{Guid.NewGuid():N}@org.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(creator);
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateCreateCrewHandler(context, creator.Id);
        var result = await handler.Handle(new CreateCrewCommand
        {
            Name = "Organizer Crew",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Online"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();

        var membership = await context.CrewMemberships
            .FirstAsync(m => m.UserId == creator.Id);
        membership.IsOrganizer.Should().BeTrue();
    }

    [Fact]
    public async Task JoinCrew_SubsequentMember_IsNotOrganizer()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var joiner = new LiberationFleet.Server.Domain.Entities.User
        {
            Username = "Joiner",
            Email = $"{Guid.NewGuid():N}@org2.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(joiner);
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateJoinCrewHandler(context, joiner.Id);
        var result = await handler.Handle(new JoinCrewCommand { CrewId = crew.Id }, CancellationToken.None);

        result.Success.Should().BeTrue();

        var joinerMembership = await context.CrewMemberships
            .FirstAsync(m => m.UserId == joiner.Id && m.CrewId == crew.Id);
        joinerMembership.IsOrganizer.Should().BeFalse();
    }

    [Fact]
    public async Task Organizer_HasPriorityScoreOfNegativeOne()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, organizer) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var calculationService = MutualAidTestFixture.CreateCalculationService(context);

        var score = await calculationService.CalculatePriorityScoreAsync(organizer.Id, crew.Id);
        score.Should().Be(-1);
    }

    [Fact]
    public async Task HonoraryMember_HasMembershipStatusOfOne()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Honorary");

        var membership = await context.CrewMemberships.SingleAsync(m => m.UserId == user.Id);
        membership.IsHonoraryMember = true;
        await context.SaveChangesAsync();

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var isMember = await calculationService.IsMemberAsync(user.Id, crew.Id);
        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task CrewMembership_IsOrganizer_DefaultsFalse()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var member = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Default");

        var membership = await context.CrewMemberships.SingleAsync(m => m.UserId == member.Id);
        membership.IsOrganizer.Should().BeFalse();
        membership.IsHonoraryMember.Should().BeFalse();
    }
}
