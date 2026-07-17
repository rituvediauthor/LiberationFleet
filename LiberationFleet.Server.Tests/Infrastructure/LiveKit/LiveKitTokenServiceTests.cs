using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LiberationFleet.Server.Infrastructure.LiveKit;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Tests.Infrastructure.LiveKit;

public class LiveKitTokenServiceTests
{
    [Fact]
    public void CreateRoomToken_EmbedsNestedVideoGrantObject()
    {
        var options = Options.Create(new LiveKitOptions
        {
            Host = "ws://localhost:7880",
            ApiKey = "devkey",
            ApiSecret = "secretsecretsecretsecretsecretsecret12",
            TokenTtlMinutes = 60
        });

        var service = new LiveKitTokenService(options);
        var jwt = service.CreateRoomToken("42", "pilot", "voice-crew-1-room-9");

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);

        token.Issuer.Should().Be("devkey");
        token.Subject.Should().Be("42");

        token.Payload.TryGetValue("video", out var video).Should().BeTrue();
        video.Should().NotBeOfType<string>("video grant must be a nested JSON object, not a string");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(video));
        document.RootElement.GetProperty("roomJoin").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("room").GetString().Should().Be("voice-crew-1-room-9");
        document.RootElement.GetProperty("canPublish").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("canSubscribe").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void CreateRoomToken_CanBeValidatedWithApiSecret()
    {
        var secret = "secretsecretsecretsecretsecretsecret12";
        var options = Options.Create(new LiveKitOptions
        {
            Host = "ws://localhost:7880",
            ApiKey = "devkey",
            ApiSecret = secret,
            TokenTtlMinutes = 60
        });

        var jwt = new LiveKitTokenService(options).CreateRoomToken("7", "crewmate", "voice-crew-2-room-3");

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "devkey",
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, parameters, out _);
        principal.FindFirst("sub")?.Value.Should().Be("7");
    }
}
