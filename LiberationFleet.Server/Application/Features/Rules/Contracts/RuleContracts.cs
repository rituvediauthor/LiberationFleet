using LiberationFleet.Server.Application.Features.Crypto.Contracts;

namespace LiberationFleet.Server.Application.Features.Rules.Contracts;

public class RuleListItemDto
{
    public int Id { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class RuleDetailDto : RuleListItemDto
{
}

public class RuleListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<RuleListItemDto> Items { get; set; } = Array.Empty<RuleListItemDto>();
}

public class RuleDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public RuleDetailDto? Rule { get; set; }
}

public class RuleOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RuleId { get; set; }
    public bool ProposalsSubmitted { get; set; }
    public int? ProposalId { get; set; }
}

public class CreateRuleRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public string PlaintextTitle { get; set; } = string.Empty;
    public string PlaintextDescription { get; set; } = string.Empty;
}

public class UpdateRuleRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public string PlaintextTitle { get; set; } = string.Empty;
    public string PlaintextDescription { get; set; } = string.Empty;
    public string PlaintextOldTitle { get; set; } = string.Empty;
    public string PlaintextOldDescription { get; set; } = string.Empty;
}

public class DeleteRuleRequest
{
    public string PlaintextTitle { get; set; } = string.Empty;
    public string PlaintextDescription { get; set; } = string.Empty;
}
