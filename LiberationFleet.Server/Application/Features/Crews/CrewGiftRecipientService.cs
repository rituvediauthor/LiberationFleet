using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Crews;

public class CrewGiftRecipientService(
    IMutualAidRepository mutualAidRepository,
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    IUnitOfWork unitOfWork)
{
    public const string DisplayName = "the crew";

    public async Task<User> GetOrCreateAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var username = UsernameForCrew(crewId);
        var existing = await userRepository.GetByEmailOrUsernameAsync(username, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null)
        {
            throw new InvalidOperationException($"Crew {crewId} was not found.");
        }

        var user = new User
        {
            Username = username,
            Email = PlaceholderUserDefaults.CreateInternalEmail(),
            PasswordHash = PlaceholderUserDefaults.PasswordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsUnclaimedPlaceholder = true,
            IsCrewGiftRecipient = true,
            InNeedOfAid = false
        };

        await userRepository.AddAsync(user, cancellationToken);
        await membershipRepository.AddAsync(new CrewMembership
        {
            User = user,
            CrewId = crewId,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    public static string UsernameForCrew(int crewId) => $"crew-gift-recipient-{crewId}";
}
