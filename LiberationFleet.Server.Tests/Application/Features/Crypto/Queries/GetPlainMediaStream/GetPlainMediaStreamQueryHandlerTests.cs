using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetPlainMediaStream;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Queries.GetPlainMediaStream;

public class GetPlainMediaStreamQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenEnvelopeIsPlain_ReturnsContentStream()
    {
        var membership = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membership
            .Setup(r => r.IsUserInCrewAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.VideoAsset,
            ResourceId = "vid-1",
            CrewId = 10,
            AuthorUserId = 1,
            KeyVersion = 1,
            Nonce = PlainMediaFraming.Nonce,
            Ciphertext = string.Empty
        };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.VideoAsset,
                It.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == "vid-1"),
                10,
                null,
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { envelope });

        await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var opened = new PlainMediaContentStream
        {
            ContentStream = content,
            ContentType = "video/mp4",
            ContentLength = 3
        };

        var deepFreeze = new Mock<IMediaDeepFreezeService>(MockBehavior.Strict);
        deepFreeze
            .Setup(s => s.OpenPlainMediaContentAsync(envelope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(opened);

        var handler = new GetPlainMediaStreamQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            membership.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            HandlerTestFixture.CreateFriendshipRepositoryMock().Object,
            HandlerTestFixture.CreateUserBlockRepositoryMock().Object,
            cryptoRepository.Object,
            deepFreeze.Object);

        var result = await handler.Handle(
            new GetPlainMediaStreamQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("video/mp4");
        result.ContentLength.Should().Be(3);
        deepFreeze.VerifyAll();
    }

    [Fact]
    public async Task Handle_WhenNonceIsEncrypted_ReturnsNull()
    {
        var membership = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membership
            .Setup(r => r.IsUserInCrewAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.VideoAsset,
            ResourceId = "vid-1",
            CrewId = 10,
            AuthorUserId = 1,
            KeyVersion = 1,
            Nonce = "encrypted-nonce",
            Ciphertext = string.Empty
        };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.VideoAsset,
                It.IsAny<IReadOnlyList<string>>(),
                10,
                null,
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { envelope });

        var deepFreeze = new Mock<IMediaDeepFreezeService>(MockBehavior.Strict);

        var handler = new GetPlainMediaStreamQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            membership.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            HandlerTestFixture.CreateFriendshipRepositoryMock().Object,
            HandlerTestFixture.CreateUserBlockRepositoryMock().Object,
            cryptoRepository.Object,
            deepFreeze.Object);

        var result = await handler.Handle(
            new GetPlainMediaStreamQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().BeNull();
    }
}
