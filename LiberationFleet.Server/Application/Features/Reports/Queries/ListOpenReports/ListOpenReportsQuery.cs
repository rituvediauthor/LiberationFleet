using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Features.Reports.Queries.ListOpenReports;

public record ListOpenReportsQuery(string ApiKey, int Limit = 50, bool IncludeEvidence = false)
    : IRequest<ListOpenReportsResponse>;

public class ListOpenReportsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ContentReportOpsDto> Items { get; set; } = Array.Empty<ContentReportOpsDto>();
}

public class ContentReportOpsDto
{
    public int Id { get; set; }
    public int ReporterUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? TargetResourceId { get; set; }
    public int? TargetAuthorUserId { get; set; }
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public string? ReporterNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EscalatedToNcmecAt { get; set; }
    public bool TargetQuarantined { get; set; }
    public bool TargetAuthorFrozen { get; set; }
    public string? VendorLabel { get; set; }
    public string? EvidenceJson { get; set; }
}

public class ListOpenReportsQueryHandler(
    IContentReportRepository reportRepository,
    IReportEvidenceProtector evidenceProtector,
    IOptions<ReportEvidenceOptions> options,
    IUnitOfWork unitOfWork) : IRequestHandler<ListOpenReportsQuery, ListOpenReportsResponse>
{
    public async Task<ListOpenReportsResponse> Handle(ListOpenReportsQuery request, CancellationToken cancellationToken)
    {
        var expected = options.Value.VendorApiKey;
        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrWhiteSpace(request.ApiKey)
            || !string.Equals(expected, request.ApiKey, StringComparison.Ordinal))
        {
            return new ListOpenReportsResponse { Success = false, Message = "Unauthorized." };
        }

        var reports = await reportRepository.ListOpenAsync(request.Limit, cancellationToken);
        var items = new List<ContentReportOpsDto>();
        foreach (var report in reports)
        {
            string? evidence = null;
            if (request.IncludeEvidence)
            {
                try
                {
                    evidence = evidenceProtector.Open(report.EvidenceNonce, report.EvidenceCiphertext);
                    await reportRepository.AddAccessLogAsync(new ContentReportAccessLog
                    {
                        ContentReportId = report.Id,
                        Actor = "ops",
                        Action = "view_evidence",
                        AccessedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                catch
                {
                    evidence = "[unable to decrypt evidence]";
                }
            }

            items.Add(new ContentReportOpsDto
            {
                Id = report.Id,
                ReporterUserId = report.ReporterUserId,
                Reason = report.Reason.ToString(),
                TargetType = report.TargetType.ToString(),
                TargetResourceId = report.TargetResourceId,
                TargetAuthorUserId = report.TargetAuthorUserId,
                CrewId = report.CrewId,
                FleetId = report.FleetId,
                ReporterNote = report.ReporterNote,
                Status = report.Status.ToString(),
                CreatedAt = report.CreatedAt,
                EscalatedToNcmecAt = report.EscalatedToNcmecAt,
                TargetQuarantined = report.TargetQuarantined,
                TargetAuthorFrozen = report.TargetAuthorFrozen,
                VendorLabel = report.VendorLabel,
                EvidenceJson = evidence
            });
        }

        if (request.IncludeEvidence)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ListOpenReportsResponse
        {
            Success = true,
            Message = "Open reports loaded.",
            Items = items
        };
    }
}
