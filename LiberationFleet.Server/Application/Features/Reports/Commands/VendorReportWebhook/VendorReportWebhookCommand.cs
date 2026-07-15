using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Features.Reports.Commands.VendorReportWebhook;

public record VendorReportWebhookCommand(
    string ApiKey,
    int ReportId,
    string Label,
    string? Notes) : IRequest<VendorReportWebhookResponse>;

public class VendorReportWebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ContentReportStatus? Status { get; set; }
}

public class VendorReportWebhookCommandHandler(
    IContentReportRepository reportRepository,
    IUserRepository userRepository,
    IOptions<ReportEvidenceOptions> options,
    IUnitOfWork unitOfWork) : IRequestHandler<VendorReportWebhookCommand, VendorReportWebhookResponse>
{
    public async Task<VendorReportWebhookResponse> Handle(
        VendorReportWebhookCommand request,
        CancellationToken cancellationToken)
    {
        if (!IsValidApiKey(request.ApiKey))
        {
            return Fail("Unauthorized.");
        }

        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report is null)
        {
            return Fail("Report not found.");
        }

        var label = (request.Label ?? string.Empty).Trim().ToLowerInvariant();
        report.VendorLabel = label;
        report.OpsNotes = string.IsNullOrWhiteSpace(request.Notes)
            ? report.OpsNotes
            : request.Notes.Trim()[..Math.Min(request.Notes.Trim().Length, 2000)];
        report.EscalatedToVendorAt ??= DateTime.UtcNow;

        if (label is "csam" or "child_sexual_exploitation" or "csea")
        {
            report.Status = ContentReportStatus.QueuedForNcmec;
            report.EscalatedToNcmecAt ??= DateTime.UtcNow;

            if (report.TargetResourceId.HasValue && report.TargetType != ContentReportTargetType.UserProfile)
            {
                await reportRepository.SoftDeleteTargetAsync(
                    report.TargetType,
                    report.TargetResourceId.Value,
                    report.TargetParentId,
                    cancellationToken);
                report.TargetQuarantined = true;
            }

            if (report.TargetAuthorUserId.HasValue)
            {
                var author = await userRepository.GetByIdWithProfileAsync(report.TargetAuthorUserId.Value, cancellationToken);
                if (author is not null && author.IsActive)
                {
                    author.IsActive = false;
                    await userRepository.UpdateAsync(author, cancellationToken);
                    report.TargetAuthorFrozen = true;
                }
            }
        }
        else if (label is "ncii" or "violence" or "other")
        {
            report.Status = ContentReportStatus.Actioned;
            if (report.TargetResourceId.HasValue && report.TargetType != ContentReportTargetType.UserProfile)
            {
                await reportRepository.SoftDeleteTargetAsync(
                    report.TargetType,
                    report.TargetResourceId.Value,
                    report.TargetParentId,
                    cancellationToken);
                report.TargetQuarantined = true;
            }
        }
        else if (label is "none" or "benign" or "closed")
        {
            report.Status = ContentReportStatus.Closed;
            report.ClosedAt = DateTime.UtcNow;
        }
        else
        {
            report.Status = ContentReportStatus.EscalatedToVendor;
        }

        await reportRepository.AddAccessLogAsync(new ContentReportAccessLog
        {
            ContentReportId = report.Id,
            Actor = "vendor",
            Action = $"webhook:{label}",
            AccessedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new VendorReportWebhookResponse
        {
            Success = true,
            Message = "Vendor triage applied.",
            Status = report.Status
        };
    }

    private bool IsValidApiKey(string? apiKey)
    {
        var expected = options.Value.VendorApiKey;
        return !string.IsNullOrWhiteSpace(expected)
            && !string.IsNullOrWhiteSpace(apiKey)
            && string.Equals(expected, apiKey, StringComparison.Ordinal);
    }

    private static VendorReportWebhookResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
