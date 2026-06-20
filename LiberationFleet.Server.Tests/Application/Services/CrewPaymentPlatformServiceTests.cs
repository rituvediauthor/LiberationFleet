using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Services;

public class CrewPaymentPlatformServiceTests
{
    [Fact]
    public void GetCommonPlatforms_ReturnsSharedPlatformsOrderedByName()
    {
        var paypal = HandlerTestFixture.CreateCrewPaymentPlatform(1, 1, "PayPal");
        var venmo = HandlerTestFixture.CreateCrewPaymentPlatform(2, 1, "Venmo");
        var cashApp = HandlerTestFixture.CreateCrewPaymentPlatform(3, 1, "Cash App");

        var first = new User
        {
            PaymentPlatforms =
            [
                new UserPaymentPlatform { CrewPaymentPlatformId = paypal.Id, CrewPaymentPlatform = paypal, Handle = "@a" },
                new UserPaymentPlatform { CrewPaymentPlatformId = venmo.Id, CrewPaymentPlatform = venmo, Handle = "@a-venmo" }
            ]
        };

        var second = new User
        {
            PaymentPlatforms =
            [
                new UserPaymentPlatform { CrewPaymentPlatformId = venmo.Id, CrewPaymentPlatform = venmo, Handle = "@b" },
                new UserPaymentPlatform { CrewPaymentPlatformId = cashApp.Id, CrewPaymentPlatform = cashApp, Handle = "@b-cash" }
            ]
        };

        var common = CrewPaymentPlatformService.GetCommonPlatforms(first, second);

        common.Should().HaveCount(1);
        common[0].Id.Should().Be(venmo.Id);
        common[0].Name.Should().Be("Venmo");
    }

    [Fact]
    public void GetCommonPlatforms_WhenNoOverlap_ReturnsEmpty()
    {
        var paypal = HandlerTestFixture.CreateCrewPaymentPlatform(1, 1, "PayPal");
        var venmo = HandlerTestFixture.CreateCrewPaymentPlatform(2, 1, "Venmo");

        var first = new User
        {
            PaymentPlatforms =
            [
                new UserPaymentPlatform { CrewPaymentPlatformId = paypal.Id, CrewPaymentPlatform = paypal, Handle = "@a" }
            ]
        };

        var second = new User
        {
            PaymentPlatforms =
            [
                new UserPaymentPlatform { CrewPaymentPlatformId = venmo.Id, CrewPaymentPlatform = venmo, Handle = "@b" }
            ]
        };

        CrewPaymentPlatformService.GetCommonPlatforms(first, second).Should().BeEmpty();
    }

    [Fact]
    public void MapCrewMemberPlatforms_UsesPreferredPlatformWhenPresent()
    {
        var paypal = HandlerTestFixture.CreateCrewPaymentPlatform(1, 1, "PayPal");
        var venmo = HandlerTestFixture.CreateCrewPaymentPlatform(2, 1, "Venmo");
        var user = new User
        {
            Id = 5,
            Username = "alice",
            PaymentPlatforms =
            [
                new UserPaymentPlatform
                {
                    CrewPaymentPlatformId = paypal.Id,
                    CrewPaymentPlatform = paypal,
                    Handle = "@alice-paypal"
                },
                new UserPaymentPlatform
                {
                    CrewPaymentPlatformId = venmo.Id,
                    CrewPaymentPlatform = venmo,
                    Handle = "@alice-venmo",
                    IsPreferred = true
                }
            ]
        };

        var membership = new CrewMembership { UserId = user.Id, User = user };
        var mapped = CrewPaymentPlatformService.MapCrewMemberPlatforms(membership);

        mapped.UserId.Should().Be(5);
        mapped.Username.Should().Be("alice");
        mapped.PlatformIds.Should().BeEquivalentTo([paypal.Id, venmo.Id]);
        mapped.PreferredPlatformId.Should().Be(venmo.Id);
        mapped.PreferredPlatformName.Should().Be("Venmo");
        mapped.PreferredPlatformHandle.Should().Be("@alice-venmo");
        mapped.PlatformAccounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task EnsurePlatformAsync_WhenPlatformExists_ReturnsExistingWithoutAdding()
    {
        var existing = HandlerTestFixture.CreateCrewPaymentPlatform(7, 3, "PayPal");
        var repository = HandlerTestFixture.CreateCrewPaymentPlatformRepositoryMock();
        repository
            .Setup(r => r.GetByCrewAndNameAsync(3, "PayPal", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var result = await CrewPaymentPlatformService.EnsurePlatformAsync(
            repository.Object,
            unitOfWork.Object,
            3,
            "PayPal",
            CancellationToken.None);

        result.Should().BeSameAs(existing);
        repository.Verify(r => r.AddAsync(It.IsAny<CrewPaymentPlatform>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsurePlatformAsync_WhenPlatformMissing_CreatesAndSaves()
    {
        var repository = HandlerTestFixture.CreateCrewPaymentPlatformRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        var result = await CrewPaymentPlatformService.EnsurePlatformAsync(
            repository.Object,
            unitOfWork.Object,
            3,
            "  Custom Platform  ",
            CancellationToken.None);

        result.Name.Should().Be("Custom Platform");
        result.CrewId.Should().Be(3);
        repository.Verify(r => r.AddAsync(It.IsAny<CrewPaymentPlatform>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
