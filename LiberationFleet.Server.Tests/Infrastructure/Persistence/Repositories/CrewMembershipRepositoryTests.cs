using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class CrewMembershipRepositoryTests
{
    [Fact]
    public async Task GetActiveMembershipAsync_ReturnsMembershipWithCrew()
    {
        var setup = await TestDbContextFactory.CreateWithCrewAsync();
        await using var context = setup.Context;
        var repository = new CrewMembershipRepository(context);

        var membership = await repository.GetActiveMembershipAsync(setup.User.Id);

        membership.Should().NotBeNull();
        membership!.Crew.Name.Should().Be("Fleet Alpha");
    }

    [Fact]
    public async Task GetActiveMembershipAsync_WhenUserHasNoMembership_ReturnsNull()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var repository = new CrewMembershipRepository(context);

        var membership = await repository.GetActiveMembershipAsync(context.Users.Single().Id);

        membership.Should().BeNull();
    }

    [Fact]
    public async Task IsUserBannedFromCrewAsync_WhenBanned_ReturnsTrue()
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

        var repository = new CrewMembershipRepository(context);
        var isBanned = await repository.IsUserBannedFromCrewAsync(bannedUser.Id, crew.Id);

        isBanned.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_PersistsMembershipAfterSaveChanges()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();

        context.Crews.Add(new Domain.Entities.Crew
        {
            Name = "Open Crew",
            MaxSize = 10,
            Privacy = Domain.Enums.CrewPrivacy.Public,
            Scope = Domain.Enums.CrewScope.Online,
            JoinCode = "OPEN1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var crew = context.Crews.Single();
        var repository = new CrewMembershipRepository(context);

        await repository.AddAsync(new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        context.CrewMemberships.Should().ContainSingle(m => m.UserId == user.Id && m.CrewId == crew.Id);
    }
}
