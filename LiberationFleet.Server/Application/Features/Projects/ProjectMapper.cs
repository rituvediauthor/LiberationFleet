using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Projects;

public static class ProjectMapper
{
    public static ProjectListItemDto MapListItem(ProjectPost post, EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorUsername = envelope is null ? post.AuthorUser.Username : string.Empty,
            LastActivityAt = post.LastActivityAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static ProjectDetailDto MapDetail(
        ProjectPost post,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ProjectCommentDto> comments,
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

    public static ProjectCommentDto MapComment(
        ProjectComment comment,
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
