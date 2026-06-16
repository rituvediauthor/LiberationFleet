using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static async Task SeedPaymentPlatformsAsync(ApplicationDbContext context)
    {
        if (await context.PaymentPlatforms.AnyAsync())
        {
            return;
        }

        context.PaymentPlatforms.AddRange(
            new PaymentPlatform { Id = 1, Name = "PayPal", SortOrder = 1 },
            new PaymentPlatform { Id = 2, Name = "Cash App", SortOrder = 2 },
            new PaymentPlatform { Id = 3, Name = "Venmo", SortOrder = 3 },
            new PaymentPlatform { Id = 4, Name = "Zelle", SortOrder = 4 },
            new PaymentPlatform { Id = 5, Name = "Other", SortOrder = 5 });
        await context.SaveChangesAsync();
    }

    public static ApplicationDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public static async Task<ApplicationDbContext> CreateWithUserAsync(
        string username = "testuser",
        string email = "test@example.com",
        string passwordHash = "hashed-password")
    {
        var context = Create();
        await SeedPaymentPlatformsAsync(context);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context;
    }

    public static async Task<(ApplicationDbContext Context, PasswordResetToken Token)> CreateWithResetTokenAsync(
        bool isUsed = false,
        DateTime? expiresAt = null)
    {
        var context = await CreateWithUserAsync();
        var user = context.Users.Single();

        var token = new PasswordResetToken
        {
            UserId = user.Id,
            Token = "valid-reset-token",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1),
            IsUsed = isUsed
        };

        context.PasswordResetTokens.Add(token);
        await context.SaveChangesAsync();

        return (context, token);
    }

    public static async Task<(ApplicationDbContext Context, User User, Crew Crew)> CreateWithCrewAsync(
        CrewScope scope = CrewScope.Online,
        CrewPrivacy privacy = CrewPrivacy.Public,
        string? zipCode = null)
    {
        var context = await CreateWithUserAsync();
        var user = context.Users.Single();
        var crew = new Crew
        {
            Name = "Fleet Alpha",
            MaxSize = 10,
            Privacy = privacy,
            Scope = scope,
            ZipCode = zipCode,
            RadiusMiles = scope == CrewScope.Local ? 25 : null,
            JoinCode = "JOIN1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return (context, user, crew);
    }

    public static async Task<ApplicationDbContext> CreateWithDashboardCrewsAsync()
    {
        var context = await CreateWithUserAsync();
        var user = context.Users.Single();

        context.Crews.AddRange(
            new Crew
            {
                Name = "Online Public",
                MaxSize = 10,
                Privacy = CrewPrivacy.Public,
                Scope = CrewScope.Online,
                JoinCode = "ONLINE01",
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            },
            new Crew
            {
                Name = "Online Private",
                MaxSize = 10,
                Privacy = CrewPrivacy.Private,
                Scope = CrewScope.Online,
                JoinCode = "ONLINE02",
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            },
            new Crew
            {
                Name = "Local Public",
                MaxSize = 10,
                Privacy = CrewPrivacy.Public,
                Scope = CrewScope.Local,
                ZipCode = "90210",
                RadiusMiles = 25,
                JoinCode = "LOCAL001",
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();
        return context;
    }
}
