using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ContentReport
{
    public int Id { get; set; }
    public int ReporterUserId { get; set; }
    public ContentReportReason Reason { get; set; }
    public ContentReportTargetType TargetType { get; set; }
    public int? TargetResourceId { get; set; }
    public int? TargetParentId { get; set; }
    public int? TargetAuthorUserId { get; set; }
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public string? ReporterNote { get; set; }
    public string EvidenceNonce { get; set; } = string.Empty;
    public string EvidenceCiphertext { get; set; } = string.Empty;
    public ContentReportStatus Status { get; set; } = ContentReportStatus.Received;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EscalatedToNcmecAt { get; set; }
    public DateTime? EscalatedToVendorAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? VendorLabel { get; set; }
    public string? OpsNotes { get; set; }
    public bool TargetQuarantined { get; set; }
    public bool TargetAuthorFrozen { get; set; }

    public User Reporter { get; set; } = null!;
    public User? TargetAuthor { get; set; }
    public ICollection<ContentReportAccessLog> AccessLogs { get; set; } = new List<ContentReportAccessLog>();
}

public class ContentReportAccessLog
{
    public long Id { get; set; }
    public int ContentReportId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

    public ContentReport ContentReport { get; set; } = null!;
}
