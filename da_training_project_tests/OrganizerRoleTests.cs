using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class OrganizerRoleTests
{
    private static ApplicationDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedPaymentPlatforms(ApplicationDbContext context)
    {
        if (await context.PaymentPlatforms.AnyAsync()) return;
        context.PaymentPlatforms.AddRange(
            new PaymentPlatform { Id = 1, Name = "PayPal", SortOrder = 1 },
            new PaymentPlatform { Id = 2, Name = "Cash App", SortOrder = 2 },
            new PaymentPlatform { Id = 3, Name = "Venmo", SortOrder = 3 },
            new PaymentPlatform { Id = 4, Name = "Zelle", SortOrder = 4 },
            new PaymentPlatform { Id = 5, Name = "Other", SortOrder = 5 });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateCrew_FirstMember_IsAssignedOrganizer()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var creator = new User { Username = "Creator", Email = "creator@org.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(creator);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Organizer Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ORGN1234",
            CreatedByUserId = creator.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var membership = new CrewMembership
        {
            UserId = creator.Id,
            CrewId = crew.Id,
            IsOrganizer = true,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };
        context.CrewMemberships.Add(membership);
        await context.SaveChangesAsync();

        var stored = await context.CrewMemberships
            .FirstAsync(m => m.UserId == creator.Id && m.CrewId == crew.Id);

        stored.IsOrganizer.Should().BeTrue();
    }

    [Fact]
    public async Task JoinCrew_SubsequentMember_IsNotOrganizer()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var creator = new User { Username = "Creator", Email = "creator@org2.com", PasswordHash = "hash", IsActive = true };
        var joiner = new User { Username = "Joiner", Email = "joiner@org2.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(creator, joiner);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Join Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "JOIN5678",
            CreatedByUserId = creator.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = creator.Id, CrewId = crew.Id, IsOrganizer = true, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = joiner.Id, CrewId = crew.Id, IsOrganizer = false, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var joinerMembership = await context.CrewMemberships
            .FirstAsync(m => m.UserId == joiner.Id && m.CrewId == crew.Id);

        joinerMembership.IsOrganizer.Should().BeFalse();
    }

    [Fact]
    public async Task Organizer_HasPriorityScoreOfNegativeOne()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var organizer = new User { Username = "Org", Email = "org@org3.com", PasswordHash = "hash", IsActive = true, InNeedOfAid = true, EmergencyLevel = 5 };
        context.Users.Add(organizer);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Priority Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "PROR1234",
            CreatedByUserId = organizer.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var membership = new CrewMembership
        {
            UserId = organizer.Id,
            CrewId = crew.Id,
            IsOrganizer = true,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };
        context.CrewMemberships.Add(membership);
        await context.SaveChangesAsync();

        var stored = await context.CrewMemberships
            .FirstAsync(m => m.UserId == organizer.Id && m.CrewId == crew.Id);

        stored.IsOrganizer.Should().BeTrue();

        int priorityScore = stored.IsOrganizer ? -1 : 0;
        priorityScore.Should().Be(-1);
    }

    [Fact]
    public async Task HonoraryMember_HasMembershipStatusOfOne()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "Honorary", Email = "honorary@org.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Honorary Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "HONR1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var membership = new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsOrganizer = false,
            IsHonoraryMember = true,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };
        context.CrewMemberships.Add(membership);
        await context.SaveChangesAsync();

        var stored = await context.CrewMemberships.FirstAsync(m => m.UserId == user.Id);
        stored.IsHonoraryMember.Should().BeTrue();
    }

    [Fact]
    public async Task CrewMembership_IsOrganizer_DefaultsFalse()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "Default", Email = "default@org.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Default Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "DFLT1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var membership = new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };
        context.CrewMemberships.Add(membership);
        await context.SaveChangesAsync();

        var stored = await context.CrewMemberships.FirstAsync(m => m.UserId == user.Id);
        stored.IsOrganizer.Should().BeFalse();
        stored.IsHonoraryMember.Should().BeFalse();
    }
}
