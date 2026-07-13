using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class CrewInvitation
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int InviterUserId { get; set; }
    public int InviteeUserId { get; set; }
    public CrewInvitationStatus Status { get; set; } = CrewInvitationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public Crew Crew { get; set; } = null!;
    public User InviterUser { get; set; } = null!;
    public User InviteeUser { get; set; } = null!;
}
