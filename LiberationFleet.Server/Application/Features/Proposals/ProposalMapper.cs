using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalMapper
{
    private const string AnonymousAuthor = "Anonymous";

    public static ProposalListItemDto MapListItem(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        ProposalCrewSettingChange? crewSettingChange = null,
        ProposalCrewRuleChange? crewRuleChange = null,
        ProposalCrewChatChange? crewChatChange = null,
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
        string? currentUserVote = null)
    {
        var listItem = MapListItem(
            proposal,
            envelope,
            crewSettingChange,
            crewRuleChange,
            crewChatChange,
            currentUserVote);
        var isSystemProposal = proposal.Kind is
            ProposalKind.CrewSettingChange or ProposalKind.CrewRuleChange or ProposalKind.CrewChatChange;
        var plaintextDescription = crewChatChange?.Description
            ?? crewRuleChange?.Description
            ?? crewSettingChange?.Description;

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
            Comments = comments
        };
    }

    public static ProposalCommentDto MapComment(
        ProposalComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount) =>
        new()
        {
            Id = comment.Id,
            AuthorUserId = 0,
            AuthorUsername = AnonymousAuthor,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static ProposalStatus ParseStatus(string status) =>
        Enum.TryParse<ProposalStatus>(status, true, out var parsed) ? parsed : ProposalStatus.Pending;

    private static void ApplyPlaintext(ProposalListItemDto dto, string title, string description)
    {
        dto.HasPlaintextContent = true;
        dto.Title = title;
        dto.DescriptionPreview = description;
    }
}
