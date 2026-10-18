using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Infrastructure.Push;

/// <summary>
/// Builds the Google OAuth JWT assertion for FCM HTTP v1.
/// Uses <see cref="JwtPayload"/> so claim names stay <c>iss</c>/<c>sub</c>/<c>scope</c>
/// instead of the long XML SOAP URIs <see cref="SecurityTokenDescriptor"/> emits.
/// </summary>
public static class FcmGoogleAuth
{
    public const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    public const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

    public static (string Assertion, string TokenUri) CreateJwtAssertion(string serviceAccountJson)
    {
        using var doc = JsonDocument.Parse(serviceAccountJson);
        var root = doc.RootElement;
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("FCM service account missing client_email.");
        var privateKeyPem = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("FCM service account missing private_key.");
        privateKeyPem = privateKeyPem.Replace("\\n", "\n", StringComparison.Ordinal);
        var tokenUri = root.TryGetProperty("token_uri", out var tokenUriEl)
            ? tokenUriEl.GetString() ?? DefaultTokenUri
            : DefaultTokenUri;

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var now = DateTimeOffset.UtcNow;
        var header = new JwtHeader(new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256));
        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iss, clientEmail },
            { JwtRegisteredClaimNames.Sub, clientEmail },
            { JwtRegisteredClaimNames.Aud, tokenUri },
            { JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Exp, now.AddMinutes(55).ToUnixTimeSeconds() },
            { "scope", FirebaseMessagingScope }
        };

        var assertion = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        return (assertion, tokenUri);
    }
}
