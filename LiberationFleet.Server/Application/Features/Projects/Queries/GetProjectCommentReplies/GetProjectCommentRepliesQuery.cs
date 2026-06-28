using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Queries.GetProjectCommentReplies;

public record GetProjectCommentRepliesQuery(int PostId, int ParentCommentId) : IRequest<ProjectCommentRepliesResponse>;

public class GetProjectCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetProjectCommentRepliesQuery, ProjectCommentRepliesResponse>
{
    public async Task<ProjectCommentRepliesResponse> Handle(GetProjectCommentRepliesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectCommentRepliesResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await projectRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ProjectCommentRepliesResponse { Success = false, Message = "Project post not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ProjectCommentRepliesResponse { Success = false, Message = "You are not in this crew." };
        }

        var parent = await projectRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.ProjectPostId != post.Id)
        {
            return new ProjectCommentRepliesResponse { Success = false, Message = "Parent comment not found." };
        }

        var threadRootId = CommentThread.GetThreadRootId(parent.Id, parent.ParentCommentId);
        var comments = await projectRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var commentById = comments.ToDictionary(c => c.Id);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var replies = comments
            .Where(c => c.ParentCommentId == threadRootId && !hiddenUserIds.Contains(c.AuthorUserId))
            .OrderBy(c => c.CreatedAt)
            .ToList();
        var replyIds = replies.Select(c => c.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProjectComment,
            replyIds,
            post.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            var replyToUsername = ResolveReplyToUsername(reply, commentById, envelopeById);
            return ProjectMapper.MapComment(reply, envelope, 0, replyToUsername);
        }).ToList();

        return new ProjectCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }

    private static string? ResolveReplyToUsername(
        ProjectComment reply,
        IReadOnlyDictionary<int, ProjectComment> commentById,
        IReadOnlyDictionary<string, EncryptedContentEnvelope> envelopeById)
    {
        if (!reply.ReplyToCommentId.HasValue
            || !commentById.TryGetValue(reply.ReplyToCommentId.Value, out var target))
        {
            return null;
        }

        if (envelopeById.ContainsKey(target.Id.ToString()))
        {
            return null;
        }

        return target.AuthorUser.Username;
    }
}
