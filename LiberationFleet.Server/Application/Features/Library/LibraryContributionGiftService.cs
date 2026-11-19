using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// Holds the tracked <see cref="Gift"/> so <see cref="GiftId"/> is correct after SaveChanges
/// (identity columns are 0 until the insert commits).
/// </summary>
public sealed class CreatorContributionGiftDetails(
    Gift gift,
    int contributorUserId,
    string contributorUsername,
    decimal amount,
    string itemTitle,
    int recipientUserId,
    string recipientUsername,
    int crewGiftRecipientUserId)
{
    public Gift Gift { get; } = gift;
    public int GiftId => Gift.Id;
    public int ContributorUserId { get; } = contributorUserId;
    public string ContributorUsername { get; } = contributorUsername;
    public decimal Amount { get; } = amount;
    public string ItemTitle { get; } = itemTitle;
    public int RecipientUserId { get; } = recipientUserId;
    public string RecipientUsername { get; } = recipientUsername;
    public int CrewGiftRecipientUserId { get; } = crewGiftRecipientUserId;
}

public class LibraryContributionGiftService(
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository,
    CrewGiftRecipientService crewGiftRecipientService,
    IMutualAidService mutualAidService)
{
    public const string InKindPlatformName = "Library of Things";

    public async Task<CreatorContributionGiftDetails?> TryAwardCreatorForStockUseAsync(
        int crewId,
        LibraryOffering offering,
        int quantity,
        int recipientUserId,
        string recipientUsername,
        CancellationToken cancellationToken = default)
    {
        // Stock use awards a peer gift: creator contribution + recipient gift-log reception,
        // without filling season cycles / survival thresholds.
        return await TryAwardRecipientReceptionForStockUseAsync(
            crewId,
            offering,
            quantity,
            recipientUserId,
            recipientUsername,
            cancellationToken);
    }

    public async Task<CreatorContributionGiftDetails?> TryAwardCreatorForFirstDurableTransferAsync(
        int crewId,
        LibraryUnit unit,
        LibraryOffering offering,
        int newPossessorUserId,
        string newPossessorUsername,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (!LibraryOfferingRules.ShouldCreditCreatorForFirstDurableTransfer(unit, offering, newPossessorUserId))
        {
            return null;
        }

        // Peer gift to the new holder: creator lifetime↑, holder gift-log reception↑,
        // without filling season cycles / survival thresholds.
        var amount = LibraryOfferingRules.CalculateCreatorContributionAmount(offering, quantity);
        if (amount <= 0 || offering.CreatorUserId == newPossessorUserId)
        {
            return null;
        }

        var gift = await CreatePeerGiftAsync(
            crewId,
            offering.CreatorUserId,
            newPossessorUserId,
            amount,
            countsTowardContribution: true,
            countsTowardReception: false,
            offering.Title,
            cancellationToken);

        unit.CreatorContributionCredited = true;

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift,
            offering.CreatorUserId,
            offering.CreatorUser?.Username ?? "Crewmate",
            amount,
            offering.Title,
            newPossessorUserId,
            newPossessorUsername,
            crewRecipient.Id);
    }

    public async Task<CreatorContributionGiftDetails?> TryAwardCompleterForDurableHandoffAsync(
        int crewId,
        LibraryOffering offering,
        int quantity,
        int completerUserId,
        string completerUsername,
        int recipientUserId,
        string recipientUsername,
        CancellationToken cancellationToken = default)
    {
        if (completerUserId == recipientUserId)
        {
            return null;
        }

        var amount = LibraryOfferingRules.CalculateCompleterDurableContributionAmount(offering, quantity);
        if (amount <= 0)
        {
            return null;
        }

        // Handoff credit is peer-to-peer (provider → receiving crewmate), never "to the crew".
        // Gift-log reception rises; cycles/thresholds are not filled.
        var gift = await CreatePeerGiftAsync(
            crewId,
            completerUserId,
            recipientUserId,
            amount,
            countsTowardContribution: true,
            countsTowardReception: false,
            offering.Title,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift,
            completerUserId,
            completerUsername,
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername,
            crewRecipient.Id);
    }

    public async Task<CreatorContributionGiftDetails?> TryAwardRecipientReceptionForStockUseAsync(
        int crewId,
        LibraryOffering offering,
        int quantity,
        int recipientUserId,
        string recipientUsername,
        CancellationToken cancellationToken = default)
    {
        if (!LibraryOfferingRules.IsStockBased(offering)
            || !LibraryOfferingRules.ShouldCreditCreatorForStockUse(offering, recipientUserId))
        {
            return null;
        }

        var amount = LibraryOfferingRules.CalculateCreatorContributionAmount(offering, quantity);
        var gift = await CreatePeerGiftAsync(
            crewId,
            offering.CreatorUserId,
            recipientUserId,
            amount,
            countsTowardContribution: true,
            countsTowardReception: false,
            offering.Title,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift,
            offering.CreatorUserId,
            offering.CreatorUser?.Username ?? "Crewmate",
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername,
            crewRecipient.Id);
    }

    private async Task<CreatorContributionGiftDetails?> AwardCreatorContributionAsync(
        int crewId,
        LibraryOffering offering,
        int quantity,
        int recipientUserId,
        string recipientUsername,
        CancellationToken cancellationToken)
    {
        var amount = LibraryOfferingRules.CalculateCreatorContributionAmount(offering, quantity);
        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        var gift = await CreateContributionGiftAsync(
            crewId,
            offering.CreatorUserId,
            crewRecipient.Id,
            amount,
            offering.Title,
            cancellationToken);

        return new CreatorContributionGiftDetails(
            gift,
            offering.CreatorUserId,
            offering.CreatorUser?.Username ?? "Crewmate",
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername,
            crewRecipient.Id);
    }

    public async Task<Gift> CreateContributionGiftAsync(
        int crewId,
        int contributorUserId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return await CreateContributionGiftAsync(
            crewId,
            contributorUserId,
            crewRecipient.Id,
            amount,
            libraryItemTitle: null,
            cancellationToken);
    }

    public async Task<Gift> CreateContributionGiftAsync(
        int crewId,
        int contributorUserId,
        int crewRecipientUserId,
        decimal amount,
        CancellationToken cancellationToken = default) =>
        await CreateContributionGiftAsync(
            crewId,
            contributorUserId,
            crewRecipientUserId,
            amount,
            libraryItemTitle: null,
            cancellationToken);

    public async Task<Gift> CreateContributionGiftAsync(
        int crewId,
        int contributorUserId,
        int crewRecipientUserId,
        decimal amount,
        string? libraryItemTitle,
        CancellationToken cancellationToken = default)
    {
        var platform = await GetOrCreateInKindPlatformAsync(crewId, cancellationToken);

        var gift = new Gift
        {
            CrewId = crewId,
            GiverUserId = contributorUserId,
            RecipientUserId = crewRecipientUserId,
            Type = GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatform = platform,
            IsCustomGift = true,
            CountsTowardReception = false,
            CountsTowardContribution = true,
            LibraryItemTitle = TruncateTitle(libraryItemTitle),
            VerificationStatus = GiftVerificationStatus.Verified,
            ReceptionApplied = false,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await mutualAidService.CreditLifetimeContributionForGiftAsync(gift, cancellationToken);
        return gift;
    }

    private async Task<Gift> CreatePeerGiftAsync(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        bool countsTowardContribution,
        bool countsTowardReception,
        string? libraryItemTitle,
        CancellationToken cancellationToken)
    {
        var platform = await GetOrCreateInKindPlatformAsync(crewId, cancellationToken);

        var gift = new Gift
        {
            CrewId = crewId,
            GiverUserId = giverUserId,
            RecipientUserId = recipientUserId,
            Type = GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatform = platform,
            IsCustomGift = false,
            CountsTowardReception = countsTowardReception,
            CountsTowardContribution = countsTowardContribution,
            LibraryItemTitle = TruncateTitle(libraryItemTitle),
            VerificationStatus = GiftVerificationStatus.Verified,
            ReceptionApplied = false,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await mutualAidService.CreditLifetimeContributionForGiftAsync(gift, cancellationToken);
        return gift;
    }

    public async Task<CreatorContributionGiftDetails?> TryAwardTaskCompletionAsync(
        int crewId,
        string taskTitle,
        decimal value,
        int completerUserId,
        string completerUsername,
        int creatorUserId,
        string creatorUsername,
        CancellationToken cancellationToken = default)
    {
        if (completerUserId == creatorUserId || value <= 0)
        {
            return null;
        }

        // Completer contribution + quest-giver gift-log reception; not cycle/threshold categorized.
        var gift = await CreatePeerGiftAsync(
            crewId,
            completerUserId,
            creatorUserId,
            value,
            countsTowardContribution: true,
            countsTowardReception: false,
            taskTitle,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift,
            completerUserId,
            completerUsername,
            value,
            taskTitle,
            creatorUserId,
            creatorUsername,
            crewRecipient.Id);
    }

    private static string? TruncateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }

    private async Task<CrewPaymentPlatform> GetOrCreateInKindPlatformAsync(
        int crewId,
        CancellationToken cancellationToken)
    {
        var platform = await crewPaymentPlatformRepository.GetLibraryOfThingsPlatformAsync(
            crewId,
            cancellationToken);

        if (platform is not null)
        {
            return platform;
        }

        // Legacy rows may exist by name without the flag until migration backfill runs.
        platform = await crewPaymentPlatformRepository.GetByCrewAndNameAsync(
            crewId,
            InKindPlatformName,
            cancellationToken);

        if (platform is not null)
        {
            platform.IsLibraryOfThings = true;
            return platform;
        }

        return await crewPaymentPlatformRepository.AddAsync(new CrewPaymentPlatform
        {
            CrewId = crewId,
            Name = InKindPlatformName,
            IsLibraryOfThings = true
        }, cancellationToken);
    }
}
