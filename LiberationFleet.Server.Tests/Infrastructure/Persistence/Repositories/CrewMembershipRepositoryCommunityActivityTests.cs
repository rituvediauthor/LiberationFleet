using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class CrewMembershipRepositoryCommunityActivityTests
{
    [Fact]
    public async Task CountRecentlyActiveCommunityAsync_CountsDistinctActiveCrewsAndCrewmates()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedPaymentPlatformsAsync(context);

        var now = DateTime.UtcNow;
        var activeUserA = CreateUser(1, "active-a", lastLoginAt: now.AddDays(-2));
        var activeUserB = CreateUser(2, "active-b", lastLoginAt: now.AddDays(-10));
        var staleUser = CreateUser(3, "stale", lastLoginAt: now.AddDays(-45));
        var placeholder = CreateUser(4, "ph", lastLoginAt: now.AddDays(-1), isPlaceholder: true);
        var inactive = CreateUser(5, "inactive", lastLoginAt: now.AddDays(-1), isActive: false);

        var crew1 = CreateCrew(10, activeUserA.Id, "Crew One");
        var crew2 = CreateCrew(11, activeUserB.Id, "Crew Two");
        var crewIdle = CreateCrew(12, staleUser.Id, "Idle Crew");

        context.Users.AddRange(activeUserA, activeUserB, staleUser, placeholder, inactive);
        context.Crews.AddRange(crew1, crew2, crewIdle);
        context.CrewMemberships.AddRange(
            CreateMembership(activeUserA.Id, crew1.Id),
            CreateMembership(activeUserB.Id, crew1.Id), // same crew as A → one crew, two mates
            CreateMembership(activeUserB.Id, crew2.Id),
            CreateMembership(staleUser.Id, crewIdle.Id),
            CreateMembership(placeholder.Id, crew1.Id, isPlaceholderMember: true),
            CreateMembership(inactive.Id, crew2.Id));
        await context.SaveChangesAsync();

        var repository = new CrewMembershipRepository(context);
        var (activeCrewCount, activeCrewmateCount) =
            await repository.CountRecentlyActiveCommunityAsync(now.AddDays(-30));

        activeCrewCount.Should().Be(2);
        activeCrewmateCount.Should().Be(2);
    }

    [Fact]
    public async Task CountRecentlyActiveCommunityAsync_IgnoresLeftAndBannedMemberships()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedPaymentPlatformsAsync(context);

        var now = DateTime.UtcNow;
        var leftUser = CreateUser(1, "left", lastLoginAt: now.AddDays(-1));
        var bannedUser = CreateUser(2, "banned", lastLoginAt: now.AddDays(-1));
        var crew = CreateCrew(10, leftUser.Id, "Crew");

        context.Users.AddRange(leftUser, bannedUser);
        context.Crews.Add(crew);
        context.CrewMemberships.AddRange(
            CreateMembership(leftUser.Id, crew.Id, leftAt: now.AddDays(-1)),
            CreateMembership(bannedUser.Id, crew.Id, isBanned: true));
        await context.SaveChangesAsync();

        var repository = new CrewMembershipRepository(context);
        var (activeCrewCount, activeCrewmateCount) =
            await repository.CountRecentlyActiveCommunityAsync(now.AddDays(-30));

        activeCrewCount.Should().Be(0);
        activeCrewmateCount.Should().Be(0);
    }

    private static User CreateUser(
        int id,
        string username,
        DateTime? lastLoginAt,
        bool isActive = true,
        bool isPlaceholder = false) =>
        new()
        {
            Id = id,
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = "hash",
            IsActive = isActive,
            IsUnclaimedPlaceholder = isPlaceholder,
            LastLoginAt = lastLoginAt,
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        };

    private static Crew CreateCrew(int id, int createdByUserId, string name) =>
        new()
        {
            Id = id,
            Name = name,
            MaxSize = 20,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = $"JOIN{id}",
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        };

    private static CrewMembership CreateMembership(
        int userId,
        int crewId,
        bool isBanned = false,
        DateTime? leftAt = null,
        bool isPlaceholderMember = false) =>
        new()
        {
            UserId = userId,
            CrewId = crewId,
            IsBanned = isBanned,
            LeftAt = leftAt,
            IsPlaceholderMember = isPlaceholderMember,
            JoinedAt = DateTime.UtcNow.AddDays(-60)
        };
}
