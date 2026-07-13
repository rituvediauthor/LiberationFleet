using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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
        var existing = await repository.GetByCrewAndNameAsync(crewId, trimmed, cancellationToken);
        if (existing is not null)
        {
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
        var firstPlatforms = first.PaymentPlatforms.ToDictionary(p => p.CrewPaymentPlatformId);
        return second.PaymentPlatforms
            .Where(p => firstPlatforms.ContainsKey(p.CrewPaymentPlatformId))
            .Select(p => new PaymentPlatformOptionDto
            {
                Id = p.CrewPaymentPlatformId,
                Name = p.CrewPaymentPlatform.Name
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    public static CrewMemberPlatforms MapCrewMemberPlatforms(CrewMembership membership)
    {
        var preferred = membership.User.PaymentPlatforms.FirstOrDefault(p => p.IsPreferred)
            ?? membership.User.PaymentPlatforms.FirstOrDefault();

        return new CrewMemberPlatforms
        {
            UserId = membership.UserId,
            Username = membership.User.Username,
            IsIntermediary = membership.IsIntermediary,
            PlatformIds = membership.User.PaymentPlatforms.Select(p => p.CrewPaymentPlatformId).ToList(),
            PlatformAccounts = membership.User.PaymentPlatforms
                .Select(p => new PlatformAccountDto
                {
                    PlatformId = p.CrewPaymentPlatformId,
                    Name = p.CrewPaymentPlatform?.Name ?? string.Empty,
                    Handle = p.Handle
                })
                .ToList(),
            PreferredPlatformId = preferred?.CrewPaymentPlatformId,
            PreferredPlatformName = preferred?.CrewPaymentPlatform?.Name,
            PreferredPlatformHandle = preferred?.Handle
        };
    }
}

public class PaymentPlatformOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
