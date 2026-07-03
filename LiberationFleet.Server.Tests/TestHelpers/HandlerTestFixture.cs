using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace LiberationFleet.Server.Tests.TestHelpers;

public static class HandlerTestFixture
{
    public static Mock<IUserRepository> CreateUserRepositoryMock()
    {
        return new Mock<IUserRepository>(MockBehavior.Strict);
    }

    public static Mock<ICrewRepository> CreateCrewRepositoryMock()
    {
        return new Mock<ICrewRepository>(MockBehavior.Strict);
    }

    public static Mock<ICrewMembershipRepository> CreateCrewMembershipRepositoryMock()
    {
        return new Mock<ICrewMembershipRepository>(MockBehavior.Strict);
    }

    public static Mock<IGiftRepository> CreateGiftRepositoryMock()
    {
        return new Mock<IGiftRepository>(MockBehavior.Strict);
    }

    public static Mock<IPaymentPlatformRepository> CreatePaymentPlatformRepositoryMock(bool exists = true)
    {
        var mock = new Mock<IPaymentPlatformRepository>(MockBehavior.Strict);
        mock.Setup(r => r.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
        mock.Setup(r => r.GetAllOrderedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentPlatform>
            {
                new() { Id = 1, Name = "PayPal", SortOrder = 1 },
                new() { Id = 2, Name = "Cash App", SortOrder = 2 },
                new() { Id = 3, Name = "Venmo", SortOrder = 3 },
                new() { Id = 4, Name = "Zelle", SortOrder = 4 },
                new() { Id = 5, Name = "Other", SortOrder = 5 }
            });
        return mock;
    }

    public static Mock<ICrewPaymentPlatformRepository> CreateCrewPaymentPlatformRepositoryMock(
        bool exists = true,
        int crewId = 1)
    {
        var mock = new Mock<ICrewPaymentPlatformRepository>(MockBehavior.Strict);
        mock.Setup(r => r.ExistsForCrewAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
        mock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new CrewPaymentPlatform
            {
                Id = id,
                CrewId = crewId,
                Name = id switch
                {
                    1 => "PayPal",
                    2 => "Cash App",
                    3 => "Venmo",
                    _ => "Platform"
                }
            });
        mock.Setup(r => r.GetByCrewAndNameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewPaymentPlatform?)null);
        mock.Setup(r => r.AddAsync(It.IsAny<CrewPaymentPlatform>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewPaymentPlatform platform, CancellationToken _) =>
            {
                platform.Id = 99;
                return platform;
            });
        return mock;
    }

    public static CrewPaymentPlatform CreateCrewPaymentPlatform(int id = 1, int crewId = 1, string name = "PayPal")
    {
        return new CrewPaymentPlatform
        {
            Id = id,
            CrewId = crewId,
            Name = name
        };
    }

    public static Mock<ICurrentUserService> CreateCurrentUserServiceMock(int? userId = 1)
    {
        var mock = new Mock<ICurrentUserService>(MockBehavior.Strict);
        mock.Setup(c => c.UserId).Returns(userId);
        return mock;
    }

    public static Mock<IZipCodeDistanceService> CreateZipCodeDistanceServiceMock(double distanceMiles = 10)
    {
        var mock = new Mock<IZipCodeDistanceService>(MockBehavior.Strict);
        mock.Setup(z => z.TryGetDistanceMiles(It.IsAny<string>(), It.IsAny<string>(), out distanceMiles))
            .Returns(true);
        return mock;
    }

    public static Mock<IMutualAidService> CreateMutualAidServiceMock()
    {
        var mock = new Mock<IMutualAidService>(MockBehavior.Loose);
        mock.Setup(m => m.ApplyGiftReceptionAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(m => m.OnCrewmatePriorityChangedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(m => m.GetPriorityScoreForUserAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        mock.Setup(m => m.IsFinancialMemberAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CrewMembership>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock;
    }

    public static Mock<IUnitOfWork> CreateUnitOfWorkMock()
    {
        var mock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        mock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return mock;
    }

    public static NotificationService CreateNotificationService(ApplicationDbContext context)
    {
        var realtimeNotifier = new Mock<INotificationRealtimeNotifier>(MockBehavior.Loose);
        realtimeNotifier
            .Setup(n => n.NotifyReceivedAsync(It.IsAny<int>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        realtimeNotifier
            .Setup(n => n.NotifyUnreadCountUpdatedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new NotificationService(
            new NotificationRepository(context),
            realtimeNotifier.Object,
            context);
    }

    public static NotificationService CreateNotificationService(
        Mock<INotificationRepository>? notificationRepository = null,
        Mock<INotificationRealtimeNotifier>? realtimeNotifier = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        notificationRepository ??= new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(It.IsAny<int>(), It.IsAny<NotificationKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        realtimeNotifier ??= new Mock<INotificationRealtimeNotifier>(MockBehavior.Loose);
        unitOfWork ??= CreateUnitOfWorkMock();

        return new NotificationService(
            notificationRepository.Object,
            realtimeNotifier.Object,
            unitOfWork.Object);
    }

    public static MutualAidService CreateMutualAidService(ApplicationDbContext context)
    {
        var membershipRepository = new CrewMembershipRepository(context);
        return new MutualAidService(
            new MutualAidRepository(context),
            membershipRepository,
            CreateNotificationService(context),
            context);
    }

    public static Mock<IPasswordHasher> CreatePasswordHasherMock(
        string hashedPassword = "hashed-password",
        bool verifyResult = true)
    {
        var mock = new Mock<IPasswordHasher>(MockBehavior.Strict);
        mock.Setup(h => h.Hash(It.IsAny<string>())).Returns(hashedPassword);
        mock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(verifyResult);
        return mock;
    }

    public static Mock<ITokenService> CreateTokenServiceMock(string token = "jwt-token")
    {
        var mock = new Mock<ITokenService>(MockBehavior.Strict);
        mock.Setup(t => t.GenerateJwtToken(It.IsAny<User>())).Returns(token);
        return mock;
    }

    public static Mock<IPasswordResetTokenRepository> CreatePasswordResetTokenRepositoryMock()
    {
        return new Mock<IPasswordResetTokenRepository>(MockBehavior.Strict);
    }

    public static ILogger<T> CreateNullLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;

    public static User CreateUser(
        int id = 1,
        string username = "testuser",
        string email = "test@example.com",
        string passwordHash = "hashed-password")
    {
        return new User
        {
            Id = id,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static PasswordResetToken CreateResetToken(
        User user,
        string token = "reset-token",
        bool isUsed = false,
        DateTime? expiresAt = null)
    {
        return new PasswordResetToken
        {
            Id = 1,
            UserId = user.Id,
            User = user,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1),
            IsUsed = isUsed
        };
    }

    public static Crew CreateCrew(
        int id = 1,
        string name = "Test Crew",
        int maxSize = 10,
        CrewPrivacy privacy = CrewPrivacy.Public,
        CrewScope scope = CrewScope.Online,
        string? zipCode = null,
        int? radiusMiles = null,
        string joinCode = "ABC12345",
        int createdByUserId = 1)
    {
        return new Crew
        {
            Id = id,
            Name = name,
            MaxSize = maxSize,
            Privacy = privacy,
            Scope = scope,
            ZipCode = zipCode,
            RadiusMiles = radiusMiles,
            JoinCode = joinCode,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static CrewMembership CreateMembership(User user, Crew crew, bool isBanned = false)
    {
        return new CrewMembership
        {
            Id = 1,
            UserId = user.Id,
            User = user,
            CrewId = crew.Id,
            Crew = crew,
            IsBanned = isBanned,
            JoinedAt = DateTime.UtcNow
        };
    }
}
