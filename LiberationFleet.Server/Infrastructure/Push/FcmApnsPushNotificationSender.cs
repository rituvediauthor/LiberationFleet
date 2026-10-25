using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

namespace LiberationFleet.Server.Infrastructure.Push;

public class FcmApnsPushNotificationSender(
    IHttpClientFactory httpClientFactory,
    IDevicePushTokenRepository pushTokens,
    IUnitOfWork unitOfWork,
    IOptions<PushNotificationOptions> options,
    ILogger<FcmApnsPushNotificationSender> logger) : IPushNotificationSender
{
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
            logger.LogInformation("No active device push tokens for user {UserId}", userId);
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
                var outcome = token.Platform switch
                {
                    PushPlatform.Android => await SendFcmAsync(opts, token, notification, badgeCount, cancellationToken),
                    PushPlatform.Ios => await SendApnsAsync(opts, token, notification, badgeCount, cancellationToken),
                    _ => PushSendOutcome.Skipped
                };

                if (outcome == PushSendOutcome.InvalidToken)
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

    private async Task<PushSendOutcome> SendFcmAsync(
        PushNotificationOptions opts,
        DevicePushToken token,
        NotificationDto notification,
        int? badgeCount,
        CancellationToken cancellationToken)
    {
        if (!opts.IsFcmConfigured)
        {
            logger.LogWarning("Skipping Android token {TokenId}: FCM is not configured.", token.Id);
            return PushSendOutcome.Skipped;
        }

        var accessToken = await GetFcmAccessTokenAsync(opts, cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(FcmApnsPushNotificationSender));
        var url = $"https://fcm.googleapis.com/v1/projects/{opts.FcmProjectId}/messages:send";
        var payload = PushPayloads.FcmMessage(token.Token, notification, badgeCount);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return PushSendOutcome.Sent;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (IsInvalidTokenResponse(body) || response.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation("Disabling invalid FCM token {TokenId}", token.Id);
            return PushSendOutcome.InvalidToken;
        }

        logger.LogWarning("FCM send failed ({Status}): {Body}", (int)response.StatusCode, PushPayloads.Truncate(body, 400));
        return PushSendOutcome.Failed;
    }

    private async Task<PushSendOutcome> SendApnsAsync(
        PushNotificationOptions opts,
        DevicePushToken token,
        NotificationDto notification,
        int? badgeCount,
        CancellationToken cancellationToken)
    {
        if (!opts.IsApnsConfigured)
        {
            logger.LogWarning("Skipping iOS token {TokenId}: APNs is not configured.", token.Id);
            return PushSendOutcome.Skipped;
        }

        var jwt = ApnsAuth.CreateJwt(opts.ApnsTeamId, opts.ApnsKeyId, opts.ApnsKeyP8);
        var payloadJson = JsonSerializer.Serialize(PushPayloads.ApnsMessage(notification, badgeCount));
        var primarySandbox = opts.ApnsUseSandbox;
        var first = await SendApnsToHostAsync(
            jwt,
            opts.ApnsBundleId,
            token.Token,
            payloadJson,
            primarySandbox,
            cancellationToken);

        if (first == PushSendOutcome.WrongEnvironment)
        {
            var retry = await SendApnsToHostAsync(
                jwt,
                opts.ApnsBundleId,
                token.Token,
                payloadJson,
                !primarySandbox,
                cancellationToken);
            if (retry == PushSendOutcome.Sent)
            {
                logger.LogInformation(
                    "APNs token {TokenId} accepted on the {Environment} host after {First} failed with BadDeviceToken.",
                    token.Id,
                    !primarySandbox ? "sandbox" : "production",
                    primarySandbox ? "sandbox" : "production");
            }

            return retry == PushSendOutcome.WrongEnvironment ? PushSendOutcome.InvalidToken : retry;
        }

        return first;
    }

    private async Task<PushSendOutcome> SendApnsToHostAsync(
        string jwt,
        string bundleId,
        string deviceToken,
        string payloadJson,
        bool sandbox,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(FcmApnsPushNotificationSender));
        using var request = CreateApnsRequest(jwt, bundleId, deviceToken, payloadJson, sandbox);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return PushSendOutcome.Sent;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Contains("BadDeviceToken", StringComparison.OrdinalIgnoreCase))
        {
            return PushSendOutcome.WrongEnvironment;
        }

        if (response.StatusCode is HttpStatusCode.Gone
            || body.Contains("Unregistered", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Disabling invalid APNs token on {Host}", sandbox ? "sandbox" : "production");
            return PushSendOutcome.InvalidToken;
        }

        logger.LogWarning(
            "APNs send failed ({Status}) on {Host}: {Body}",
            (int)response.StatusCode,
            sandbox ? "sandbox" : "production",
            PushPayloads.Truncate(body, 400));
        return PushSendOutcome.Failed;
    }

    internal static HttpRequestMessage CreateApnsRequest(
        string jwt,
        string bundleId,
        string deviceToken,
        string payloadJson,
        bool sandbox)
    {
        var host = sandbox
            ? "https://api.sandbox.push.apple.com"
            : "https://api.push.apple.com";
        var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/3/device/{deviceToken}");
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", jwt);
        request.Headers.TryAddWithoutValidation("apns-topic", bundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        return request;
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

    private static bool IsInvalidTokenResponse(string body) =>
        body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
        || body.Contains("NotRegistered", StringComparison.OrdinalIgnoreCase)
        || (body.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase)
            && (body.Contains("registration token", StringComparison.OrdinalIgnoreCase)
                || body.Contains("registration_token", StringComparison.OrdinalIgnoreCase)
                || body.Contains("not a valid FCM", StringComparison.OrdinalIgnoreCase)));

    private enum PushSendOutcome
    {
        Sent,
        Failed,
        InvalidToken,
        WrongEnvironment,
        Skipped
    }
}
