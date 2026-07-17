using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Services;

namespace LiberationFleet.Server.Infrastructure.Storage;

/// <summary>No-op store used when deep freeze is disabled or misconfigured.</summary>
public sealed class NullDeepFreezeBlobStore : IDeepFreezeBlobStore
{
    public bool IsEnabled => false;

    public Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class DeepFreezeBlobStoreRouter(
    LocalDeepFreezeBlobStore local,
    AzureDeepFreezeBlobStore azure,
    NullDeepFreezeBlobStore disabled) : IDeepFreezeBlobStore
{
    private IDeepFreezeBlobStore Active =>
        azure.IsEnabled ? azure : local.IsEnabled ? local : disabled;

    public bool IsEnabled => Active.IsEnabled;

    public Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default) =>
        Active.UploadAsync(blobPath, ciphertext, cancellationToken);

    public Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Active.DownloadAsync(blobPath, cancellationToken);

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Active.DeleteAsync(blobPath, cancellationToken);
}
