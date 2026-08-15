using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContentMeta;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Queries.GetEncryptedContentMeta;

public class GetEncryptedContentMetaQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenEnvelopeExists_ReturnsNonceWithoutLoadingBytes()
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
            KeyVersion = 2,
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { envelope });

        var handler = new GetEncryptedContentMetaQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            membership.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            cryptoRepository.Object);

        var result = await handler.Handle(
            new GetEncryptedContentMetaQuery(EncryptedContentTypeDto.VideoAsset, "vid-1", CrewId: 10),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.ResourceId.Should().Be("vid-1");
        result.KeyVersion.Should().Be(2);
        result.Nonce.Should().Be(PlainMediaFraming.Nonce);
        cryptoRepository.VerifyAll();
    }
}
