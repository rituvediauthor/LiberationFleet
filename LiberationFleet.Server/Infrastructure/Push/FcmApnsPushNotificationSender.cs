using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiberationFleet.Server.Infrastructure.Push;

public class FcmApnsPushNotificationSender(
    IHttpClientFactory httpClientFactory,
    IDevicePushTokenRepository pushTokens,
    IUnitOfWork unitOfWork,
    IOptions<PushNotificationOptions> options,
    ILogger<FcmApnsPushNotificationSender> logger) : IPushNotificationSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _fcmGate = new(1, 1);
    private string? _fcmAccessToken;
    private DateTimeOffset _fcmAccessTokenExpiresAt;
    private static int _unconfiguredLogged;

    public async Task SendAsync(
        int userId,
        NotificationDto notification,
        int? badgeCount = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.IsFcmConfigured && !opts.IsApnsConfigured)
        {
            if (Interlocked.Exchange(ref _unconfiguredLogged, 1) == 0)
            {
                logger.LogWarning(
                    "Background push is not configured. Set Push__FcmProjectId and Key Vault Push-FcmServiceAccountJson for Android FCM (and APNs settings for iOS). In-app SignalR alerts still work while the app is open.");
            }
            return;
        }

        IReadOnlyList<DevicePushToken> tokens;
        try
        {
            tokens = await pushTokens.GetActiveForUserAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed loading push tokens for user {UserId}", userId);
            return;
        }

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
                var ok = token.Platform switch
                {
                    PushPlatform.Android when opts.IsFcmConfigured
                        => await SendFcmAsync(opts, token, notification, badgeCount, cancellationToken),
                    PushPlatform.Ios when opts.IsApnsConfigured
                        => await SendApnsAsync(opts, token, notification, badgeCount, cancellationToken),
                    _ => true
                };

                if (!ok)
                {
                    await pushTokens.DisableAsync(token.Id, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Push send failed for user {UserId} token {TokenId} platform {Platform}",
                    userId,
                    token.Id,
                    token.Platform);
            }
        }
    }

    private async Task<bool> SendFcmAsync(
        PushNotificationOptions opts,
        DevicePushToken token,
        NotificationDto notification,
        int? badgeCount,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetFcmAccessTokenAsync(opts, cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(FcmApnsPushNotificationSender));
        var url = $"https://fcm.googleapis.com/v1/projects/{opts.FcmProjectId}/messages:send";

        // Explicit camelCase / proto JSON names — do not run this through PropertyNamingPolicy.
        // channelId must match PushNotificationRegistrationService ANDROID_PUSH_CHANNEL_ID.
        // icon must be a white-on-transparent status drawable, not the adaptive launcher.
        var payload = new Dictionary<string, object>
        {
            ["message"] = new Dictionary<string, object>
            {
                ["token"] = token.Token,
                ["notification"] = new Dictionary<string, string>
                {
                    ["title"] = Truncate(notification.Title, 100),
                    ["body"] = Truncate(notification.Body, 200)
                },
                ["data"] = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["actionUrl"] = notification.ActionUrl ?? string.Empty,
                    ["kind"] = notification.Kind.ToString()
                },
                ["android"] = new Dictionary<string, object>
                {
                    ["priority"] = "HIGH",
                    ["notification"] = new Dictionary<string, object>
                    {
                        ["channelId"] = "liberation_fleet_default",
                        ["icon"] = "ic_stat_notify",
                        ["color"] = "#0B1220",
                        ["sound"] = "default",
                        ["notificationPriority"] = "PRIORITY_HIGH"
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (IsInvalidTokenResponse(body) || response.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            logger.LogInformation("Disabling invalid FCM token {TokenId}", token.Id);
            return false;
        }

        logger.LogWarning("FCM send failed ({Status}): {Body}", (int)response.StatusCode, Truncate(body, 400));
        return true;
    }

    private async Task<bool> SendApnsAsync(
        PushNotificationOptions opts,
        DevicePushToken token,
        NotificationDto notification,
        int? badgeCount,
        CancellationToken cancellationToken)
    {
        var jwt = CreateApnsJwt(opts);
        var host = opts.ApnsUseSandbox
            ? "https://api.sandbox.push.apple.com"
            : "https://api.push.apple.com";
        var url = $"{host}/3/device/{token.Token}";

        var aps = new Dictionary<string, object?>
        {
            ["alert"] = new Dictionary<string, string>
            {
                ["title"] = Truncate(notification.Title, 100),
                ["body"] = Truncate(notification.Body, 200)
            },
            ["sound"] = "default"
        };
        if (badgeCount is > 0)
        {
            aps["badge"] = badgeCount.Value;
        }

        var payload = new Dictionary<string, object?>
        {
            ["aps"] = aps,
            ["notificationId"] = notification.Id,
            ["actionUrl"] = notification.ActionUrl ?? string.Empty,
            ["kind"] = notification.Kind.ToString()
        };

        var client = httpClientFactory.CreateClient(nameof(FcmApnsPushNotificationSender));
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Version = new Version(2, 0);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", jwt);
        request.Headers.TryAddWithoutValidation("apns-topic", opts.ApnsBundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Gone
            || body.Contains("BadDeviceToken", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Unregistered", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Disabling invalid APNs token {TokenId}", token.Id);
            return false;
        }

        logger.LogWarning("APNs send failed ({Status}): {Body}", (int)response.StatusCode, Truncate(body, 400));
        return true;
    }

    private async Task<string> GetFcmAccessTokenAsync(
        PushNotificationOptions opts,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_fcmAccessToken)
            && _fcmAccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _fcmAccessToken;
        }

        await _fcmGate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_fcmAccessToken)
                && _fcmAccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _fcmAccessToken;
            }

            var (assertion, tokenUri) = FcmGoogleAuth.CreateJwtAssertion(opts.FcmServiceAccountJson);

            var client = httpClientFactory.CreateClient(nameof(FcmApnsPushNotificationSender));
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            });
            using var response = await client.PostAsync(tokenUri, form, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var tokenDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            _fcmAccessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("FCM token response missing access_token.");
            var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var expEl)
                ? expEl.GetInt32()
                : 3600;
            _fcmAccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return _fcmAccessToken;
        }
        finally
        {
            _fcmGate.Release();
        }
    }

    private static string CreateApnsJwt(PushNotificationOptions opts)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(opts.ApnsKeyP8);

        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(50).UtcDateTime,
            Issuer = opts.ApnsTeamId,
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(ecdsa) { KeyId = opts.ApnsKeyId },
                SecurityAlgorithms.EcdsaSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static bool IsInvalidTokenResponse(string body) =>
        body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
        || body.Contains("NotRegistered", StringComparison.OrdinalIgnoreCase)
        // FCM uses INVALID_ARGUMENT for many payload errors; only treat as a bad
        // device token when the message clearly refers to the registration token.
        || (body.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase)
            && (body.Contains("registration token", StringComparison.OrdinalIgnoreCase)
                || body.Contains("registration_token", StringComparison.OrdinalIgnoreCase)
                || body.Contains("not a valid FCM", StringComparison.OrdinalIgnoreCase)));

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
