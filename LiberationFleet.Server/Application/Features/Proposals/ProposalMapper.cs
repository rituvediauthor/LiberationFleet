using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalMapper
{
    public const string AnonymousAuthor = "Anonymous";

    public static ProposalListItemDto MapListItem(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        ProposalCrewSettingChange? crewSettingChange = null,
        ProposalCrewRuleChange? crewRuleChange = null,
        ProposalCrewChatChange? crewChatChange = null,
        ProposalCrewmateKick? crewmateKick = null,
        ProposalCrewmateRejoin? crewmateRejoin = null,
        ProposalCrewJoinRequest? crewJoinRequest = null,
        ProposalCrewRoleChange? crewRoleChange = null,
        ProposalClaimPlaceholderIdentity? claimPlaceholderIdentity = null,
        ProposalCrewmatePermissionGrant? crewmatePermissionGrant = null,
        string? currentUserVote = null)
    {
        var dto = new ProposalListItemDto
        {
            Id = proposal.Id,
            AuthorUserId = 0,
            AuthorUsername = AnonymousAuthor,
            LastActivityAt = proposal.LastActivityAt,
            Status = proposal.Status.ToString(),
            ApproveCount = proposal.ApproveCount,
            DisapproveCount = proposal.DisapproveCount,
            ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
            CurrentUserVote = currentUserVote
        };

        if (crewmateKick is not null)
        {
            ApplyPlaintext(dto, crewmateKick.Title, crewmateKick.Description);
            return dto;
        }

        if (crewJoinRequest is not null)
        {
            ApplyPlaintext(dto, crewJoinRequest.Title, crewJoinRequest.Description);
            return dto;
        }

        if (crewRoleChange is not null)
        {
            ApplyPlaintext(dto, crewRoleChange.Title, crewRoleChange.Description);
            return dto;
        }

        if (claimPlaceholderIdentity is not null)
        {
            ApplyPlaintext(dto, claimPlaceholderIdentity.Title, claimPlaceholderIdentity.Description);
            return dto;
        }

        if (crewmatePermissionGrant is not null)
        {
            ApplyPlaintext(dto, crewmatePermissionGrant.Title, crewmatePermissionGrant.Description);
            return dto;
        }

        if (crewmateRejoin is not null)
        {
            ApplyPlaintext(dto, crewmateRejoin.Title, crewmateRejoin.Description);
            return dto;
        }

        if (crewChatChange is not null)
        {
            ApplyPlaintext(dto, crewChatChange.Title, crewChatChange.Description);
            return dto;
        }

        if (crewRuleChange is not null)
        {
            ApplyPlaintext(dto, crewRuleChange.Title, crewRuleChange.Description);
            return dto;
        }

        if (crewSettingChange is not null)
        {
            ApplyPlaintext(dto, crewSettingChange.Title, crewSettingChange.Description);
            return dto;
        }

        if (envelope is not null)
        {
            dto.HasEncryptedContent = true;
            dto.EncryptedPayload = CryptoMapper.MapPayload(envelope);
        }

        return dto;
    }

    public static ProposalDetailDto MapDetail(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ProposalCommentDto> comments,
        int viewerUserId,
        ProposalCrewSettingChange? crewSettingChange = null,
        ProposalCrewRuleChange? crewRuleChange = null,
        ProposalCrewChatChange? crewChatChange = null,
        ProposalCrewmateKick? crewmateKick = null,
        ProposalCrewmateRejoin? crewmateRejoin = null,
        ProposalCrewJoinRequest? crewJoinRequest = null,
        ProposalCrewRoleChange? crewRoleChange = null,
        ProposalClaimPlaceholderIdentity? claimPlaceholderIdentity = null,
        ProposalCrewmatePermissionGrant? crewmatePermissionGrant = null,
        string? currentUserVote = null,
        string? viewerAlias = null)
    {
        var listItem = MapListItem(
            proposal,
            envelope,
            crewSettingChange,
            crewRuleChange,
            crewChatChange,
            crewmateKick,
            crewmateRejoin,
            crewJoinRequest,
            crewRoleChange,
            claimPlaceholderIdentity,
            crewmatePermissionGrant,
            currentUserVote);
        var isSystemProposal = proposal.Kind is
            ProposalKind.CrewSettingChange
            or ProposalKind.CrewRuleChange
            or ProposalKind.CrewChatChange
            or ProposalKind.CrewmateKick
            or ProposalKind.CrewmateSeasonKick
            or ProposalKind.CrewmateRejoin
            or ProposalKind.CrewJoinRequest
            or ProposalKind.CrewRoleChange
            or ProposalKind.ClaimPlaceholderIdentity
            or ProposalKind.CrewmatePermissionGrant;
        var plaintextDescription = crewmateKick?.Description
            ?? crewJoinRequest?.Description
            ?? crewRoleChange?.Description
            ?? claimPlaceholderIdentity?.Description
            ?? crewmatePermissionGrant?.Description
            ?? crewmateRejoin?.Description
            ?? crewChatChange?.Description
            ?? crewRuleChange?.Description
            ?? crewSettingChange?.Description;
        var usesAnonymousComments = proposal.Kind == ProposalKind.General;

        var isKickVoteTarget = crewmateKick is not null && crewmateKick.TargetUserId == viewerUserId;

        return new ProposalDetailDto
        {
            Id = listItem.Id,
            AuthorUserId = listItem.AuthorUserId,
            AuthorUsername = listItem.AuthorUsername,
            LastActivityAt = listItem.LastActivityAt,
            Status = listItem.Status,
            ApproveCount = listItem.ApproveCount,
            DisapproveCount = listItem.DisapproveCount,
            ApprovalTimerEndsAt = listItem.ApprovalTimerEndsAt,
            HasEncryptedContent = listItem.HasEncryptedContent,
            HasPlaintextContent = listItem.HasPlaintextContent,
            Title = listItem.Title,
            DescriptionPreview = listItem.DescriptionPreview,
            EncryptedPayload = listItem.EncryptedPayload,
            CurrentUserVote = listItem.CurrentUserVote,
            CreatedAt = proposal.CreatedAt,
            Description = plaintextDescription,
            CanEdit = !isSystemProposal && proposal.AuthorUserId == viewerUserId,
            CanDelete = !isSystemProposal && proposal.AuthorUserId == viewerUserId,
            UsesAnonymousComments = usesAnonymousComments,
            CanKickAuthor = usesAnonymousComments && proposal.AuthorUserId != viewerUserId,
            ViewerAlias = viewerAlias,
            IsKickVoteTarget = isKickVoteTarget,
            CanVote = proposal.Status == ProposalStatus.Pending && !isKickVoteTarget,
            Comments = comments
        };
    }

    public static ProposalCommentDto MapComment(
        ProposalComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount,
        int viewerUserId,
        bool usesAnonymousComments,
        IReadOnlyDictionary<int, string> nicknameByUserId,
        string? replyToUsername = null)
    {
        var isOwn = comment.AuthorUserId == viewerUserId;
        nicknameByUserId.TryGetValue(comment.AuthorUserId, out var nickname);

        return new ProposalCommentDto
        {
            Id = comment.Id,
            AuthorUserId = 0,
            AuthorUsername = usesAnonymousComments && !string.IsNullOrWhiteSpace(nickname)
                ? nickname
                : AnonymousAuthor,
            ParentCommentId = comment.ParentCommentId,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToUsername = replyToUsername,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            IsOwnComment = isOwn,
            CanKick = usesAnonymousComments && !isOwn
        };
    }

    public static ProposalStatus ParseStatus(string status) =>
        Enum.TryParse<ProposalStatus>(status, true, out var parsed) ? parsed : ProposalStatus.Pending;

    /// <summary>
    /// Crew governance and join proposals stay visible even when the viewer and author have blocked each other,
    /// so members can still review and vote on changes that affect the whole crew.
    /// </summary>
    public static bool IsVisibleDespiteBlock(ProposalKind kind) =>
        kind is ProposalKind.CrewSettingChange
            or ProposalKind.CrewRuleChange
            or ProposalKind.CrewChatChange
            or ProposalKind.CrewmateKick
            or ProposalKind.CrewmateSeasonKick
            or ProposalKind.CrewmateRejoin
            or ProposalKind.CrewJoinRequest
            or ProposalKind.CrewRoleChange
            or ProposalKind.ClaimPlaceholderIdentity
            or ProposalKind.CrewmatePermissionGrant;

    private static void ApplyPlaintext(ProposalListItemDto dto, string title, string description)
    {
        dto.HasPlaintextContent = true;
        dto.Title = title;
        dto.DescriptionPreview = description;
    }
}
