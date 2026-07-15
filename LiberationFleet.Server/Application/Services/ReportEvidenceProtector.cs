using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Services;

public class ReportEvidenceOptions
{
    public const string SectionName = "ReportEvidence";

    /// <summary>Base64-encoded 32-byte AES key. Generate with: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))</summary>
    public string AesKeyBase64 { get; set; } = string.Empty;

    /// <summary>Shared secret for vendor/ops endpoints (header X-Report-Vendor-Key).</summary>
    public string VendorApiKey { get; set; } = string.Empty;

    public int NonCsamRetentionDays { get; set; } = 90;
}

public interface IReportEvidenceProtector
{
    (string Nonce, string Ciphertext) Seal(string plaintextJson);
    string Open(string nonce, string ciphertext);
}

public class ReportEvidenceProtector(IOptions<ReportEvidenceOptions> options) : IReportEvidenceProtector
{
    public (string Nonce, string Ciphertext) Seal(string plaintextJson)
    {
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(plaintextJson);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var packed = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, packed, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, ciphertext.Length, tag.Length);
        return (Convert.ToBase64String(nonce), Convert.ToBase64String(packed));
    }

    public string Open(string nonce, string ciphertext)
    {
        var key = GetKey();
        var nonceBytes = Convert.FromBase64String(nonce);
        var packed = Convert.FromBase64String(ciphertext);
        if (packed.Length < 16)
        {
            throw new CryptographicException("Invalid evidence ciphertext.");
        }

        var body = packed.AsSpan(0, packed.Length - 16);
        var tag = packed.AsSpan(packed.Length - 16);
        var plaintext = new byte[body.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonceBytes, body, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetKey()
    {
        var configured = options.Value.AesKeyBase64;
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Dev fallback — replace in production via ReportEvidence:AesKeyBase64
            configured = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("liberation-fleet-dev-report-evidence-key")));
        }

        var key = Convert.FromBase64String(configured);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("ReportEvidence:AesKeyBase64 must decode to 32 bytes.");
        }

        return key;
    }
}
