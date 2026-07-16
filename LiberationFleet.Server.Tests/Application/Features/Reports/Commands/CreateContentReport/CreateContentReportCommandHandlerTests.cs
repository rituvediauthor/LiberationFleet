using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Reports.Commands.CreateContentReport;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Reports.Commands.CreateContentReport;

public class CreateContentReportCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsFailure()
    {
        var handler = CreateHandler(userId: null);

        var result = await handler.Handle(ValidHarassmentCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task Handle_WhenEvidenceMissing_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(ValidHarassmentCommand() with { EvidencePlaintextJson = " " }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Report evidence is required.");
    }

    [Fact]
    public async Task Handle_WhenEvidenceIsNotJson_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(ValidHarassmentCommand() with { EvidencePlaintextJson = "not-json" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Report evidence must be valid JSON.");
    }

    [Fact]
    public async Task Handle_WhenHarassment_ReceivesReportWithoutFreezingAuthor()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var reportRepository = CreateReportRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var author = HandlerTestFixture.CreateUser(id: 99, username: "author");

        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var handler = CreateHandler(
            userId: 1,
            reportRepository: reportRepository,
            userRepository: userRepository,
            unitOfWork: unitOfWork);

        var result = await handler.Handle(ValidHarassmentCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ContentReportStatus.Received);
        author.IsActive.Should().BeTrue();
        reportRepository.Verify(r => r.SoftDeleteTargetAsync(
            It.IsAny<ContentReportTargetType>(),
            It.IsAny<int>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCsam_QueuesForNcmecQuarantinesAndFreezesAuthor()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var reportRepository = CreateReportRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var author = HandlerTestFixture.CreateUser(id: 99, username: "author");

        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        userRepository
            .Setup(r => r.UpdateAsync(author, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        reportRepository
            .Setup(r => r.SoftDeleteTargetAsync(
                ContentReportTargetType.ChatMessage,
                55,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            userId: 1,
            reportRepository: reportRepository,
            userRepository: userRepository,
            unitOfWork: unitOfWork);

        var result = await handler.Handle(new CreateContentReportCommand(
            ContentReportReason.ChildSexualExploitation,
            ContentReportTargetType.ChatMessage,
            TargetResourceId: 55,
            TargetParentId: null,
            TargetAuthorUserId: 99,
            CrewId: 10,
            FleetId: null,
            ReporterNote: "illegal",
            EvidencePlaintextJson: """{"text":"evidence"}""",
            AlsoBlockAuthor: false), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ContentReportStatus.QueuedForNcmec);
        author.IsActive.Should().BeFalse();
        reportRepository.Verify(r => r.SoftDeleteTargetAsync(
            ContentReportTargetType.ChatMessage,
            55,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        userRepository.Verify(r => r.UpdateAsync(author, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreateContentReportCommandHandler CreateHandler(
        int? userId,
        Mock<IContentReportRepository>? reportRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        reportRepository ??= CreateReportRepositoryMock();
        userRepository ??= HandlerTestFixture.CreateUserRepositoryMock();
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();

        var evidence = new Mock<IReportEvidenceProtector>(MockBehavior.Strict);
        evidence.Setup(p => p.Seal(It.IsAny<string>())).Returns(("nonce", "cipher"));

        var blocks = new Mock<IUserBlockRepository>(MockBehavior.Loose);
        var friendships = new Mock<IFriendshipRepository>(MockBehavior.Loose);

        return new CreateContentReportCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(userId).Object,
            reportRepository.Object,
            userRepository.Object,
            blocks.Object,
            friendships.Object,
            evidence.Object,
            unitOfWork.Object);
    }

    private static Mock<IContentReportRepository> CreateReportRepositoryMock()
    {
        var mock = new Mock<IContentReportRepository>(MockBehavior.Strict);
        mock.Setup(r => r.AddAsync(It.IsAny<ContentReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(r => r.AddAccessLogAsync(It.IsAny<ContentReportAccessLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static CreateContentReportCommand ValidHarassmentCommand() =>
        new(
            ContentReportReason.Harassment,
            ContentReportTargetType.ChatMessage,
            TargetResourceId: 55,
            TargetParentId: null,
            TargetAuthorUserId: 99,
            CrewId: 10,
            FleetId: null,
            ReporterNote: "mean",
            EvidencePlaintextJson: """{"text":"evidence"}""",
            AlsoBlockAuthor: false);
}
