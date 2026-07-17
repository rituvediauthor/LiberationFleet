using System.Net.Http.Json;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Services;

public interface IReportVendorNotifier
{
    Task NotifyNewReportAsync(ContentReport report, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional fire-and-forget metadata ping so a moderation vendor can poll /ops without waiting.
/// Never sends decrypted evidence on the wire here — vendor must fetch via authenticated /ops.
/// </summary>
public class ReportVendorNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<ReportEvidenceOptions> options,
    ILogger<ReportVendorNotifier> logger) : IReportVendorNotifier
{
    public async Task NotifyNewReportAsync(ContentReport report, CancellationToken cancellationToken = default)
    {
        var url = options.Value.VendorNotifyUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            logger.LogWarning("ReportEvidence:VendorNotifyUrl is not a valid absolute URL.");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(ReportVendorNotifier));
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            var apiKey = options.Value.VendorApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Report-Vendor-Key", apiKey);
            }

            request.Content = JsonContent.Create(new
            {
                reportId = report.Id,
                reason = report.Reason.ToString(),
                status = report.Status.ToString(),
                createdAt = report.CreatedAt,
                targetType = report.TargetType.ToString(),
                targetResourceId = report.TargetResourceId,
                targetAuthorUserId = report.TargetAuthorUserId
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Vendor notify returned {StatusCode} for report {ReportId}.",
                    (int)response.StatusCode,
                    report.Id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vendor notify failed for report {ReportId}.", report.Id);
        }
    }
}
