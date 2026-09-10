namespace LiberationFleet.Server.Application.Features.Profile.Contracts;

/// <summary>Opaque client-encrypted location blob (user content key). Server cannot decrypt.</summary>
public class EncryptedLocationDto
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}
