using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetCrewForumPosts;

public record GetCrewForumPostsQuery() : IRequest<ForumListResponse>;

public class GetCrewForumPostsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewForumPostsQuery, ForumListResponse>
{
    public async Task<ForumListResponse> Handle(GetCrewForumPostsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ForumListResponse { Success = false, Message = "You are not in a crew." };
        }

        var posts = await forumRepository.GetByCrewIdAsync(membership.CrewId, cancellationToken);
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        posts = posts
            .Where(post => !AdultContentAccess.IsBlocked(preference, post.IsAdultContent))
            .ToList();

        var resourceIds = posts.Select(p => p.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumPost,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = posts.Select(post =>
        {
            envelopeById.TryGetValue(post.Id.ToString(), out var envelope);
            return ForumMapper.MapListItem(post, envelope);
        }).ToList();

        return new ForumListResponse
        {
            Success = true,
            Message = "Forum posts loaded.",
            Items = items
        };
    }
}
