using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
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
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);
            context.UserPaymentPlatforms.Add(new UserPaymentPlatform
            {
                UserId = user.Id,
                CrewPaymentPlatformId = platforms["PayPal"].Id,
                Handle = "old@example.com"
            });
            await context.SaveChangesAsync();

            var userRepository = new UserRepository(context);
            var membershipRepository = new CrewMembershipRepository(context);
            var mutualAidService = new MutualAidService(new MutualAidRepository(context), membershipRepository, context);
            var handler = new UpdateProfileCommandHandler(
                userRepository,
                new GiftRepository(context),
                membershipRepository,
                new CrewPaymentPlatformRepository(context),
                HandlerTestFixture.CreateCurrentUserServiceMock(user.Id).Object,
                mutualAidService,
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
                    new PaymentPlatformAccountDto { PlatformId = platforms["Venmo"].Id, Handle = "@new" }
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
            reloaded.PaymentPlatforms.Should().ContainSingle(p =>
                p.CrewPaymentPlatformId == platforms["Venmo"].Id && p.Handle == "@new");
        }
    }
}
