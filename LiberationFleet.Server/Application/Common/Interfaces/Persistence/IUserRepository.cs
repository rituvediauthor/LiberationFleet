using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IUserRepository
{
    Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithProfileAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, string?>> GetAvatarResourceIdsAsync(
        IReadOnlyList<int> userIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> SearchByUsernameAsync(string usernameFragment, int limit = 20, CancellationToken cancellationToken = default);
    Task<bool> IsUsernameTakenByOtherUserAsync(string username, int userId, CancellationToken cancellationToken = default);
    Task<bool> IsEmailTakenByOtherUserAsync(string email, int userId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    void Remove(User user);
}
