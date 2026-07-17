namespace LiberationFleet.Server.Application.Services;

public class MediaDeepFreezeOptions
{
    public const string SectionName = "MediaDeepFreeze";

    /// <summary>When false, the background job no-ops and reads stay hot-only.</summary>
    public bool Enabled { get; set; }

    /// <summary>Age after which Image/Video/Audio assets are moved off SQL.</summary>
    public int AgeDays { get; set; } = 60;

    /// <summary>Max envelopes to freeze per job run.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Skip tiny payloads; default ~4KB ciphertext chars.</summary>
    public int MinimumCiphertextChars { get; set; } = 4096;

    /// <summary>local | azure</summary>
    public string Provider { get; set; } = "local";

    /// <summary>Local filesystem root when Provider=local.</summary>
    public string LocalRootPath { get; set; } = "App_Data/deep-freeze";

    /// <summary>Azure Storage connection string when Provider=azure.</summary>
    public string? AzureConnectionString { get; set; }

    /// <summary>Blob container name (Cool access tier recommended).</summary>
    public string AzureContainerName { get; set; } = "media-deep-freeze";
}
