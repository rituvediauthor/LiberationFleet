using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IEmailMfaChallengeRepository
{
    Task<EmailMfaChallenge?> GetActiveByTokenAsync(string challengeToken, CancellationToken cancellationToken = default);
    Task InvalidateOpenAsync(int userId, EmailMfaPurpose purpose, CancellationToken cancellationToken = default);
    Task AddAsync(EmailMfaChallenge challenge, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmailMfaChallenge challenge, CancellationToken cancellationToken = default);
}
