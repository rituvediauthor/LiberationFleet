using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetForumPosts;

public record GetFleetForumPostsQuery() : IRequest<ForumListResponse>;

public class GetFleetForumPostsQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetFleetForumPostsQuery, ForumListResponse>
{
    public async Task<ForumListResponse> Handle(GetFleetForumPostsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new ForumListResponse { Success = false, Message = "You are not in a fleet." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, fleet.Id, cancellationToken))
        {
            return new ForumListResponse { Success = false, Message = "You are not in this fleet." };
        }

        var posts = await forumRepository.GetByFleetIdAsync(fleet.Id, cancellationToken);
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        posts = posts
            .Where(post => !AdultContentAccess.IsBlocked(preference, post.IsAdultContent)
                && !hiddenUserIds.Contains(post.AuthorUserId))
            .ToList();

        var resourceIds = posts.Select(p => p.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumPost,
            resourceIds,
            fleetId: fleet.Id,
            cancellationToken: cancellationToken);
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
