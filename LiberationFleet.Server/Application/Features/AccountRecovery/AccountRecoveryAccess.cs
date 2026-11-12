using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.AccountRecovery;

public static class AccountRecoveryAccess
{
    public sealed class ValidationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public int CrewId { get; init; }
        public User? OldUser { get; init; }
        public User? NewUser { get; init; }

        public static ValidationResult Fail(string message) => new() { Success = false, Message = message };
    }

    public static async Task<ValidationResult> ValidateSameCrewAsync(
        ICrewMembershipRepository membershipRepository,
        IUserRepository userRepository,
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken)
    {
        var newMembership = await membershipRepository.GetActiveMembershipAsync(newUserId, cancellationToken);
        if (newMembership is null)
        {
            return ValidationResult.Fail("You must be in a crew to recover another account.");
        }

        var oldMembership = await membershipRepository.GetMembershipAsync(
            oldUserId,
            newMembership.CrewId,
            cancellationToken);
        if (oldMembership is null || oldMembership.LeftAt is not null || oldMembership.IsBanned)
        {
            return ValidationResult.Fail("That account is not an active crewmate in your crew.");
        }

        var newUser = await userRepository.GetByIdWithProfileAsync(newUserId, cancellationToken);
        var oldUser = await userRepository.GetByIdWithProfileAsync(oldUserId, cancellationToken);
        if (newUser is null || oldUser is null)
        {
            return ValidationResult.Fail("Account not found.");
        }

        if (!newUser.IsActive || !oldUser.IsActive)
        {
            return ValidationResult.Fail("Both accounts must be active.");
        }

        if (newUser.IsUnclaimedPlaceholder || oldUser.IsUnclaimedPlaceholder)
        {
            return ValidationResult.Fail("Placeholder accounts cannot be recovered this way.");
        }

        return new ValidationResult
        {
            Success = true,
            CrewId = newMembership.CrewId,
            OldUser = oldUser,
            NewUser = newUser
        };
    }
}
