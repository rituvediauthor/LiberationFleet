using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Queries.GetCrewGiftLog;

public class GetCrewGiftLogQueryHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenDuplicateCompletedGiftsExist_StillLoadsLog()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var platform = fixture.Platforms["PayPal"];

        var initiated = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            MiddlemanUserId = fixture.Carol.Id,
            Type = GiftType.Initiated,
            Amount = 20m,
            CrewPaymentPlatformId = platform.Id,
            VerificationStatus = GiftVerificationStatus.MiddlemanReceivedFunds,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        fixture.Context.Gifts.Add(initiated);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.AddRange(
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = fixture.Alice.Id,
                RecipientUserId = fixture.Bob.Id,
                MiddlemanUserId = fixture.Carol.Id,
                Type = GiftType.Completed,
                Amount = 20m,
                CrewPaymentPlatformId = platform.Id,
                InitiatedGiftId = initiated.Id,
                VerificationStatus = GiftVerificationStatus.Verified,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = fixture.Alice.Id,
                RecipientUserId = fixture.Bob.Id,
                MiddlemanUserId = fixture.Carol.Id,
                Type = GiftType.Completed,
                Amount = 20m,
                CrewPaymentPlatformId = platform.Id,
                InitiatedGiftId = initiated.Id,
                VerificationStatus = GiftVerificationStatus.Verified,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });
        await fixture.Context.SaveChangesAsync();

        var giftRepository = new GiftRepository(fixture.Context);
        var act = async () => await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(
            fixture.Crew.Id,
            CancellationToken.None);

        var map = await act.Should().NotThrowAsync();
        map.Subject.Should().ContainKey(initiated.Id);
    }

    [Fact]
    public async Task Handle_WhenMiddlemanMissing_StillMapsInitiatedGift()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var platform = fixture.Platforms["Venmo"];

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            GiverUser = fixture.Alice,
            RecipientUserId = fixture.Bob.Id,
            RecipientUser = fixture.Bob,
            MiddlemanUserId = null,
            MiddlemanUser = null,
            Type = GiftType.Initiated,
            Amount = 15m,
            CrewPaymentPlatform = platform,
            VerificationStatus = GiftVerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var membership = await fixture.Context.CrewMemberships
            .Include(m => m.Crew)
            .SingleAsync(m => m.UserId == fixture.Alice.Id);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(fixture.Alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var giftRepository = new Mock<IGiftRepository>();
        giftRepository
            .Setup(r => r.GetLogPageByCrewIdAsync(
                fixture.Crew.Id,
                It.IsAny<int>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GiftLogPage { Items = new List<Gift> { gift }, HasMore = false });
        giftRepository
            .Setup(r => r.GetCompletedGiftsByInitiatedIdsAsync(fixture.Crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Gift>());
        giftRepository
            .Setup(r => r.GetActiveLikeCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetActiveLikedGiftIdsByUserAsync(fixture.Alice.Id, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        giftRepository
            .Setup(r => r.GetCommentCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetSeasonStartDatesForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, DateTime?>());
        giftRepository
            .Setup(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cryptoRepository = new Mock<ICryptoRepository>();
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.GiftLogEntry,
                It.IsAny<IReadOnlyList<string>>(),
                fixture.Crew.Id,
                null,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EncryptedContentEnvelope>());

        var handler = new GetCrewGiftLogQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            membershipRepository.Object,
            giftRepository.Object,
            cryptoRepository.Object,
            new Mock<ILogger<GetCrewGiftLogQueryHandler>>().Object);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle();
        result.Items[0].Message.Should().Contain("a middleman");
    }
}
