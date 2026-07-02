using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public class LibraryContributionGiftService(
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository)
{
    public const string InKindPlatformName = "In-kind (Library)";

    public async Task<Gift> CreateContributionGiftAsync(
        int crewId,
        int contributorUserId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var platform = await crewPaymentPlatformRepository.GetByCrewAndNameAsync(
            crewId,
            InKindPlatformName,
            cancellationToken);

        if (platform is null)
        {
            platform = await crewPaymentPlatformRepository.AddAsync(new CrewPaymentPlatform
            {
                CrewId = crewId,
                Name = InKindPlatformName
            }, cancellationToken);
        }

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
}
