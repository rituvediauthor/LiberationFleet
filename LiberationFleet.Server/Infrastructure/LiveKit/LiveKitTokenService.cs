using System.IdentityModel.Tokens.Jwt;
using System.Text;
using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Infrastructure.LiveKit;

public class LiveKitTokenService(IOptions<LiveKitOptions> options) : ILiveKitTokenService
{
    private readonly LiveKitOptions _options = options.Value;

    public string CreateRoomToken(string participantIdentity, string participantName, string liveKitRoomName)
    {
        EnsureConfigured();

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(_options.TokenTtlMinutes, 15, 720));

        // LiveKit requires nested JSON object for "video" grants (not a stringified claim).
        var videoGrant = new Dictionary<string, object>
        {
            ["roomJoin"] = true,
            ["room"] = liveKitRoomName,
            ["canPublish"] = true,
            ["canSubscribe"] = true,
            ["canPublishData"] = true
        };

        var header = new JwtHeader(new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret)),
            SecurityAlgorithms.HmacSha256));

        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iss, _options.ApiKey },
            { JwtRegisteredClaimNames.Sub, participantIdentity },
            { JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N") },
            { "name", participantName },
            { "video", videoGrant }
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    public string CreateAdminToken()
    {
        EnsureConfigured();

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(10);

        var videoGrant = new Dictionary<string, object>
        {
            ["roomAdmin"] = true,
            ["roomCreate"] = true,
            ["roomList"] = true
        };

        var header = new JwtHeader(new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret)),
            SecurityAlgorithms.HmacSha256));

        var payload = new JwtPayload
        {
            { JwtRegisteredClaimNames.Iss, _options.ApiKey },
            { JwtRegisteredClaimNames.Sub, "server-admin" },
            { JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds() },
            { JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N") },
            { "video", videoGrant }
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    public string GetWebSocketUrl() => _options.Host;

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("LiveKit is not configured.");
        }
    }
}
