namespace LiberationFleet.Server.Application.Common.Interfaces;

/// <summary>
/// Opaque cold storage for E2EE media ciphertext (server never decrypts).
/// </summary>
public interface IDeepFreezeBlobStore
{
    bool IsEnabled { get; }

    Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default);

    Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
