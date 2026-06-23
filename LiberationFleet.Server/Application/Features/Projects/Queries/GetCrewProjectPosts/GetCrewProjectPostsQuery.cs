using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Queries.GetCrewProjectPosts;

public record GetCrewProjectPostsQuery() : IRequest<ProjectListResponse>;

public class GetCrewProjectPostsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewProjectPostsQuery, ProjectListResponse>
{
    public async Task<ProjectListResponse> Handle(GetCrewProjectPostsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProjectListResponse { Success = false, Message = "You are not in a crew." };
        }

        var posts = await projectRepository.GetByCrewIdAsync(membership.CrewId, cancellationToken);
        var resourceIds = posts.Select(p => p.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProjectForumPost,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = posts.Select(post =>
        {
            envelopeById.TryGetValue(post.Id.ToString(), out var envelope);
            return ProjectMapper.MapListItem(post, envelope);
        }).ToList();

        return new ProjectListResponse
        {
            Success = true,
            Message = "Project posts loaded.",
            Items = items
        };
    }
}
