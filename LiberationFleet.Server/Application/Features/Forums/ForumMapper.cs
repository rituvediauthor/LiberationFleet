using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Forums;

public static class ForumMapper
{
    public static ForumListItemDto MapListItem(
        ForumPost post,
        EncryptedContentEnvelope? envelope,
        int likeCount = 0,
        bool likedByCurrentUser = false,
        int commentCount = 0,
        IReadOnlySet<int>? crewAvatarAllowedUserIds = null) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorUsername = envelope is null ? post.AuthorUser.Username : string.Empty,
            AuthorAvatarResourceId = CrewAvatarVisibilityService.Filter(
                post.AuthorUser?.AvatarResourceId,
                post.AuthorUserId,
                crewAvatarAllowedUserIds),
            LastActivityAt = post.LastActivityAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Title = post.Title,
            Body = post.Body,
            IsAdultContent = post.IsAdultContent,
            LikeCount = likeCount,
            LikedByCurrentUser = likedByCurrentUser,
            CommentCount = commentCount
        };

    public static ForumDetailDto MapDetail(
        ForumPost post,
        EncryptedContentEnvelope? envelope,
        IReadOnlyList<ForumCommentDto> comments,
        int viewerUserId,
        int likeCount = 0,
        bool likedByCurrentUser = false,
        int commentCount = 0,
        IReadOnlySet<int>? crewAvatarAllowedUserIds = null) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorUsername = envelope is null ? post.AuthorUser.Username : string.Empty,
            AuthorAvatarResourceId = CrewAvatarVisibilityService.Filter(
                post.AuthorUser?.AvatarResourceId,
                post.AuthorUserId,
                crewAvatarAllowedUserIds),
            LastActivityAt = post.LastActivityAt,
            CreatedAt = post.CreatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Title = post.Title,
            Body = post.Body,
            CanEdit = post.AuthorUserId == viewerUserId,
            CanDelete = post.AuthorUserId == viewerUserId,
            IsAdultContent = post.IsAdultContent,
            LikeCount = likeCount,
            LikedByCurrentUser = likedByCurrentUser,
            CommentCount = commentCount,
            Comments = comments
        };

    public static ForumCommentDto MapComment(
        ForumComment comment,
        EncryptedContentEnvelope? envelope,
        int replyCount,
        string? replyToUsername = null,
        int likeCount = 0,
        bool likedByCurrentUser = false,
        IReadOnlySet<int>? crewAvatarAllowedUserIds = null) =>
        new()
        {
            Id = comment.Id,
            AuthorUserId = comment.AuthorUserId,
            AuthorUsername = envelope is null ? comment.AuthorUser.Username : string.Empty,
            AuthorAvatarResourceId = CrewAvatarVisibilityService.Filter(
                comment.AuthorUser?.AvatarResourceId,
                comment.AuthorUserId,
                crewAvatarAllowedUserIds),
            ParentCommentId = comment.ParentCommentId,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToUsername = replyToUsername,
            CreatedAt = comment.CreatedAt,
            ReplyCount = replyCount,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null,
            Body = comment.Body,
            LikeCount = likeCount,
            LikedByCurrentUser = likedByCurrentUser
        };
}
