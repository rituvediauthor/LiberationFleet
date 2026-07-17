using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Services;

public class MediaDeepFreezeServiceTests
{
    [Fact]
    public async Task FreezeBatchAsync_MovesOldMediaCiphertextToColdStore()
    {
        var envelope = new EncryptedContentEnvelope
        {
            Id = 1,
            ContentType = EncryptedContentType.ImageAsset,
            ResourceId = "img-1",
            CrewId = 9,
            AuthorUserId = 3,
            Ciphertext = new string('A', 5000),
            StorageTier = EncryptedContentStorageTier.Hot,
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        };

        var crypto = new Mock<ICryptoRepository>();
        crypto.Setup(r => r.GetDeepFreezeCandidatesAsync(
                It.IsAny<IReadOnlyList<EncryptedContentType>>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([envelope]);

        var uploaded = new Dictionary<string, string>();
        var blob = new Mock<IDeepFreezeBlobStore>();
        blob.SetupGet(b => b.IsEnabled).Returns(true);
        blob.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, cipher, _) => uploaded[path] = cipher)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = CreateService(crypto.Object, blob.Object, uow.Object, enabled: true);

        var frozen = await service.FreezeBatchAsync();

        frozen.Should().Be(1);
        envelope.StorageTier.Should().Be(EncryptedContentStorageTier.DeepFreeze);
        envelope.Ciphertext.Should().BeEmpty();
        envelope.ColdBlobPath.Should().NotBeNullOrWhiteSpace();
        envelope.FrozenAt.Should().NotBeNull();
        uploaded.Should().ContainKey(envelope.ColdBlobPath!);
        uploaded[envelope.ColdBlobPath!].Length.Should().Be(5000);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HydrateAsync_RestoresCiphertextFromColdStoreWithoutPersisting()
    {
        var envelope = new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.VideoAsset,
            ResourceId = "vid-1",
            CrewId = 2,
            Ciphertext = string.Empty,
            StorageTier = EncryptedContentStorageTier.DeepFreeze,
            ColdBlobPath = "crew-2/10/vid-1.cipher"
        };

        var blob = new Mock<IDeepFreezeBlobStore>();
        blob.SetupGet(b => b.IsEnabled).Returns(true);
        blob.Setup(b => b.DownloadAsync("crew-2/10/vid-1.cipher", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cold-cipher");

        var uow = new Mock<IUnitOfWork>();
        var service = CreateService(Mock.Of<ICryptoRepository>(), blob.Object, uow.Object, enabled: true);

        await service.HydrateAsync([envelope]);

        envelope.Ciphertext.Should().Be("cold-cipher");
        envelope.StorageTier.Should().Be(EncryptedContentStorageTier.DeepFreeze);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FreezeBatchAsync_WhenDisabled_ReturnsZero()
    {
        var crypto = new Mock<ICryptoRepository>();
        var blob = new Mock<IDeepFreezeBlobStore>();
        blob.SetupGet(b => b.IsEnabled).Returns(false);
        var service = CreateService(crypto.Object, blob.Object, Mock.Of<IUnitOfWork>(), enabled: false);

        var frozen = await service.FreezeBatchAsync();

        frozen.Should().Be(0);
        crypto.Verify(
            r => r.GetDeepFreezeCandidatesAsync(
                It.IsAny<IReadOnlyList<EncryptedContentType>>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static MediaDeepFreezeService CreateService(
        ICryptoRepository crypto,
        IDeepFreezeBlobStore blob,
        IUnitOfWork uow,
        bool enabled)
    {
        var options = Options.Create(new MediaDeepFreezeOptions
        {
            Enabled = enabled,
            AgeDays = 60,
            BatchSize = 50,
            MinimumCiphertextChars = 4096,
            Provider = "local"
        });

        return new MediaDeepFreezeService(
            crypto,
            blob,
            uow,
            options,
            NullLogger<MediaDeepFreezeService>.Instance);
    }
}
