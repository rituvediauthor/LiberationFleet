using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IContentMentionRepository
{
    Task<IReadOnlyList<int>> GetMentionedUserIdsAsync(
        MentionedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<ContentMention> mentions, CancellationToken cancellationToken = default);

    Task DeleteByContentAsync(
        MentionedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default);
}
