using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewChatChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public CrewChatProposalAction Action { get; set; }
    public int? RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public ChatRoomType RoomType { get; set; }
    public string? NameNonce { get; set; }
    public string? NameCiphertext { get; set; }

    /// <summary>
    /// Plaintext room name used by fleet chat proposals (fleet rooms are not E2E encrypted).
    /// Crew chat proposals keep the name in the encrypted NameNonce/NameCiphertext pair.
    /// </summary>
    public string? PlaintextName { get; set; }
    public int KeyVersion { get; set; } = 1;
    public bool IsAdultContent { get; set; }
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
