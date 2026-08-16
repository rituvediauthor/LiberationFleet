using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Storage;

/// <summary>
/// Dev/local cold store under a configurable filesystem root.
/// </summary>
public sealed class LocalDeepFreezeBlobStore(IOptions<MediaDeepFreezeOptions> options) : IDeepFreezeBlobStore
{
    public bool IsEnabled =>
        options.Value.Enabled
        && string.Equals(options.Value.Provider, "local", StringComparison.OrdinalIgnoreCase);

    public async Task UploadAsync(string blobPath, string ciphertext, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        EnsureDirectory(fullPath);
        await File.WriteAllTextAsync(fullPath, ciphertext, cancellationToken);
    }

    public async Task<string?> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public async Task UploadBytesAsync(string blobPath, byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        EnsureDirectory(fullPath);
        await File.WriteAllBytesAsync(fullPath, ciphertext, cancellationToken);
    }

    public async Task<byte[]?> DownloadBytesAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task<(Stream Stream, long Length)?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(blobPath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<(Stream Stream, long Length)?>(null);
        }

        // RandomAccess: plain-media Range (206) responses seek to moov/mdat offsets.
        // SequentialScan made reverse seeks unreliable for Safari progressive play.
        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.RandomAccess);
        return Task.FromResult<(Stream Stream, long Length)?>((stream, stream.Length));
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static void EnsureDirectory(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private string ResolvePath(string blobPath)
    {
        var root = options.Value.LocalRootPath;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, root);
        }

        var normalized = blobPath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid deep-freeze blob path.");
        }

        return Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }
}
