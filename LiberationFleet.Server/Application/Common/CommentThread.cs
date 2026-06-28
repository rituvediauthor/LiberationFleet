namespace LiberationFleet.Server.Application.Common;

public static class CommentThread
{
    public static int GetThreadRootId(int commentId, int? parentCommentId) =>
        parentCommentId ?? commentId;

    public static (int ThreadRootId, int? ReplyToCommentId) ResolveNewReply(int parentCommentId, int? parentParentCommentId) =>
        parentParentCommentId is null
            ? (parentCommentId, null)
            : (parentParentCommentId.Value, parentCommentId);
}
