using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

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

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are already a member of a crew");
    }

    [Fact]
    public async Task Handle_WhenValidOnlineCrew_CreatesCrewAndMembership()
    {
        var user = HandlerTestFixture.CreateUser();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);

        Crew? capturedCrew = null;
        crewRepository
            .Setup(r => r.GetByJoinCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Crew?)null);
        crewRepository
            .Setup(r => r.AddAsync(It.IsAny<Crew>(), It.IsAny<CancellationToken>()))
            .Callback<Crew, CancellationToken>((crew, _) =>
            {
                crew.Id = 1;
                capturedCrew = crew;
            })
            .Returns(Task.CompletedTask);

        CrewMembership? capturedMembership = null;
        membershipRepository
            .Setup(r => r.AddAsync(It.IsAny<CrewMembership>(), It.IsAny<CancellationToken>()))
            .Callback<CrewMembership, CancellationToken>((membership, _) => capturedMembership = membership)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository, unitOfWork: unitOfWork);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Crew created successfully");
        result.Crew.Should().NotBeNull();
        result.Crew!.Name.Should().Be("My Crew");
        result.Crew.MemberCount.Should().Be(1);

        capturedCrew.Should().NotBeNull();
        capturedCrew!.Privacy.Should().Be(CrewPrivacy.Public);
        capturedCrew.Scope.Should().Be(CrewScope.Online);
        capturedCrew.ZipCode.Should().BeNull();
        capturedCrew.JoinCode.Should().HaveLength(8);

        capturedMembership.Should().NotBeNull();
        capturedMembership!.UserId.Should().Be(user.Id);
        capturedMembership.CrewId.Should().Be(1);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenValidLocalCrew_PersistsZipAndRadius()
    {
        var user = HandlerTestFixture.CreateUser();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);

        Crew? capturedCrew = null;
        crewRepository
            .Setup(r => r.GetByJoinCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Crew?)null);
        crewRepository
            .Setup(r => r.AddAsync(It.IsAny<Crew>(), It.IsAny<CancellationToken>()))
            .Callback<Crew, CancellationToken>((crew, _) =>
            {
                crew.Id = 2;
                capturedCrew = crew;
            })
            .Returns(Task.CompletedTask);

        membershipRepository
            .Setup(r => r.AddAsync(It.IsAny<CrewMembership>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository, unitOfWork: unitOfWork);

        var command = ValidCommand();
        command.Scope = "Local";
        command.ZipCode = "90210";
        command.RadiusMiles = 50;

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedCrew!.Scope.Should().Be(CrewScope.Local);
        capturedCrew.ZipCode.Should().Be("90210");
        capturedCrew.RadiusMiles.Should().Be(50);
    }

    [Fact]
    public async Task Handle_WhenGeneratedJoinCodeAlreadyExists_RetriesUntilUnique()
    {
        var user = HandlerTestFixture.CreateUser();
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var attempts = 0;

        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);

        crewRepository
            .Setup(r => r.GetByJoinCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                return attempts == 1 ? HandlerTestFixture.CreateCrew() : null;
            });

        crewRepository
            .Setup(r => r.AddAsync(It.IsAny<Crew>(), It.IsAny<CancellationToken>()))
            .Callback<Crew, CancellationToken>((crew, _) => crew.Id = 1)
            .Returns(Task.CompletedTask);

        membershipRepository
            .Setup(r => r.AddAsync(It.IsAny<CrewMembership>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(currentUserId: user.Id, crewRepository: crewRepository, membershipRepository: membershipRepository, unitOfWork: unitOfWork);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        attempts.Should().Be(2);
        crewRepository.Verify(r => r.GetByJoinCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static CreateCrewCommand ValidCommand() => new()
    {
        Name = "My Crew",
        MaxSize = 10,
        Privacy = "Public",
        Scope = "Online"
    };

    private static CreateCrewCommandHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewRepository>? crewRepository = null,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        crewRepository ??= HandlerTestFixture.CreateCrewRepositoryMock();
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();

        return new CreateCrewCommandHandler(
            crewRepository.Object,
            membershipRepository.Object,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            HandlerTestFixture.CreateContentTenureService(membershipRepository: membershipRepository),
            unitOfWork.Object);
    }
}
