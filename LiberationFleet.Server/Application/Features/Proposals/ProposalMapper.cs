using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalMapper
{
    public static ProposalListItemDto MapListItem(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        string? currentUserVote = null)
    {
        var dto = new ProposalListItemDto
        {
            Id = proposal.Id,
            AuthorUserId = proposal.AuthorUserId,
            AuthorUsername = proposal.AuthorUser.Username,
            LastActivityAt = proposal.LastActivityAt,
            Status = proposal.Status.ToString(),
            ApproveCount = proposal.ApproveCount,
            DisapproveCount = proposal.DisapproveCount,
            ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
            CurrentUserVote = currentUserVote
        };

        if (envelope is not null)
        {
            dto.HasEncryptedContent = true;
            dto.EncryptedPayload = CryptoMapper.MapPayload(envelope);
            dto.AuthorUsername = string.Empty;
        }

        return dto;
    }

    public static ProposalDetailDto MapDetail(
        Proposal proposal,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ProposalCommentDto> comments,
        int viewerUserId,
        string? currentUserVote = null) =>
        new()
        {
            Id = proposal.Id,
            AuthorUserId = proposal.AuthorUserId,
            AuthorUsername = envelope is null ? proposal.AuthorUser.Username : string.Empty,
            LastActivityAt = proposal.LastActivityAt,
            CreatedAt = proposal.CreatedAt,
            Status = proposal.Status.ToString(),
            ApproveCount = proposal.ApproveCount,
            DisapproveCount = proposal.DisapproveCount,
            ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            CanEdit = proposal.AuthorUserId == viewerUserId,
            CanDelete = proposal.AuthorUserId == viewerUserId,
            CurrentUserVote = currentUserVote,
            Comments = comments
        };

    public static ProposalCommentDto MapComment(
        ProposalComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount) =>
        new()
        {
            Id = comment.Id,
            AuthorUserId = comment.AuthorUserId,
            AuthorUsername = envelope is null ? comment.AuthorUser.Username : string.Empty,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static ProposalStatus ParseStatus(string status) =>
        Enum.TryParse<ProposalStatus>(status, true, out var parsed) ? parsed : ProposalStatus.Pending;
}
