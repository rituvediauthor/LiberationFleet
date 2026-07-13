namespace LiberationFleet.Server.Application.Features.Crews.Contracts;

public class InviteCandidateDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsFriend { get; set; }
}

public class InviteCandidateListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<InviteCandidateDto> Items { get; set; } = Array.Empty<InviteCandidateDto>();
}

public class CrewInvitationDto
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int InviterUserId { get; set; }
    public string InviterUsername { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CrewInvitationListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewInvitationDto> Items { get; set; } = Array.Empty<CrewInvitationDto>();
}

public class CrewInvitationDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CrewInvitationDto? Invitation { get; set; }
}

public class CrewInvitationOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int InvitationId { get; set; }
    public int? CrewId { get; set; }
}

public class InviteCrewmateRequest
{
    public int UserId { get; set; }
}
