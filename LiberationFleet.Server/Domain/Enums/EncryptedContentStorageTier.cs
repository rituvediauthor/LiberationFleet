namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// Where the envelope ciphertext bytes live. DeepFreeze moves media off SQL into cold blob storage.
/// </summary>
public enum EncryptedContentStorageTier
{
    Hot = 0,
    DeepFreeze = 1
}
