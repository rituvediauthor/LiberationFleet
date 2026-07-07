using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.LiveKit;

public class LiveKitAdminService(
    IHttpClientFactory httpClientFactory,
    ILiveKitTokenService tokenService,
    IOptions<LiveKitOptions> options) : ILiveKitAdminService
{
    private readonly LiveKitOptions _options = options.Value;

    public async Task RemoveParticipantAsync(string liveKitRoomName, string participantIdentity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            return;
        }

        var httpUrl = ToHttpUrl(_options.Host);
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenService.CreateAdminToken());

        var payload = JsonSerializer.Serialize(new
        {
            room = liveKitRoomName,
            identity = participantIdentity
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{httpUrl}/twirp/livekit.RoomService/RemoveParticipant")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        try
        {
            await client.SendAsync(request, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup when LiveKit is unavailable.
        }
    }

    private static string ToHttpUrl(string host)
    {
        var normalized = host.TrimEnd('/');
        if (normalized.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
        {
            return "http://" + normalized["ws://".Length..];
        }

        if (normalized.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + normalized["wss://".Length..];
        }

        return normalized;
    }
}
