using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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
    string RecipientUsername);

public class LibraryContributionGiftService(
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository)
{
    public const string InKindPlatformName = "In-kind (Library)";

    public async Task<CreatorContributionGiftDetails?> TryAwardCreatorForStockUseAsync(
        int crewId,
        LibraryOffering offering,
        int quantity,
        int recipientUserId,
        string recipientUsername,
        CancellationToken cancellationToken = default)
    {
        if (!LibraryOfferingRules.ShouldCreditCreatorForStockUse(offering, recipientUserId))
        {
            return null;
        }

        return await AwardCreatorContributionAsync(
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
        var amount = LibraryOfferingRules.CalculateCompleterDurableContributionAmount(offering, quantity);
        if (amount <= 0)
        {
            return null;
        }

        var gift = await CreateContributionGiftAsync(crewId, completerUserId, amount, cancellationToken);
        return new CreatorContributionGiftDetails(
            gift.Id,
            completerUserId,
            completerUsername,
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername);
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
        var gift = await CreateReceptionGiftAsync(
            crewId,
            offering.CreatorUserId,
            recipientUserId,
            amount,
            cancellationToken);

        return new CreatorContributionGiftDetails(
            gift.Id,
            offering.CreatorUserId,
            offering.CreatorUser?.Username ?? "Crewmate",
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername);
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
        var gift = await CreateContributionGiftAsync(
            crewId,
            offering.CreatorUserId,
            amount,
            cancellationToken);

        return new CreatorContributionGiftDetails(
            gift.Id,
            offering.CreatorUserId,
            offering.CreatorUser?.Username ?? "Crewmate",
            amount,
            offering.Title,
            recipientUserId,
            recipientUsername);
    }

    public async Task<Gift> CreateContributionGiftAsync(
        int crewId,
        int contributorUserId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var platform = await GetOrCreateInKindPlatformAsync(crewId, cancellationToken);

        var gift = new Gift
        {
            CrewId = crewId,
            GiverUserId = contributorUserId,
            RecipientUserId = contributorUserId,
            Type = GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatform = platform,
            IsCustomGift = true,
            CountsTowardReception = false,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Verified,
            ReceptionApplied = false,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        return gift;
    }

    private async Task<Gift> CreateReceptionGiftAsync(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
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
            CountsTowardReception = true,
            CountsTowardContribution = false,
            VerificationStatus = GiftVerificationStatus.Verified,
            ReceptionApplied = false,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        return gift;
    }

    private async Task<CrewPaymentPlatform> GetOrCreateInKindPlatformAsync(
        int crewId,
        CancellationToken cancellationToken)
    {
        var platform = await crewPaymentPlatformRepository.GetByCrewAndNameAsync(
            crewId,
            InKindPlatformName,
            cancellationToken);

        if (platform is not null)
        {
            return platform;
        }

        return await crewPaymentPlatformRepository.AddAsync(new CrewPaymentPlatform
        {
            CrewId = crewId,
            Name = InKindPlatformName
        }, cancellationToken);
    }
}
