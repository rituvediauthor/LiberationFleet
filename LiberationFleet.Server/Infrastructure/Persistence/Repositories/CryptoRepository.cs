using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CryptoRepository : ICryptoRepository
{
    private readonly ApplicationDbContext _context;

    public CryptoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<UserKeyBundle?> GetUserKeyBundleAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.UserKeyBundles.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserKeyBundle>> GetUserKeyBundlesAsync(
        IReadOnlyList<int> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<UserKeyBundle>();
        }

        return await _context.UserKeyBundles
            .Where(b => userIds.Contains(b.UserId))
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertUserKeyBundleAsync(UserKeyBundle bundle, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserKeyBundles.FirstOrDefaultAsync(b => b.UserId == bundle.UserId, cancellationToken);
        if (existing is null)
        {
            await _context.UserKeyBundles.AddAsync(bundle, cancellationToken);
            return;
        }

        existing.IdentityPublicKey = bundle.IdentityPublicKey;
        existing.KeyVersion = bundle.KeyVersion;
        existing.UpdatedAt = bundle.UpdatedAt;
    }

    public Task<UserPrivateKeyBackup?> GetPrivateKeyBackupAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.UserPrivateKeyBackups.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

    public async Task UpsertPrivateKeyBackupAsync(UserPrivateKeyBackup backup, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserPrivateKeyBackups.FirstOrDefaultAsync(b => b.UserId == backup.UserId, cancellationToken);
        if (existing is null)
        {
            await _context.UserPrivateKeyBackups.AddAsync(backup, cancellationToken);
            return;
        }

        existing.Salt = backup.Salt;
        existing.Iv = backup.Iv;
        existing.Ciphertext = backup.Ciphertext;
        existing.KeyVersion = backup.KeyVersion;
        existing.UpdatedAt = backup.UpdatedAt;
    }

    public Task<CrewKeyDistribution?> GetCrewKeyDistributionAsync(
        int crewId,
        int userId,
        int keyVersion,
        CancellationToken cancellationToken = default) =>
        _context.CrewKeyDistributions.FirstOrDefaultAsync(
            d => d.CrewId == crewId && d.UserId == userId && d.KeyVersion == keyVersion,
            cancellationToken);

    public async Task<int?> GetLatestCrewKeyVersionAsync(int crewId, CancellationToken cancellationToken = default)
    {
        return await _context.CrewKeyDistributions
            .Where(d => d.CrewId == crewId)
            .Select(d => (int?)d.KeyVersion)
            .MaxAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CrewKeyDistribution>> GetCrewKeyDistributionsAsync(
        int crewId,
        int keyVersion,
        CancellationToken cancellationToken = default) =>
        await _context.CrewKeyDistributions
            .Where(d => d.CrewId == crewId && d.KeyVersion == keyVersion)
            .ToListAsync(cancellationToken);

    public async Task UpsertCrewKeyDistributionAsync(CrewKeyDistribution distribution, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CrewKeyDistributions.FirstOrDefaultAsync(
            d => d.CrewId == distribution.CrewId
                && d.UserId == distribution.UserId
                && d.KeyVersion == distribution.KeyVersion,
            cancellationToken);

        if (existing is null)
        {
            await _context.CrewKeyDistributions.AddAsync(distribution, cancellationToken);
            return;
        }

        existing.WrappedCrewKey = distribution.WrappedCrewKey;
        existing.WrapNonce = distribution.WrapNonce;
        existing.WrappedByUserId = distribution.WrappedByUserId;
    }

    public Task<FleetKeyDistribution?> GetFleetKeyDistributionAsync(
        int fleetId,
        int userId,
        int keyVersion,
        CancellationToken cancellationToken = default) =>
        _context.FleetKeyDistributions.FirstOrDefaultAsync(
            d => d.FleetId == fleetId && d.UserId == userId && d.KeyVersion == keyVersion,
            cancellationToken);

    public async Task<int?> GetLatestFleetKeyVersionAsync(int fleetId, CancellationToken cancellationToken = default)
    {
        return await _context.FleetKeyDistributions
            .Where(d => d.FleetId == fleetId)
            .Select(d => (int?)d.KeyVersion)
            .MaxAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FleetKeyDistribution>> GetFleetKeyDistributionsAsync(
        int fleetId,
        int keyVersion,
        CancellationToken cancellationToken = default) =>
        await _context.FleetKeyDistributions
            .Where(d => d.FleetId == fleetId && d.KeyVersion == keyVersion)
            .ToListAsync(cancellationToken);

    public async Task UpsertFleetKeyDistributionAsync(FleetKeyDistribution distribution, CancellationToken cancellationToken = default)
    {
        var existing = await _context.FleetKeyDistributions.FirstOrDefaultAsync(
            d => d.FleetId == distribution.FleetId
                && d.UserId == distribution.UserId
                && d.KeyVersion == distribution.KeyVersion,
            cancellationToken);

        if (existing is null)
        {
            await _context.FleetKeyDistributions.AddAsync(distribution, cancellationToken);
            return;
        }

        existing.WrappedFleetKey = distribution.WrappedFleetKey;
        existing.WrapNonce = distribution.WrapNonce;
        existing.WrappedByUserId = distribution.WrappedByUserId;
    }

    public Task<EncryptedContentEnvelope?> GetEnvelopeAsync(
        EncryptedContentType contentType,
        string resourceId,
        CancellationToken cancellationToken = default) =>
        _context.EncryptedContentEnvelopes.FirstOrDefaultAsync(
            e => e.ContentType == contentType && e.ResourceId == resourceId,
            cancellationToken);

    public async Task<IReadOnlyList<EncryptedContentEnvelope>> GetEnvelopesAsync(
        EncryptedContentType contentType,
        IReadOnlyList<string> resourceIds,
        int? crewId = null,
        int? fleetId = null,
        CancellationToken cancellationToken = default)
    {
        if (resourceIds.Count == 0)
        {
            return Array.Empty<EncryptedContentEnvelope>();
        }

        var query = _context.EncryptedContentEnvelopes
            .Where(e => e.ContentType == contentType && resourceIds.Contains(e.ResourceId));

        if (crewId.HasValue)
        {
            query = query.Where(e => e.CrewId == crewId);
        }

        if (fleetId.HasValue)
        {
            query = query.Where(e => e.FleetId == fleetId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task UpsertEnvelopeAsync(EncryptedContentEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var existing = await _context.EncryptedContentEnvelopes.FirstOrDefaultAsync(
            e => e.ContentType == envelope.ContentType && e.ResourceId == envelope.ResourceId,
            cancellationToken);

        if (existing is null)
        {
            await _context.EncryptedContentEnvelopes.AddAsync(envelope, cancellationToken);
            return;
        }

        existing.CrewId = envelope.CrewId;
        existing.FleetId = envelope.FleetId;
        existing.AuthorUserId = envelope.AuthorUserId;
        existing.KeyVersion = envelope.KeyVersion;
        existing.Nonce = envelope.Nonce;
        existing.Ciphertext = envelope.Ciphertext;
        existing.UpdatedAt = envelope.UpdatedAt;
    }

    public async Task DeleteEnvelopesAsync(
        EncryptedContentType contentType,
        IReadOnlyList<string> resourceIds,
        CancellationToken cancellationToken = default)
    {
        if (resourceIds.Count == 0)
        {
            return;
        }

        await _context.EncryptedContentEnvelopes
            .Where(e => e.ContentType == contentType && resourceIds.Contains(e.ResourceId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
