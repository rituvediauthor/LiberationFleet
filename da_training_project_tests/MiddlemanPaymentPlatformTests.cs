using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class MiddlemanPaymentPlatformTests
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
    public async Task MiddlemanDetection_SharesPlatformWithBoth_IsValidMiddleman()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giverPlatformId = 1;
        var recipientPlatformId = 2;

        var giver = new User { Username = "Giver", Email = "giver@mm.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "Recipient", Email = "recipient@mm.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "Middleman", Email = "middleman@mm.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = giverPlatformId, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = recipientPlatformId, Handle = "recipient@cashapp" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = giverPlatformId, Handle = "mid@paypal" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = recipientPlatformId, Handle = "mid@cashapp" });
        await context.SaveChangesAsync();

        var giverPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == giver.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var recipientPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == recipient.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var middlemanPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == middleman.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var sharesWithGiver = middlemanPlatforms.Intersect(giverPlatforms).Any();
        var sharesWithRecipient = middlemanPlatforms.Intersect(recipientPlatforms).Any();

        sharesWithGiver.Should().BeTrue();
        sharesWithRecipient.Should().BeTrue();
    }

    [Fact]
    public async Task MiddlemanDetection_NoSharedPlatform_NotSuitable()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "GiverNS", Email = "giverns@mm.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "RecipientNS", Email = "recipientns@mm.com", PasswordHash = "hash", IsActive = true };
        var potential = new User { Username = "PotentialMM", Email = "potentialmm@mm.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, potential);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" },
            new UserPaymentPlatform { UserId = potential.Id, PaymentPlatformId = 3, Handle = "potential@venmo" });
        await context.SaveChangesAsync();

        var giverPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == giver.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var recipientPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == recipient.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var potentialPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == potential.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var sharesWithGiver = potentialPlatforms.Intersect(giverPlatforms).Any();
        var sharesWithRecipient = potentialPlatforms.Intersect(recipientPlatforms).Any();
        var isSuitableMiddleman = sharesWithGiver && sharesWithRecipient;

        isSuitableMiddleman.Should().BeFalse();
    }

    [Fact]
    public async Task DirectGiftPossible_WhenUsersSharePaymentPlatform()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "DirectGiver", Email = "directgiver@mm.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "DirectRecip", Email = "directrecip@mm.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 1, Handle = "recipient@paypal" });
        await context.SaveChangesAsync();

        var giverPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == giver.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var recipientPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == recipient.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var sharedPlatforms = giverPlatforms.Intersect(recipientPlatforms).ToList();
        sharedPlatforms.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NoSharedPlatform_NoMiddleman_ShowsNoSuitableMiddlemanNote()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "IsolatedGiver", Email = "isogiver@mm.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "IsolatedRecip", Email = "isorecip@mm.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" });
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Iso Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ISOL1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var giverPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == giver.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var recipientPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == recipient.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var sharedPlatforms = giverPlatforms.Intersect(recipientPlatforms).ToList();

        var crewMemberIds = await context.CrewMemberships
            .Where(m => m.CrewId == crew.Id && m.UserId != giver.Id && m.UserId != recipient.Id)
            .Select(m => m.UserId)
            .ToListAsync();

        var suitableMiddlemen = new List<int>();
        foreach (var memberId in crewMemberIds)
        {
            var memberPlatforms = await context.UserPaymentPlatforms
                .Where(p => p.UserId == memberId)
                .Select(p => p.PaymentPlatformId)
                .ToListAsync();

            if (memberPlatforms.Intersect(giverPlatforms).Any() &&
                memberPlatforms.Intersect(recipientPlatforms).Any())
            {
                suitableMiddlemen.Add(memberId);
            }
        }

        sharedPlatforms.Should().BeEmpty();
        suitableMiddlemen.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleMiddlemen_NoDefaultSelected_UserMustChoose()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "MultiGiver", Email = "multigiver@mm.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "MultiRecip", Email = "multirecip@mm.com", PasswordHash = "hash", IsActive = true };
        var mm1 = new User { Username = "MM1", Email = "mm1@mm.com", PasswordHash = "hash", IsActive = true };
        var mm2 = new User { Username = "MM2", Email = "mm2@mm.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, mm1, mm2);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" },
            new UserPaymentPlatform { UserId = mm1.Id, PaymentPlatformId = 1, Handle = "mm1@paypal" },
            new UserPaymentPlatform { UserId = mm1.Id, PaymentPlatformId = 2, Handle = "mm1@cashapp" },
            new UserPaymentPlatform { UserId = mm2.Id, PaymentPlatformId = 1, Handle = "mm2@paypal" },
            new UserPaymentPlatform { UserId = mm2.Id, PaymentPlatformId = 2, Handle = "mm2@cashapp" });
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Multi Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "MULT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = mm1.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = mm2.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var giverPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == giver.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var recipientPlatforms = await context.UserPaymentPlatforms
            .Where(p => p.UserId == recipient.Id)
            .Select(p => p.PaymentPlatformId)
            .ToListAsync();

        var crewMemberIds = await context.CrewMemberships
            .Where(m => m.CrewId == crew.Id && m.UserId != giver.Id && m.UserId != recipient.Id)
            .Select(m => m.UserId)
            .ToListAsync();

        var suitableMiddlemen = new List<int>();
        foreach (var memberId in crewMemberIds)
        {
            var memberPlatforms = await context.UserPaymentPlatforms
                .Where(p => p.UserId == memberId)
                .Select(p => p.PaymentPlatformId)
                .ToListAsync();

            if (memberPlatforms.Intersect(giverPlatforms).Any() &&
                memberPlatforms.Intersect(recipientPlatforms).Any())
            {
                suitableMiddlemen.Add(memberId);
            }
        }

        suitableMiddlemen.Should().HaveCount(2);
    }
}
