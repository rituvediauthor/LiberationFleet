using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommandHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenJoinCodeMatchesCrewHomeCode_AddsMembership()
    {
        var (context, creator, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.JoinCode = "FLEET123";
            await context.SaveChangesAsync();

            var joiner = new User
            {
                Username = "joiner",
                Email = "joiner@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(joiner);
            await context.SaveChangesAsync();

            var crewRepository = new CrewRepository(context);
            var membershipRepository = new CrewMembershipRepository(context);
            var handler = new JoinCrewCommandHandler(
                crewRepository,
                membershipRepository,
                HandlerTestFixture.CreateCurrentUserServiceMock(joiner.Id).Object,
                context);

            var result = await handler.Handle(new JoinCrewCommand { JoinCode = "fleet123" }, CancellationToken.None);

            result.Success.Should().BeTrue();

            var membership = context.CrewMemberships
                .Single(m => m.UserId == joiner.Id && m.CrewId == crew.Id);

            membership.IsBanned.Should().BeFalse();
        }
    }
}
