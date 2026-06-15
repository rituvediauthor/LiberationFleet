using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetActiveByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetToken resetToken, CancellationToken cancellationToken = default);
    Task UpdateAsync(PasswordResetToken resetToken, CancellationToken cancellationToken = default);
}
