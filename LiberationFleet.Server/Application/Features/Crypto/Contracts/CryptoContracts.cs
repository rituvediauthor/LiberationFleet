namespace LiberationFleet.Server.Application.Features.Crypto.Contracts;

public class UserKeyBundleDto
{
    public int UserId { get; set; }
    public string IdentityPublicKey { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserPrivateKeyBackupDto
{
    public string Salt { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
}

public class CrewKeyDistributionDto
{
    public int CrewId { get; set; }
    public int UserId { get; set; }
    public int KeyVersion { get; set; }
    public string WrappedCrewKey { get; set; } = string.Empty;
    public string WrapNonce { get; set; } = string.Empty;
    public int WrappedByUserId { get; set; }
}

public class EncryptedPayloadDto
{
    public int KeyVersion { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
}

public class EncryptedContentEnvelopeDto
{
    public EncryptedContentTypeDto ContentType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public int? CrewId { get; set; }
    public int AuthorUserId { get; set; }
    public int KeyVersion { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public enum EncryptedContentTypeDto
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
    ProjectComment = 13,
    ChatRoomName = 14
}

public class CryptoOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpsertPublicKeyRequest
{
    public string IdentityPublicKey { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpsertPrivateKeyBackupRequest
{
    public string Salt { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpsertCrewKeyDistributionRequest
{
    public int UserId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string WrappedCrewKey { get; set; } = string.Empty;
    public string WrapNonce { get; set; } = string.Empty;
}

public class UpsertEncryptedContentRequest
{
    public EncryptedContentTypeDto ContentType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public int? CrewId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
}
