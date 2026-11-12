using LiberationFleet.Server.Application.Features.AccountRecovery;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.AccountRecovery;

public class AccountRecoveryMergeServiceTests
{
    [Fact]
    public async Task MergeAsync_MovesNewGiftOwnership_SwapsKeys_AndSoftDeletesNewAccount()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var oldUser = fixture.Alice;
        var newUser = fixture.Bob;

        fixture.Context.UserKeyBundles.AddRange(
            new UserKeyBundle
            {
                UserId = oldUser.Id,
                IdentityPublicKey = "old-pub",
                KeyVersion = 1,
                UpdatedAt = DateTime.UtcNow
            },
            new UserKeyBundle
            {
                UserId = newUser.Id,
                IdentityPublicKey = "new-pub",
                KeyVersion = 2,
                UpdatedAt = DateTime.UtcNow
            });
        fixture.Context.UserPrivateKeyBackups.AddRange(
            new UserPrivateKeyBackup
            {
                UserId = oldUser.Id,
                Salt = "old-salt",
                Iv = "old-iv",
                Ciphertext = "old-ct",
                KeyVersion = 1
            },
            new UserPrivateKeyBackup
            {
                UserId = newUser.Id,
                Salt = "new-salt",
                Iv = "new-iv",
                Ciphertext = "new-ct",
                KeyVersion = 2
            });
        fixture.Context.CrewKeyDistributions.AddRange(
            new CrewKeyDistribution
            {
                CrewId = fixture.Crew.Id,
                UserId = oldUser.Id,
                KeyVersion = 1,
                WrappedCrewKey = "wrap-old",
                WrapNonce = "nonce-old",
                WrappedByUserId = oldUser.Id,
                CreatedAt = DateTime.UtcNow
            },
            new CrewKeyDistribution
            {
                CrewId = fixture.Crew.Id,
                UserId = newUser.Id,
                KeyVersion = 1,
                WrappedCrewKey = "wrap-new",
                WrapNonce = "nonce-new",
                WrappedByUserId = newUser.Id,
                CreatedAt = DateTime.UtcNow
            });
        fixture.Context.Gifts.Add(new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = newUser.Id,
            RecipientUserId = fixture.Carol.Id,
            Type = GiftType.Direct,
            Amount = 42m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            VerificationStatus = GiftVerificationStatus.Verified,
            CreatedAt = DateTime.UtcNow
        });
        fixture.Context.EncryptedContentEnvelopes.Add(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProfileAvatar,
            ResourceId = "avatar-old",
            AuthorUserId = oldUser.Id,
            KeyVersion = 1,
            Nonce = "n",
            Ciphertext = "c",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        oldUser.AvatarResourceId = "avatar-old";
        oldUser.LocationCiphertext = "loc-ct";
        oldUser.LocationNonce = "loc-n";
        oldUser.LocationKeyVersion = 1;
        await fixture.Context.SaveChangesAsync();

        var service = CreateMergeService(fixture);
        var result = await service.MergeAsync(
            fixture.Crew.Id,
            newUserId: newUser.Id,
            oldUserId: oldUser.Id,
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);

        var gift = await fixture.Context.Gifts.SingleAsync(g => g.Amount == 42m);
        gift.GiverUserId.Should().Be(oldUser.Id);

        var bundle = await fixture.Context.UserKeyBundles.SingleAsync(b => b.UserId == oldUser.Id);
        bundle.IdentityPublicKey.Should().Be("new-pub");
        bundle.KeyVersion.Should().Be(2);
        (await fixture.Context.UserKeyBundles.CountAsync(b => b.UserId == newUser.Id)).Should().Be(0);

        var backup = await fixture.Context.UserPrivateKeyBackups.SingleAsync(b => b.UserId == oldUser.Id);
        backup.Ciphertext.Should().Be("new-ct");

        var wraps = await fixture.Context.CrewKeyDistributions
            .Where(d => d.CrewId == fixture.Crew.Id)
            .ToListAsync();
        wraps.Should().ContainSingle();
        wraps[0].UserId.Should().Be(oldUser.Id);
        wraps[0].WrappedCrewKey.Should().Be("wrap-new");

        (await fixture.Context.EncryptedContentEnvelopes
            .CountAsync(e => e.ResourceId == "avatar-old")).Should().Be(0);

        var surviving = await fixture.Context.Users.SingleAsync(u => u.Id == oldUser.Id);
        surviving.IsActive.Should().BeTrue();
        surviving.Username.Should().Be("alice");
        surviving.LocationCiphertext.Should().BeNull();
        surviving.AvatarResourceId.Should().BeNull();

        var deleted = await fixture.Context.Users.SingleAsync(u => u.Id == newUser.Id);
        deleted.IsActive.Should().BeFalse();
        deleted.Username.Should().StartWith("deleted");

        (await fixture.Context.CrewMemberships
            .CountAsync(m => m.UserId == newUser.Id && m.CrewId == fixture.Crew.Id)).Should().Be(0);
        (await fixture.Context.CrewMemberships
            .CountAsync(m => m.UserId == oldUser.Id && m.CrewId == fixture.Crew.Id && m.LeftAt == null))
            .Should().Be(1);
    }

    [Fact]
    public async Task MergeAsync_WhenAccountsDifferInCrew_Fails()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == fixture.Bob.Id);
        membership.LeftAt = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();

        var service = CreateMergeService(fixture);
        var result = await service.MergeAsync(
            fixture.Crew.Id,
            newUserId: fixture.Bob.Id,
            oldUserId: fixture.Alice.Id,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("active members");
    }

    [Fact]
    public async Task MergeAsync_PreservesDirectMessagesWithThirdParty()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var oldUser = fixture.Alice;
        var newUser = fixture.Bob;
        var friend = fixture.Carol;

        var oldLow = Math.Min(oldUser.Id, friend.Id);
        var oldHigh = Math.Max(oldUser.Id, friend.Id);
        var newLow = Math.Min(newUser.Id, friend.Id);
        var newHigh = Math.Max(newUser.Id, friend.Id);

        var oldConvo = new DirectConversation
        {
            UserLowId = oldLow,
            UserHighId = oldHigh,
            CreatedAt = DateTime.UtcNow
        };
        var newConvo = new DirectConversation
        {
            UserLowId = newLow,
            UserHighId = newHigh,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.DirectConversations.AddRange(oldConvo, newConvo);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.DirectMessages.AddRange(
            new DirectMessage
            {
                ConversationId = oldConvo.Id,
                AuthorUserId = oldUser.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new DirectMessage
            {
                ConversationId = newConvo.Id,
                AuthorUserId = newUser.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        fixture.Context.EncryptedContentEnvelopes.Add(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.DirectMessage,
            ResourceId = $"dm-{oldConvo.Id}-1",
            AuthorUserId = oldUser.Id,
            RecipientUserId = friend.Id,
            KeyVersion = 1,
            Nonce = "n1",
            Ciphertext = "old-dm-ct",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var service = CreateMergeService(fixture);
        var result = await service.MergeAsync(
            fixture.Crew.Id,
            newUserId: newUser.Id,
            oldUserId: oldUser.Id,
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);

        var conversations = await fixture.Context.DirectConversations
            .Where(c => (c.UserLowId == oldUser.Id && c.UserHighId == friend.Id)
                || (c.UserLowId == friend.Id && c.UserHighId == oldUser.Id))
            .ToListAsync();
        conversations.Should().ContainSingle();

        var messages = await fixture.Context.DirectMessages
            .Where(m => m.ConversationId == conversations[0].Id)
            .ToListAsync();
        messages.Should().HaveCount(2);
        messages.Select(m => m.AuthorUserId).Should().OnlyContain(id => id == oldUser.Id || id == friend.Id);
        messages.Count(m => m.AuthorUserId == oldUser.Id).Should().Be(2);

        (await fixture.Context.EncryptedContentEnvelopes
            .CountAsync(e => e.ResourceId == $"dm-{oldConvo.Id}-1")).Should().Be(1);
    }

    private static AccountRecoveryMergeService CreateMergeService(MutualAidSeasonFixture fixture)
    {
        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var fleetRepository = new FleetRepository(fixture.Context);
        var libraryRepository = new LibraryRepository(fixture.Context);
        var cryptoRepository = new CryptoRepository(fixture.Context);
        var requestCleanup = new LibraryRequestCleanupHelper(libraryRepository, cryptoRepository);
        var libraryCleanup = new LibraryMemberCleanupService(libraryRepository, cryptoRepository, requestCleanup);
        var contentTenure = new ContentTenureService(
            new ContentTenureRepository(fixture.Context),
            membershipRepository,
            fleetRepository);
        var fleetMembership = new FleetMembershipService(fleetRepository, contentTenure);
        var emptyCrewCleanup = new EmptyCrewCleanupService(
            membershipRepository,
            new CrewCleanupRepository(fixture.Context));

        return new AccountRecoveryMergeService(
            new UserRepository(fixture.Context),
            membershipRepository,
            fleetRepository,
            new GiftRepository(fixture.Context),
            new MutualAidRepository(fixture.Context),
            fixture.Service,
            new AccountRecoveryRepository(fixture.Context),
            libraryCleanup,
            emptyCrewCleanup,
            fleetMembership,
            contentTenure,
            HandlerTestFixture.CreatePasswordHasherMock("scrambled").Object,
            fixture.Context);
    }
}
