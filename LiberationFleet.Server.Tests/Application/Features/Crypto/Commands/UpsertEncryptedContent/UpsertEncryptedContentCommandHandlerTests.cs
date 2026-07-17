using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContent;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Commands.UpsertEncryptedContent;

public class UpsertEncryptedContentCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsFailure()
    {
        var handler = CreateHandler(userId: null);

        var result = await handler.Handle(ValidImageCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task Handle_WhenGiftLogCiphertextTooLarge_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);
        var tooLarge = new string('x', (512 * 1024) + 1);

        var result = await handler.Handle(new UpsertEncryptedContentCommand(
            EncryptedContentTypeDto.GiftLogEntry,
            "gift-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            Ciphertext: tooLarge), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Encrypted gift log entry is too large.");
    }

    [Fact]
    public async Task Handle_WhenMediaCiphertextTooLarge_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);
        var tooLarge = new string('x', (20 * 1024 * 1024) + 1);

        var result = await handler.Handle(new UpsertEncryptedContentCommand(
            EncryptedContentTypeDto.ImageAsset,
            "img-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            Ciphertext: tooLarge), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Encrypted attachment is too large.");
    }

    [Fact]
    public async Task Handle_WhenBothCrewAndFleetMissing_ReturnsFailure()
    {
        var handler = CreateHandler(userId: 1);

        var result = await handler.Handle(new UpsertEncryptedContentCommand(
            EncryptedContentTypeDto.ImageAsset,
            "img-1",
            CrewId: null,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            Ciphertext: "cipher"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Exactly one of crew or fleet scope is required.");
    }

    private static UpsertEncryptedContentCommandHandler CreateHandler(int? userId)
    {
        return new UpsertEncryptedContentCommandHandler(
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

    private static UpsertEncryptedContentCommand ValidImageCommand() =>
        new(
            EncryptedContentTypeDto.ImageAsset,
            "img-1",
            CrewId: 10,
            FleetId: null,
            KeyVersion: 1,
            Nonce: "nonce",
            Ciphertext: "cipher");
}
