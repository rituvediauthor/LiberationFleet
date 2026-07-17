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
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

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

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(blobPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
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
