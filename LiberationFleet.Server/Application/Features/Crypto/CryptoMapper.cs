using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crypto;

public static class CryptoMapper
{
    public static UserKeyBundleDto MapKeyBundle(UserKeyBundle bundle) => new()
    {
        UserId = bundle.UserId,
        IdentityPublicKey = bundle.IdentityPublicKey,
        KeyVersion = bundle.KeyVersion,
        UpdatedAt = bundle.UpdatedAt
    };

    public static UserPrivateKeyBackupDto MapPrivateKeyBackup(UserPrivateKeyBackup backup) => new()
    {
        Salt = backup.Salt,
        Iv = backup.Iv,
        Ciphertext = backup.Ciphertext,
        KeyVersion = backup.KeyVersion
    };

    public static CrewKeyDistributionDto MapCrewKeyDistribution(CrewKeyDistribution distribution) => new()
    {
        CrewId = distribution.CrewId,
        UserId = distribution.UserId,
        KeyVersion = distribution.KeyVersion,
        WrappedCrewKey = distribution.WrappedCrewKey,
        WrapNonce = distribution.WrapNonce,
        WrappedByUserId = distribution.WrappedByUserId
    };

    public static EncryptedContentEnvelopeDto MapEnvelope(EncryptedContentEnvelope envelope) => new()
    {
        ContentType = MapContentType(envelope.ContentType),
        ResourceId = envelope.ResourceId,
        CrewId = envelope.CrewId,
        AuthorUserId = envelope.AuthorUserId,
        KeyVersion = envelope.KeyVersion,
        Nonce = envelope.Nonce,
        Ciphertext = envelope.Ciphertext,
        UpdatedAt = envelope.UpdatedAt
    };

    public static EncryptedPayloadDto MapPayload(EncryptedContentEnvelope envelope) => new()
    {
        KeyVersion = envelope.KeyVersion,
        Nonce = envelope.Nonce,
        Ciphertext = envelope.Ciphertext
    };

    public static EncryptedContentType ToDomain(EncryptedContentTypeDto type) => type switch
    {
        EncryptedContentTypeDto.GiftLogEntry => EncryptedContentType.GiftLogEntry,
        EncryptedContentTypeDto.DirectMessage => EncryptedContentType.DirectMessage,
        EncryptedContentTypeDto.ChatRoomMessage => EncryptedContentType.ChatRoomMessage,
        EncryptedContentTypeDto.ForumPost => EncryptedContentType.ForumPost,
        EncryptedContentTypeDto.ProjectForumPost => EncryptedContentType.ProjectForumPost,
        EncryptedContentTypeDto.Proposal => EncryptedContentType.Proposal,
        EncryptedContentTypeDto.RulesDocument => EncryptedContentType.RulesDocument,
        EncryptedContentTypeDto.LibraryItem => EncryptedContentType.LibraryItem,
        EncryptedContentTypeDto.ImageAsset => EncryptedContentType.ImageAsset,
        EncryptedContentTypeDto.AudioAsset => EncryptedContentType.AudioAsset,
        EncryptedContentTypeDto.VideoAsset => EncryptedContentType.VideoAsset,
        EncryptedContentTypeDto.ProposalComment => EncryptedContentType.ProposalComment,
        EncryptedContentTypeDto.ForumComment => EncryptedContentType.ForumComment,
        EncryptedContentTypeDto.ProjectComment => EncryptedContentType.ProjectComment,
        EncryptedContentTypeDto.ChatRoomName => EncryptedContentType.ChatRoomName,
        _ => EncryptedContentType.GiftLogEntry
    };

    public static EncryptedContentTypeDto MapContentType(EncryptedContentType type) => type switch
    {
        EncryptedContentType.GiftLogEntry => EncryptedContentTypeDto.GiftLogEntry,
        EncryptedContentType.DirectMessage => EncryptedContentTypeDto.DirectMessage,
        EncryptedContentType.ChatRoomMessage => EncryptedContentTypeDto.ChatRoomMessage,
        EncryptedContentType.ForumPost => EncryptedContentTypeDto.ForumPost,
        EncryptedContentType.ProjectForumPost => EncryptedContentTypeDto.ProjectForumPost,
        EncryptedContentType.Proposal => EncryptedContentTypeDto.Proposal,
        EncryptedContentType.RulesDocument => EncryptedContentTypeDto.RulesDocument,
        EncryptedContentType.LibraryItem => EncryptedContentTypeDto.LibraryItem,
        EncryptedContentType.ImageAsset => EncryptedContentTypeDto.ImageAsset,
        EncryptedContentType.AudioAsset => EncryptedContentTypeDto.AudioAsset,
        EncryptedContentType.VideoAsset => EncryptedContentTypeDto.VideoAsset,
        EncryptedContentType.ProposalComment => EncryptedContentTypeDto.ProposalComment,
        EncryptedContentType.ForumComment => EncryptedContentTypeDto.ForumComment,
        EncryptedContentType.ProjectComment => EncryptedContentTypeDto.ProjectComment,
        EncryptedContentType.ChatRoomName => EncryptedContentTypeDto.ChatRoomName,
        _ => EncryptedContentTypeDto.GiftLogEntry
    };
}
