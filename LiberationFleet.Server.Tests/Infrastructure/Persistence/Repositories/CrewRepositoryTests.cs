using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class CrewRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsMatchingCrew()
    {
        var setup = await TestDbContextFactory.CreateWithCrewAsync();
        await using var context = setup.Context;
        var repository = new CrewRepository(context);

        var result = await repository.GetByIdAsync(setup.Crew.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Fleet Alpha");
    }

    [Fact]
    public async Task GetByJoinCodeAsync_ReturnsMatchingCrew()
    {
        var setup = await TestDbContextFactory.CreateWithCrewAsync();
        await using var context = setup.Context;
        var repository = new CrewRepository(context);

        var result = await repository.GetByJoinCodeAsync("JOIN1234");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchPublicAsync_ReturnsOnlyPublicCrewsWithMatchingScope()
    {
        await using var context = await TestDbContextFactory.CreateWithDashboardCrewsAsync();
        var repository = new CrewRepository(context);

        var onlineResults = await repository.SearchPublicAsync(CrewScope.Online);
        var localResults = await repository.SearchPublicAsync(CrewScope.Local);

        onlineResults.Should().ContainSingle(c => c.Name == "Online Public");
        localResults.Should().ContainSingle(c => c.Name == "Local Public");
    }

    [Fact]
    public async Task CountMembersAsync_ExcludesBannedMembers()
    {
        var setup = await TestDbContextFactory.CreateWithCrewAsync();
        await using var context = setup.Context;
        var crew = setup.Crew;

        context.Users.Add(new User
        {
            Username = "banneduser",
            Email = "banned@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var bannedUser = context.Users.Single(u => u.Email == "banned@example.com");
        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = bannedUser.Id,
            CrewId = crew.Id,
            IsBanned = true,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new CrewRepository(context);
        var count = await repository.CountMembersAsync(crew.Id);

        count.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_PersistsCrewAfterSaveChanges()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();
        var repository = new CrewRepository(context);

        await repository.AddAsync(new Crew
        {
            Name = "New Crew",
            MaxSize = 8,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "NEWCODE1",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        context.Crews.Should().ContainSingle(c => c.Name == "New Crew");
    }
}
