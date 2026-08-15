namespace LiberationFleet.Server.Application.Common.Interfaces;

/// <summary>
/// Opaque cold storage for E2EE media ciphertext (server never decrypts).
/// </summary>
public interface IDeepFreezeBlobStore
{
    bool IsEnabled { get; }

    /// <summary>Legacy path: base64 ciphertext stored as UTF-8 text.</summary>
    Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default);

    Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>Raw AES-GCM ciphertext bytes (preferred for large video/audio).</summary>
    Task UploadBytesAsync(string blobPath, byte[] ciphertext, CancellationToken cancellationToken = default);

    Task<byte[]?> DownloadBytesAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open a seekable read stream over a raw binary blob (for Range / progressive playback).
    /// Caller owns the returned stream.
    /// </summary>
    Task<(Stream Stream, long Length)?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
