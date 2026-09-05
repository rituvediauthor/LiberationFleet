using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public static class CrewPaymentPlatformService
{
    public static async Task<CrewPaymentPlatform> EnsurePlatformAsync(
        ICrewPaymentPlatformRepository repository,
        IUnitOfWork unitOfWork,
        int crewId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (string.Equals(trimmed, LibraryContributionGiftService.InKindPlatformName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Library of Things is a system platform and cannot be added as a payment method.");
        }

        var existing = await repository.GetByCrewAndNameAsync(crewId, trimmed, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsLibraryOfThings)
            {
                throw new InvalidOperationException(
                    "Library of Things is a system platform and cannot be added as a payment method.");
            }

            return existing;
        }

        var platform = await repository.AddAsync(new CrewPaymentPlatform
        {
            CrewId = crewId,
            Name = trimmed
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return platform;
    }

    public static IReadOnlyList<PaymentPlatformOptionDto> GetCommonPlatforms(
        User first,
        User second)
    {
        var firstPlatforms = first.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue
                && (p.CrewPaymentPlatform is null || !p.CrewPaymentPlatform.IsLibraryOfThings))
            .ToDictionary(p => p.CrewPaymentPlatformId!.Value);
        return second.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue
                && (p.CrewPaymentPlatform is null || !p.CrewPaymentPlatform.IsLibraryOfThings)
                && firstPlatforms.ContainsKey(p.CrewPaymentPlatformId!.Value))
            .Select(p => new PaymentPlatformOptionDto
            {
                Id = p.CrewPaymentPlatformId!.Value,
                Name = p.CrewPaymentPlatform?.Name ?? p.PlatformName
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    public static CrewMemberPlatforms MapCrewMemberPlatforms(CrewMembership membership)
    {
        var accounts = membership.User.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue
                && (p.CrewPaymentPlatform is null || !p.CrewPaymentPlatform.IsLibraryOfThings))
            .ToList();
        var preferred = accounts.FirstOrDefault(p => p.IsPreferred)
            ?? accounts.FirstOrDefault();

        return new CrewMemberPlatforms
        {
            UserId = membership.UserId,
            Username = membership.User.Username,
            IsIntermediary = membership.IsIntermediary,
            PlatformIds = accounts.Select(p => p.CrewPaymentPlatformId!.Value).ToList(),
            PlatformAccounts = accounts
                .Select(p => new PlatformAccountDto
                {
                    PlatformId = p.CrewPaymentPlatformId!.Value,
                    Name = p.CrewPaymentPlatform?.Name ?? p.PlatformName,
                    Handle = p.Handle
                })
                .ToList(),
            PreferredPlatformId = preferred?.CrewPaymentPlatformId,
            PreferredPlatformName = preferred?.CrewPaymentPlatform?.Name ?? preferred?.PlatformName,
            PreferredPlatformHandle = preferred?.Handle
        };
    }
}

public class PaymentPlatformOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
