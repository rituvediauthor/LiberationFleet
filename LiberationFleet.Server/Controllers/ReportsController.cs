using LiberationFleet.Server.Application.Features.Reports.Commands.CreateContentReport;
using LiberationFleet.Server.Application.Features.Reports.Commands.VendorReportWebhook;
using LiberationFleet.Server.Application.Features.Reports.Queries.ListOpenReports;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(IMediator mediator) : ControllerBase
{
    public class CreateReportBody
    {
        public string Reason { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public int? TargetResourceId { get; set; }
        public int? TargetParentId { get; set; }
        public int? TargetAuthorUserId { get; set; }
        public int? CrewId { get; set; }
        public int? FleetId { get; set; }
        public string? ReporterNote { get; set; }
        public string EvidencePlaintextJson { get; set; } = string.Empty;
        public bool AlsoBlockAuthor { get; set; }
    }

    public class VendorWebhookBody
    {
        public int ReportId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportBody body)
    {
        if (!Enum.TryParse<ContentReportReason>(body.Reason, ignoreCase: true, out var reason)
            || !Enum.TryParse<ContentReportTargetType>(body.TargetType, ignoreCase: true, out var targetType))
        {
            return BadRequest(new CreateContentReportResponse
            {
                Success = false,
                Message = "Invalid reason or target type."
            });
        }

        var result = await mediator.Send(new CreateContentReportCommand(
            reason,
            targetType,
            body.TargetResourceId,
            body.TargetParentId,
            body.TargetAuthorUserId,
            body.CrewId,
            body.FleetId,
            body.ReporterNote,
            body.EvidencePlaintextJson,
            body.AlsoBlockAuthor));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Vendor/ops triage webhook. Authenticate with header X-Report-Vendor-Key.</summary>
    [AllowAnonymous]
    [HttpPost("vendor/webhook")]
    public async Task<IActionResult> VendorWebhook([FromBody] VendorWebhookBody body)
    {
        var apiKey = Request.Headers["X-Report-Vendor-Key"].FirstOrDefault() ?? string.Empty;
        var result = await mediator.Send(new VendorReportWebhookCommand(
            apiKey,
            body.ReportId,
            body.Label,
            body.Notes));
        if (!result.Success && result.Message == "Unauthorized.")
        {
            return Unauthorized(result);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>List open reports for ops/vendor. Authenticate with header X-Report-Vendor-Key.</summary>
    [AllowAnonymous]
    [HttpGet("ops")]
    public async Task<IActionResult> ListOps([FromQuery] int limit = 50, [FromQuery] bool includeEvidence = false)
    {
        var apiKey = Request.Headers["X-Report-Vendor-Key"].FirstOrDefault() ?? string.Empty;
        var result = await mediator.Send(new ListOpenReportsQuery(apiKey, limit, includeEvidence));
        if (!result.Success && result.Message == "Unauthorized.")
        {
            return Unauthorized(result);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
