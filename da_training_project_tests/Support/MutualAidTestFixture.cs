using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;
using LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace da_training_project_tests.Support;

public static class MutualAidTestFixture
{
    public static ApplicationDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

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

    public static IMutualAidCalculationService CreateCalculationService(ApplicationDbContext context) =>
        new MutualAidCalculationService(context);

    public static IReceptionOrderService CreateReceptionOrderService(ApplicationDbContext context) =>
        new ReceptionOrderService(context, CreateCalculationService(context));

    public static Mock<ICurrentUserService> CreateCurrentUserMock(int? userId)
    {
        var mock = new Mock<ICurrentUserService>(MockBehavior.Strict);
        mock.Setup(c => c.UserId).Returns(userId);
        return mock;
    }

    public static RecordGiftCommandHandler CreateRecordGiftHandler(ApplicationDbContext context, int userId) =>
        new(
            CreateCurrentUserMock(userId).Object,
            new CrewMembershipRepository(context),
            new GiftRepository(context),
            new PaymentPlatformRepository(context),
            CreateReceptionOrderService(context),
            context);

    public static CreateCrewCommandHandler CreateCreateCrewHandler(ApplicationDbContext context, int userId) =>
        new(
            new CrewRepository(context),
            new CrewMembershipRepository(context),
            CreateCurrentUserMock(userId).Object,
            context);

    public static JoinCrewCommandHandler CreateJoinCrewHandler(ApplicationDbContext context, int userId) =>
        new(
            new CrewRepository(context),
            new CrewMembershipRepository(context),
            CreateCurrentUserMock(userId).Object,
            context);

    public static GetMyProfileQueryHandler CreateGetMyProfileHandler(ApplicationDbContext context, int userId) =>
        new(
            new UserRepository(context),
            new GiftRepository(context),
            new CrewMembershipRepository(context),
            CreateCurrentUserMock(userId).Object,
            CreateCalculationService(context));

    public static GetReceptionOrderQueryHandler CreateGetReceptionOrderHandler(ApplicationDbContext context, int userId) =>
        new(
            CreateCurrentUserMock(userId).Object,
            new CrewMembershipRepository(context),
            CreateReceptionOrderService(context));

    public static async Task<(Crew crew, User creator)> SeedCrewWithCreatorAsync(
        ApplicationDbContext context,
        string crewName = "Test Crew",
        string joinCode = "TEST1234")
    {
        var creator = new User
        {
            Username = "Creator",
            Email = $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "hash",
            IsActive = true,
            InNeedOfAid = true,
            EmergencyLevel = 2
        };
        context.Users.Add(creator);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = crewName,
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = joinCode,
            CreatedByUserId = creator.Id,
            CreatedAt = DateTime.UtcNow,
            CurrentSeasonStartDate = DateTime.UtcNow.AddDays(-30)
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = creator.Id,
            CrewId = crew.Id,
            IsBanned = false,
            IsOrganizer = true,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return (crew, creator);
    }

    public static async Task<User> AddCrewMemberAsync(
        ApplicationDbContext context,
        Crew crew,
        string username,
        bool isOrganizer = false,
        bool needsSurvivalAid = false,
        int emergencyLevel = 2,
        bool inNeedOfAid = true)
    {
        var user = new User
        {
            Username = username,
            Email = $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "hash",
            IsActive = true,
            InNeedOfAid = inNeedOfAid,
            EmergencyLevel = emergencyLevel,
            NeedsSurvivalAid = needsSurvivalAid
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsBanned = false,
            IsOrganizer = isOrganizer,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        return user;
    }

    public static async Task SeedContributionGiftAsync(
        ApplicationDbContext context,
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        DateTime createdAt,
        GiftType type = GiftType.Direct)
    {
        context.Gifts.Add(new Gift
        {
            CrewId = crewId,
            GiverUserId = giverUserId,
            RecipientUserId = recipientUserId,
            Type = type,
            Amount = amount,
            PaymentPlatformId = 1,
            CreatedAt = createdAt,
            CountsTowardReception = type != GiftType.Initiated
        });
        await context.SaveChangesAsync();
    }
}
