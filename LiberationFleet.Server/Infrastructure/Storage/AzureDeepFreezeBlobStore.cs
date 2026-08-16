using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Storage;

/// <summary>
/// Azure Blob store (Hot access tier) for media ciphertext offloaded from SQL.
/// </summary>
public sealed class AzureDeepFreezeBlobStore : IDeepFreezeBlobStore
{
    private readonly BlobContainerClient? _container;
    private readonly bool _enabled;

    public AzureDeepFreezeBlobStore(IOptions<MediaDeepFreezeOptions> options)
    {
        var opts = options.Value;
        _enabled = opts.Enabled
            && string.Equals(opts.Provider, "azure", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(opts.AzureConnectionString);

        if (_enabled)
        {
            var service = new BlobServiceClient(opts.AzureConnectionString);
            _container = service.GetBlobContainerClient(opts.AzureContainerName);
        }
    }

    public bool IsEnabled => _enabled && _container is not null;

    public async Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(blobPath);
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ciphertext));
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
                AccessTier = AccessTier.Hot
            },
            cancellationToken);
    }

    public async Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        var blob = container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }

    public async Task UploadBytesAsync(string blobPath, byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(blobPath);
        await using var stream = new MemoryStream(ciphertext, writable: false);
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
                AccessTier = AccessTier.Hot
            },
            cancellationToken);
    }

    public async Task<byte[]?> DownloadBytesAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        var blob = container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToArray();
    }

    public async Task<(Stream Stream, long Length)?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        var blob = container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        // Seekable buffered read so ASP.NET Range (206) can jump to moov/mdat like local files.
        var stream = await blob.OpenReadAsync(
            new BlobOpenReadOptions(allowModifications: false)
            {
                BufferSize = 1 * 1024 * 1024
            },
            cancellationToken);
        return (stream, props.Value.ContentLength);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = EnsureContainer();
        await container.DeleteBlobIfExistsAsync(blobPath, cancellationToken: cancellationToken);
    }

    private BlobContainerClient EnsureContainer() =>
        _container ?? throw new InvalidOperationException("Azure deep-freeze storage is not configured.");
}
