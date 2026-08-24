namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Tracks whether EF migrations (and schema repairs) have finished.
/// The host listens for health probes before migrate completes; API traffic
/// must wait so clients do not hit half-migrated schema (e.g. missing LoT flag).
/// </summary>
public sealed class DatabaseReadyState
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;
}
