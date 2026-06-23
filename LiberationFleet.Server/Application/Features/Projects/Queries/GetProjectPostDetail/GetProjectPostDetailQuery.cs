using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Queries.GetProjectPostDetail;

public record GetProjectPostDetailQuery(int PostId) : IRequest<ProjectDetailResponse>;

public class GetProjectPostDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetProjectPostDetailQuery, ProjectDetailResponse>
{
    public async Task<ProjectDetailResponse> Handle(GetProjectPostDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await projectRepository.GetByIdWithAuthorAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ProjectDetailResponse { Success = false, Message = "Project post not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ProjectDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var postEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ProjectForumPost,
            post.Id.ToString(),
            cancellationToken);

        var comments = await projectRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var topLevel = comments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = comments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProjectComment,
            commentIds,
            post.CrewId,
            cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = comments.Count(c => c.ParentCommentId == comment.Id);
            return ProjectMapper.MapComment(comment, envelope, replyCount);
        }).ToList();

        return new ProjectDetailResponse
        {
            Success = true,
            Message = "Project post loaded.",
            Post = ProjectMapper.MapDetail(post, postEnvelope, commentDtos, userId)
        };
    }
}
