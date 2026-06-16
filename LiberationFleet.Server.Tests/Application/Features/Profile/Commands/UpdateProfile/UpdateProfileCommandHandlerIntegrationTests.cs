using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenValid_PersistsAllFieldsToDatabase()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();
        context.UserPaymentPlatforms.Add(new UserPaymentPlatform
        {
            UserId = user.Id,
            PaymentPlatformId = 1,
            Handle = "old@example.com"
        });
        await context.SaveChangesAsync();

        var userRepository = new UserRepository(context);
        var handler = new UpdateProfileCommandHandler(
            userRepository,
            new GiftRepository(context),
            new CrewMembershipRepository(context),
            new PaymentPlatformRepository(context),
            HandlerTestFixture.CreateCurrentUserServiceMock(user.Id).Object,
            context);

        var result = await handler.Handle(new UpdateProfileCommand
        {
            Username = "newname",
            Email = "new@example.com",
            InNeedOfAid = false,
            EmergencyLevel = 2,
            NeedsSurvivalAid = true,
            PaymentPlatforms =
            [
                new PaymentPlatformAccountDto { PlatformId = 3, Handle = "@new" }
            ]
        }, CancellationToken.None);

        result.Success.Should().BeTrue();

        var reloaded = await context.Users
            .Include(u => u.PaymentPlatforms)
            .SingleAsync(u => u.Id == user.Id);

        reloaded.Username.Should().Be("newname");
        reloaded.Email.Should().Be("new@example.com");
        reloaded.InNeedOfAid.Should().BeFalse();
        reloaded.EmergencyLevel.Should().Be(2);
        reloaded.NeedsSurvivalAid.Should().BeTrue();
        reloaded.PaymentPlatforms.Should().ContainSingle(p => p.PaymentPlatformId == 3 && p.Handle == "@new");
    }
}
