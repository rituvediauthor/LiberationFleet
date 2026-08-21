using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftEngagementMapper
{
    public static GiftCommentDto MapComment(
        GiftComment comment,
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

    public static GiftDetailDto MapDetail(GiftLogEntryDto entry, IReadOnlyList<GiftCommentDto> comments) =>
        new()
        {
            Id = entry.Id,
            Type = entry.Type,
            GiverId = entry.GiverId,
            GiverName = entry.GiverName,
            RecipientId = entry.RecipientId,
            RecipientName = entry.RecipientName,
            MiddlemanId = entry.MiddlemanId,
            MiddlemanName = entry.MiddlemanName,
            Amount = entry.Amount,
            Platform = entry.Platform,
            Timestamp = entry.Timestamp,
            Message = entry.Message,
            RelatedUserIds = entry.RelatedUserIds,
            Status = entry.Status,
            VerificationStatus = entry.VerificationStatus,
            DisplayFlag = entry.DisplayFlag,
            CustomGiftCategory = entry.CustomGiftCategory,
            AvailableActions = entry.AvailableActions,
            CompletionPlatformOptions = entry.CompletionPlatformOptions,
            HasEncryptedContent = entry.HasEncryptedContent,
            EncryptedPayload = entry.EncryptedPayload,
            LikeCount = entry.LikeCount,
            LikedByCurrentUser = entry.LikedByCurrentUser,
            CommentCount = entry.CommentCount,
            IsSeasonLocked = entry.IsSeasonLocked,
            Comments = comments
        };
}
