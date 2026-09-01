using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public sealed record CreatorContributionGiftDetails(
    int GiftId,
    int ContributorUserId,
    string ContributorUsername,
    decimal Amount,
    string ItemTitle,
    int RecipientUserId,
    string RecipientUsername,
    int CrewGiftRecipientUserId);

public class LibraryContributionGiftService(
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository,
    CrewGiftRecipientService crewGiftRecipientService)
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
        // Stock use awards a single peer gift: creator gets contribution credit (financial membership),
        // recipient gets reception credit toward their cycle.
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

        var details = await AwardCreatorContributionAsync(
            crewId,
            offering,
            quantity,
            newPossessorUserId,
            newPossessorUsername,
            cancellationToken);
        if (details is not null)
        {
            unit.CreatorContributionCredited = true;
        }

        return details;
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
        var gift = await CreatePeerGiftAsync(
            crewId,
            completerUserId,
            recipientUserId,
            amount,
            countsTowardContribution: true,
            countsTowardReception: true,
            offering.Title,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift.Id,
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
            countsTowardReception: true,
            offering.Title,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift.Id,
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
            gift.Id,
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

        var gift = await CreatePeerGiftAsync(
            crewId,
            completerUserId,
            creatorUserId,
            value,
            countsTowardContribution: true,
            countsTowardReception: true,
            taskTitle,
            cancellationToken);

        var crewRecipient = await crewGiftRecipientService.GetOrCreateAsync(crewId, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift.Id,
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
