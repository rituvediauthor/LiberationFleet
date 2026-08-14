using FluentAssertions;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContentBytes;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Commands.UpsertEncryptedContentBytes;

public class UpsertEncryptedContentBytesCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsFailure()
    {
        var handler = CreateHandler(userId: null);

        var result = await handler.Handle(ValidVideoCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task Handle_WhenCiphertextEmpty_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(new UpsertEncryptedContentBytesCommand(
            EncryptedContentTypeDto.VideoAsset,
            "vid-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            CiphertextBytes: []), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Encrypted content payload is required.");
    }

    [Fact]
    public async Task Handle_WhenUnsupportedContentType_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(new UpsertEncryptedContentBytesCommand(
            EncryptedContentTypeDto.ForumPost,
            "post-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            CiphertextBytes: [1, 2, 3]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Binary upload is only supported for image, video, and audio.");
    }

    [Fact]
    public async Task Handle_WhenBothCrewAndFleetMissing_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(new UpsertEncryptedContentBytesCommand(
            EncryptedContentTypeDto.VideoAsset,
            "vid-1",
            CrewId: null,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            CiphertextBytes: [1, 2, 3]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Exactly one of crew or fleet scope is required.");
    }

    private static UpsertEncryptedContentBytesCommandHandler CreateHandler(int? userId)
    {
        return new UpsertEncryptedContentBytesCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(userId).Object,
            HandlerTestFixture.CreateCrewMembershipRepositoryMock().Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            HandlerTestFixture.CreateCrewRepositoryMock().Object,
            HandlerTestFixture.CreateGiftRepositoryMock().Object,
            new Mock<ICryptoRepository>(MockBehavior.Loose).Object,
            new Mock<IMediaDeepFreezeService>(MockBehavior.Loose).Object,
            HandlerTestFixture.CreateContentTenureService(),
            HandlerTestFixture.CreateUnitOfWorkMock().Object);
    }

    private static UpsertEncryptedContentBytesCommand ValidVideoCommand() =>
        new(
            EncryptedContentTypeDto.VideoAsset,
            "vid-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            CiphertextBytes: [1, 2, 3, 4]);
}
