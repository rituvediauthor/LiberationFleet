namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Shared who / what / where wording for user-facing notifications.
/// </summary>
public static class NotificationCopy
{
    public static string ActorOrFallback(string? displayName, string fallback = "Someone") =>
        string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();

    public static string ChatRoomLabel(string? roomName) =>
        string.IsNullOrWhiteSpace(roomName) ? "a chat" : roomName.Trim();

    public static string PostedInChat(string actorName, string? roomName) =>
        $"{ActorOrFallback(actorName)} posted in {ChatRoomLabel(roomName)}";

    public static string LikedYourMessage(string actorName) =>
        $"{ActorOrFallback(actorName)} liked your message";

    public static string LikedYourPost(string actorName, bool fleet = false) =>
        $"{ActorOrFallback(actorName)} liked your {(fleet ? "fleet " : "")}post";

    public static string LikedYourComment(string actorName, bool fleet = false) =>
        $"{ActorOrFallback(actorName)} liked your {(fleet ? "fleet " : "")}comment";

    public static string LikedYourGift(string actorName) =>
        $"{ActorOrFallback(actorName)} liked your gift log entry";

    public static string LikedYourGiftComment(string actorName) =>
        $"{ActorOrFallback(actorName)} liked your gift log comment";

    public static string RepliedOnForum(string actorName, bool toComment) =>
        toComment
            ? $"{ActorOrFallback(actorName)} replied to your forum comment"
            : $"{ActorOrFallback(actorName)} commented on your forum post";

    public static string RepliedOnFleetForum(string actorName, bool toComment) =>
        toComment
            ? $"{ActorOrFallback(actorName)} replied to your fleet forum comment"
            : $"{ActorOrFallback(actorName)} commented on your fleet forum post";

    public static string RepliedOnGift(string actorName, bool toComment) =>
        toComment
            ? $"{ActorOrFallback(actorName)} replied to your gift log comment"
            : $"{ActorOrFallback(actorName)} commented on a gift log entry";

    public static string RepliedOnProposal(string actorName, bool toComment, bool fleet = false) =>
        toComment
            ? $"{ActorOrFallback(actorName)} replied to your {(fleet ? "fleet " : "")}proposal comment"
            : $"{ActorOrFallback(actorName)} commented on your {(fleet ? "fleet " : "")}proposal";

    public static string SentDirectMessage(string actorName) =>
        $"{ActorOrFallback(actorName)} sent you a message";

    public static string ReceivedGift(string actorName, bool fleet = false) =>
        $"{ActorOrFallback(actorName)} recorded a {(fleet ? "fleet " : "")}gift for you";

    public static string SentFriendRequest(string actorName) =>
        $"{ActorOrFallback(actorName)} sent you a friend request";

    public static string AcceptedFriendRequest(string actorName) =>
        $"{ActorOrFallback(actorName)} accepted your friend request";

    public static string InvitedToCrew(string actorName, string? crewName) =>
        string.IsNullOrWhiteSpace(crewName)
            ? $"{ActorOrFallback(actorName)} invited you to a crew"
            : $"{ActorOrFallback(actorName)} invited you to {crewName.Trim()}";

    public static string SubmittedProposal(string actorName, bool fleet = false) =>
        $"{ActorOrFallback(actorName)} submitted a {(fleet ? "fleet " : "crew ")}proposal";

    public static string PreviewOr(string? preview, string fallback) =>
        NotificationPreview.BodyOrFallback(preview, fallback);

    /// <summary>
    /// Prefer client-sent plaintext preview (required for encrypted content), then any
    /// server-stored plaintext fields, then the fallback sentence.
    /// </summary>
    public static string PreviewOr(string? clientPreview, string? storedPlaintext, string fallback) =>
        PreviewOr(
            !string.IsNullOrWhiteSpace(clientPreview) ? clientPreview : storedPlaintext,
            fallback);
}
