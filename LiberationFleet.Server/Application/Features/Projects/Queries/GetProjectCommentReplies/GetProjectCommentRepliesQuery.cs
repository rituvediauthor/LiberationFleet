using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
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

        var comments = await projectRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var replies = comments
            .Where(c => c.ParentCommentId == parent.Id && !hiddenUserIds.Contains(c.AuthorUserId))
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
            return ProjectMapper.MapComment(reply, envelope, 0);
        }).ToList();

        return new ProjectCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }
}
