using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Infrastructure.Push;

/// <summary>
/// APNs provider JWT (ES256). Built with <see cref="JwtHeader"/>/<see cref="JwtPayload"/>
/// so <c>iss</c>/<c>iat</c>/<c>kid</c> stay short names Apple accepts.
/// </summary>
public static class ApnsAuth
{
    public static string CreateJwt(string teamId, string keyId, string keyP8)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(NormalizePem(keyP8));

        var credentials = new SigningCredentials(
            new ECDsaSecurityKey(ecdsa) { KeyId = keyId },
            SecurityAlgorithms.EcdsaSha256);

        var header = new JwtHeader(credentials);
        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iss, teamId },
            { JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    public static string NormalizePem(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            return pem;
        }

        return pem.Trim()
            .Trim('"')
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
