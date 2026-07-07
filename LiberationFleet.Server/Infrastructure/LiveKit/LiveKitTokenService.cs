using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Infrastructure.LiveKit;

public class LiveKitTokenService(IOptions<LiveKitOptions> options) : ILiveKitTokenService
{
    private readonly LiveKitOptions _options = options.Value;

    public string CreateRoomToken(string participantIdentity, string participantName, string liveKitRoomName)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("LiveKit is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(_options.TokenTtlMinutes, 15, 720));

        var videoGrant = new Dictionary<string, object?>
        {
            ["roomJoin"] = true,
            ["room"] = liveKitRoomName,
            ["canPublish"] = true,
            ["canSubscribe"] = true
        };

        var claims = new List<Claim>
        {
            new("video", JsonSerializer.Serialize(videoGrant), ClaimValueTypes.String),
            new(JwtRegisteredClaimNames.Name, participantName),
            new(JwtRegisteredClaimNames.Sub, participantIdentity),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.ApiKey,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateAdminToken()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("LiveKit is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(10);

        var videoGrant = new Dictionary<string, object?>
        {
            ["roomAdmin"] = true,
            ["roomCreate"] = true
        };

        var claims = new List<Claim>
        {
            new("video", JsonSerializer.Serialize(videoGrant), ClaimValueTypes.String),
            new(JwtRegisteredClaimNames.Sub, "server-admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.ApiKey,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GetWebSocketUrl() => _options.Host;
}
