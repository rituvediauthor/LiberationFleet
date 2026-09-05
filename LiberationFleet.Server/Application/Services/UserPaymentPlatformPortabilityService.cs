using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

/// <summary>
/// Carries user payment handles across crew leave/join by detaching from the old
/// crew catalog and remounting (by name) onto the new crew's platforms.
/// </summary>
public class UserPaymentPlatformPortabilityService(
    IUserRepository userRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IUnitOfWork unitOfWork)
{
    public async Task DetachFromCrewAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null || user.PaymentPlatforms.Count == 0)
        {
            return;
        }

        foreach (var account in user.PaymentPlatforms.ToList())
        {
            var name = FirstNonEmpty(account.PlatformName, account.CrewPaymentPlatform?.Name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(account.Handle))
            {
                user.PaymentPlatforms.Remove(account);
                continue;
            }

            account.PlatformName = name.Trim();
            account.CrewPaymentPlatformId = null;
            account.CrewPaymentPlatform = null;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemountToCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null || user.PaymentPlatforms.Count == 0)
        {
            return;
        }

        foreach (var account in user.PaymentPlatforms.ToList())
        {
            var name = FirstNonEmpty(account.PlatformName, account.CrewPaymentPlatform?.Name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(account.Handle))
            {
                user.PaymentPlatforms.Remove(account);
                continue;
            }

            name = name.Trim();
            if (account.CrewPaymentPlatformId.HasValue
                && account.CrewPaymentPlatform is not null
                && account.CrewPaymentPlatform.CrewId == crewId
                && !account.CrewPaymentPlatform.IsLibraryOfThings)
            {
                account.PlatformName = account.CrewPaymentPlatform.Name;
                continue;
            }

            try
            {
                var platform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                    crewPaymentPlatformRepository,
                    unitOfWork,
                    crewId,
                    name,
                    cancellationToken);
                account.CrewPaymentPlatformId = platform.Id;
                account.CrewPaymentPlatform = platform;
                account.PlatformName = platform.Name;
            }
            catch (InvalidOperationException)
            {
                // Skip Library-of-Things / reserved names that cannot remount as payment methods.
                user.PaymentPlatforms.Remove(account);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
