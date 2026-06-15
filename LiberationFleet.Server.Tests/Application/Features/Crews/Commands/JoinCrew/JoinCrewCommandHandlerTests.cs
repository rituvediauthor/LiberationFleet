using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new JoinCrewCommand { CrewId = 1 }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyHasCrew_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = CreateHandler(currentUserId: user.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(new JoinCrewCommand { CrewId = 2 }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are already a member of a crew");
    }

    [Fact]
    public async Task Handle_WhenCrewNotFound_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = SetupNoExistingMembership(user.Id);

        crewRepository
            .Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Crew?)null);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository);

        var result = await handler.Handle(new JoinCrewCommand { CrewId = 99 }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Crew not found");
    }

    [Fact]
    public async Task Handle_WhenUserIsBanned_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = SetupNoExistingMembership(user.Id);

        crewRepository
            .Setup(r => r.GetByIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(crew);

        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(user.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository);

        var result = await handler.Handle(new JoinCrewCommand { CrewId = crew.Id }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are banned from this crew");
    }

    [Fact]
    public async Task Handle_WhenCrewIsFull_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew(maxSize: 2);
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = SetupNoExistingMembership(user.Id);

        crewRepository
            .Setup(r => r.GetByJoinCodeAsync("JOIN1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(crew);

        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(user.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        crewRepository
            .Setup(r => r.CountMembersAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository);

        var result = await handler.Handle(new JoinCrewCommand { JoinCode = "join1234" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("This crew is full");
    }

    [Fact]
    public async Task Handle_WhenValidJoinByCrewId_AddsMembership()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = SetupNoExistingMembership(user.Id);
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        crewRepository
            .Setup(r => r.GetByIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(crew);

        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(user.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        crewRepository
            .Setup(r => r.CountMembersAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        CrewMembership? capturedMembership = null;
        membershipRepository
            .Setup(r => r.AddAsync(It.IsAny<CrewMembership>(), It.IsAny<CancellationToken>()))
            .Callback<CrewMembership, CancellationToken>((m, _) => capturedMembership = m)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository, unitOfWork: unitOfWork);

        var result = await handler.Handle(new JoinCrewCommand { CrewId = crew.Id }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Joined crew successfully");
        result.Crew!.MemberCount.Should().Be(2);

        capturedMembership!.UserId.Should().Be(user.Id);
        capturedMembership.CrewId.Should().Be(crew.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ICrewMembershipRepository> SetupNoExistingMembership(int userId)
    {
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);
        return membershipRepository;
    }

    private static JoinCrewCommandHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewRepository>? crewRepository = null,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        crewRepository ??= HandlerTestFixture.CreateCrewRepositoryMock();
        membershipRepository ??= SetupNoExistingMembership(currentUserId ?? 1);
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();

        return new JoinCrewCommandHandler(
            crewRepository.Object,
            membershipRepository.Object,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            unitOfWork.Object);
    }
}
