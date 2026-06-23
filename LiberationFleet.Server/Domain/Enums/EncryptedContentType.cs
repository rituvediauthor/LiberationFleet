namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// Identifies the kind of encrypted payload stored in <see cref="Entities.EncryptedContentEnvelope"/>.
/// Used for gift log today; reserved for future features.
/// </summary>
public enum EncryptedContentType
{
    GiftLogEntry = 0,
    DirectMessage = 1,
    ChatRoomMessage = 2,
    ForumPost = 3,
    ProjectForumPost = 4,
    Proposal = 5,
    RulesDocument = 6,
    LibraryItem = 7,
    ImageAsset = 8,
    AudioAsset = 9,
    VideoAsset = 10,
    ProposalComment = 11,
    ForumComment = 12,
    ProjectComment = 13
}
