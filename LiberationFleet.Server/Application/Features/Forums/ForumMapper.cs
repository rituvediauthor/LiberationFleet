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
            AuthorAvatarResourceId = post.AuthorUser?.AvatarResourceId,
            LastActivityAt = post.LastActivityAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Title = post.Title,
            Body = post.Body,
            IsAdultContent = post.IsAdultContent
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
            AuthorAvatarResourceId = post.AuthorUser?.AvatarResourceId,
            LastActivityAt = post.LastActivityAt,
            CreatedAt = post.CreatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Title = post.Title,
            Body = post.Body,
            CanEdit = post.AuthorUserId == viewerUserId,
            CanDelete = post.AuthorUserId == viewerUserId,
            IsAdultContent = post.IsAdultContent,
            Comments = comments
        };

    public static ForumCommentDto MapComment(
        ForumComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount,
        string? replyToUsername = null) =>
        new()
        {
            Id = comment.Id,
            AuthorUserId = comment.AuthorUserId,
            AuthorUsername = envelope is null ? comment.AuthorUser.Username : string.Empty,
            AuthorAvatarResourceId = comment.AuthorUser?.AvatarResourceId,
            ParentCommentId = comment.ParentCommentId,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToUsername = replyToUsername,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Body = comment.Body
        };
}
