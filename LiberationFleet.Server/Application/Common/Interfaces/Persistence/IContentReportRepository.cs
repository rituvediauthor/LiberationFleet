using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IContentReportRepository
{
    Task AddAsync(ContentReport report, CancellationToken cancellationToken = default);
    Task AddAccessLogAsync(ContentReportAccessLog log, CancellationToken cancellationToken = default);
    Task<ContentReport?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentReport>> ListOpenAsync(int limit, CancellationToken cancellationToken = default);
    Task SoftDeleteTargetAsync(
        ContentReportTargetType targetType,
        int resourceId,
        int? parentId,
        CancellationToken cancellationToken = default);
}
