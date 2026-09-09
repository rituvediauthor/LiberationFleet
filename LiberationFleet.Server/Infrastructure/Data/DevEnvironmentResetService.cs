using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Non-production full reset: wipe persisted data/blob storage, then recreate schema + seeds.
/// </summary>
public sealed class DevEnvironmentResetService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseReadyState _readyState;
    private readonly IDeepFreezeBlobStore _deepFreezeBlobStore;
    private readonly ILogger<DevEnvironmentResetService> _logger;

    public DevEnvironmentResetService(
        IServiceScopeFactory scopeFactory,
        DatabaseReadyState readyState,
        IDeepFreezeBlobStore deepFreezeBlobStore,
        ILogger<DevEnvironmentResetService> logger)
    {
        _scopeFactory = scopeFactory;
        _readyState = readyState;
        _deepFreezeBlobStore = deepFreezeBlobStore;
        _logger = logger;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _readyState.MarkNotReady();

        await _deepFreezeBlobStore.ClearAllAsync(cancellationToken);

        await using (var deleteScope = _scopeFactory.CreateAsyncScope())
        {
            var deleteContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await deleteContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await using var recreateScope = _scopeFactory.CreateAsyncScope();
        var recreateContext = recreateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseLifecycle.ApplyMigrationsAndRepairsAsync(
            recreateContext,
            _readyState,
            _logger,
            cancellationToken);
    }
}
