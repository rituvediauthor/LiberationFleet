using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace da_training_project_tests;

public class GiftRecordingTests
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

    private static Mock<ICurrentUserService> CreateCurrentUserMock(int? userId)
    {
        var mock = new Mock<ICurrentUserService>(MockBehavior.Strict);
        mock.Setup(c => c.UserId).Returns(userId);
        return mock;
    }

    [Fact]
    public async Task RecordGift_NoAmountRestriction_AllowsGiftExceedingNeedAmount()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "Savannah", Email = "savannah@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "Dave", Email = "dave@gr.com", PasswordHash = "hash", IsActive = true, InNeedOfAid = true, EmergencyLevel = 2 };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Gift Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "GIFT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(giver.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(25m, 1, recipient.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await context.Gifts.FirstAsync(g => g.RecipientUserId == recipient.Id);
        gift.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task RecordGift_DirectGift_SharedPaymentPlatform()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "GiverA", Email = "givera@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "RecipientB", Email = "recipientb@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 1, Handle = "recipient@paypal" });
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Direct Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "DRCT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(giver.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(30m, 1, recipient.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var gift = await context.Gifts.FirstAsync();
        gift.Type.Should().Be(GiftType.Direct);
        gift.MiddlemanUserId.Should().BeNull();
    }

    [Fact]
    public async Task RecordGift_MiddlemanGift_InitiatedCorrectly()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "CrewmateA", Email = "crewmatea@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "CrewmateB", Email = "crewmateb@gr.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "CrewmateC", Email = "crewmatec@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "a@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "b@cashapp" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 1, Handle = "c@paypal" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 2, Handle = "c@cashapp" });
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Middle Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "MIDL1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = middleman.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(giver.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(30m, 1, recipient.Id, middleman.Id, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var gift = await context.Gifts.FirstAsync();
        gift.Type.Should().Be(GiftType.Initiated);
        gift.MiddlemanUserId.Should().Be(middleman.Id);
        gift.GiverUserId.Should().Be(giver.Id);
        gift.RecipientUserId.Should().Be(recipient.Id);
    }

    [Fact]
    public async Task RecordGift_MiddlemanCompletes_MarksAsCompleted()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "Giver", Email = "giver@comp.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "Recipient", Email = "recipient@comp.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "Middleman", Email = "middleman@comp.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Complete Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "COMP1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = middleman.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var initiated = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Initiated,
            Amount = 30m,
            PaymentPlatformId = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.Gifts.Add(initiated);
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(middleman.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(30m, 2, null, null, initiated.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var completed = await context.Gifts
            .Where(g => g.Type == GiftType.Completed)
            .SingleAsync();

        completed.InitiatedGiftId.Should().Be(initiated.Id);
        completed.MiddlemanUserId.Should().Be(middleman.Id);
        completed.Amount.Should().Be(30m);
    }

    [Fact]
    public async Task RecordGift_InitiatedGift_CountsAsContributionImmediately()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "ImmGiver", Email = "immgiver@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "ImmRecip", Email = "immrecip@gr.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "ImmMiddle", Email = "immmiddle@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Imm Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "IMMD1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = middleman.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(giver.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(50m, 1, recipient.Id, middleman.Id, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var giverGifts = await context.Gifts
            .Where(g => g.GiverUserId == giver.Id)
            .ToListAsync();

        giverGifts.Should().ContainSingle();
        giverGifts[0].Amount.Should().Be(50m);
        giverGifts[0].Type.Should().Be(GiftType.Initiated);
    }

    [Fact]
    public async Task RecordGift_CompletedGift_CountsTowardRecipientReception()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "RGiver", Email = "rgiver@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "RRecip", Email = "rrecip@gr.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "RMiddle", Email = "rmiddle@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Reception Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "RCPT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = middleman.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var initiated = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Initiated,
            Amount = 40m,
            PaymentPlatformId = 1,
            CreatedAt = DateTime.UtcNow,
            CountsTowardReception = false
        };
        context.Gifts.Add(initiated);
        await context.SaveChangesAsync();

        var completed = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Completed,
            Amount = 40m,
            PaymentPlatformId = 2,
            CreatedAt = DateTime.UtcNow,
            InitiatedGiftId = initiated.Id,
            CountsTowardReception = true
        };
        context.Gifts.Add(completed);
        await context.SaveChangesAsync();

        var completedGift = await context.Gifts
            .Where(g => g.Type == GiftType.Completed && g.RecipientUserId == recipient.Id)
            .SingleAsync();

        completedGift.CountsTowardReception.Should().BeTrue();
        completedGift.Amount.Should().Be(40m);
    }

    [Fact]
    public async Task RecordGift_SelfGift_ReturnsFailure()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "SelfUser", Email = "self@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Self Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "SELF1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.Add(
            new CrewMembership { UserId = user.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(user.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(20m, 1, user.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot give a gift to yourself.");
    }

    [Fact]
    public async Task RecordGift_CannotBeMiddlemanForOwnGift()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "OwnMiddle", Email = "ownmiddle@gr.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "OwnRecip", Email = "ownrecip@gr.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Own Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "OWNM1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = giver.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var handler = new RecordGiftCommandHandler(
            CreateCurrentUserMock(giver.Id).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            context);

        var result = await handler.Handle(
            new RecordGiftCommand(30m, 1, recipient.Id, giver.Id, null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot be the middleman for your own gift.");
    }
}
