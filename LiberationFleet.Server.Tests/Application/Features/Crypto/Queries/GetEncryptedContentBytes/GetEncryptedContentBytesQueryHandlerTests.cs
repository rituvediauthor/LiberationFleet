using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContentBytes;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Queries.GetEncryptedContentBytes;

public class GetEncryptedContentBytesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsNull()
    {
        var handler = CreateHandler(userId: null);

        var result = await handler.Handle(
            new GetEncryptedContentBytesQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenBothCrewAndFleetMissing_ReturnsNull()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(
            new GetEncryptedContentBytesQuery(EncryptedContentTypeDto.VideoAsset, "vid-1"),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserNotInCrew_ReturnsNull()
    {
        var membership = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membership
            .Setup(r => r.IsUserInCrewAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler(userId: 1, membershipRepository: membership);

        var result = await handler.Handle(
            new GetEncryptedContentBytesQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEnvelopeExists_ReturnsDecodedBytesAndMetadata()
    {
        var plaintextBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var ciphertextBase64 = Convert.ToBase64String(plaintextBytes);

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
            KeyVersion = 2,
            Nonce = "test-nonce",
            Ciphertext = ciphertextBase64
        };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.VideoAsset,
                It.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == "vid-1"),
                10,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { envelope });

        var deepFreeze = new Mock<IMediaDeepFreezeService>(MockBehavior.Strict);
        deepFreeze
            .Setup(s => s.HydrateAsync(
                It.Is<IReadOnlyList<EncryptedContentEnvelope>>(list => list.Count == 1),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            userId: 1,
            membershipRepository: membership,
            cryptoRepository: cryptoRepository,
            deepFreezeService: deepFreeze);

        var result = await handler.Handle(
            new GetEncryptedContentBytesQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.ResourceId.Should().Be("vid-1");
        result.KeyVersion.Should().Be(2);
        result.Nonce.Should().Be("test-nonce");
        result.CiphertextBytes.Should().Equal(plaintextBytes);
        deepFreeze.VerifyAll();
    }

    [Fact]
    public async Task Handle_WhenCiphertextInvalidBase64_ReturnsNull()
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
            Nonce = "test-nonce",
            Ciphertext = "%%%not-base64%%%"
        };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.VideoAsset,
                It.IsAny<IReadOnlyList<string>>(),
                10,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { envelope });

        var deepFreeze = new Mock<IMediaDeepFreezeService>(MockBehavior.Loose);

        var handler = CreateHandler(
            userId: 1,
            membershipRepository: membership,
            cryptoRepository: cryptoRepository,
            deepFreezeService: deepFreeze);

        var result = await handler.Handle(
            new GetEncryptedContentBytesQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().BeNull();
    }

    private static GetEncryptedContentBytesQueryHandler CreateHandler(
        int? userId,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<ICryptoRepository>? cryptoRepository = null,
        Mock<IMediaDeepFreezeService>? deepFreezeService = null)
    {
        return new GetEncryptedContentBytesQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(userId).Object,
            (membershipRepository ?? HandlerTestFixture.CreateCrewMembershipRepositoryMock()).Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            (cryptoRepository ?? new Mock<ICryptoRepository>(MockBehavior.Strict)).Object,
            (deepFreezeService ?? new Mock<IMediaDeepFreezeService>(MockBehavior.Loose)).Object);
    }
}
