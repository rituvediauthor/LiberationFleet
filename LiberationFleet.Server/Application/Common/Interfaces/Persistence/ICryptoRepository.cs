using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICryptoRepository
{
    Task<UserKeyBundle?> GetUserKeyBundleAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserKeyBundle>> GetUserKeyBundlesAsync(IReadOnlyList<int> userIds, CancellationToken cancellationToken = default);
    Task UpsertUserKeyBundleAsync(UserKeyBundle bundle, CancellationToken cancellationToken = default);

    Task<UserPrivateKeyBackup?> GetPrivateKeyBackupAsync(int userId, CancellationToken cancellationToken = default);
    Task UpsertPrivateKeyBackupAsync(UserPrivateKeyBackup backup, CancellationToken cancellationToken = default);

    Task<CrewKeyDistribution?> GetCrewKeyDistributionAsync(int crewId, int userId, int keyVersion, CancellationToken cancellationToken = default);
    Task<int?> GetLatestCrewKeyVersionAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewKeyDistribution>> GetCrewKeyDistributionsAsync(int crewId, int keyVersion, CancellationToken cancellationToken = default);
    Task UpsertCrewKeyDistributionAsync(CrewKeyDistribution distribution, CancellationToken cancellationToken = default);

    Task<EncryptedContentEnvelope?> GetEnvelopeAsync(
        EncryptedContentType contentType,
        string resourceId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EncryptedContentEnvelope>> GetEnvelopesAsync(
        EncryptedContentType contentType,
        IReadOnlyList<string> resourceIds,
        int? crewId = null,
        CancellationToken cancellationToken = default);
    Task UpsertEnvelopeAsync(EncryptedContentEnvelope envelope, CancellationToken cancellationToken = default);
}
