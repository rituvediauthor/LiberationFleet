using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Forums;

public static class ForumMapper
{
    public static ForumListItemDto MapListItem(ForumPost post, EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorUsername = envelope is null ? post.AuthorUser.Username : string.Empty,
            LastActivityAt = post.LastActivityAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static ForumDetailDto MapDetail(
        ForumPost post,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ForumCommentDto> comments,
        int viewerUserId) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorUsername = envelope is null ? post.AuthorUser.Username : string.Empty,
            LastActivityAt = post.LastActivityAt,
            CreatedAt = post.CreatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            CanEdit = post.AuthorUserId == viewerUserId,
            CanDelete = post.AuthorUserId == viewerUserId,
            Comments = comments
        };

    public static ForumCommentDto MapComment(
        ForumComment comment,
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
}
