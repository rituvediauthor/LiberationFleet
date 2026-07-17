using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ContentReportRepository(ApplicationDbContext context) : IContentReportRepository
{
    public async Task AddAsync(ContentReport report, CancellationToken cancellationToken = default) =>
        await context.ContentReports.AddAsync(report, cancellationToken);

    public async Task AddAccessLogAsync(ContentReportAccessLog log, CancellationToken cancellationToken = default) =>
        await context.ContentReportAccessLogs.AddAsync(log, cancellationToken);

    public Task<ContentReport?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.ContentReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> ListOpenAsync(int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await context.ContentReports
            .AsNoTracking()
            .Where(r => r.Status == ContentReportStatus.Received
                || r.Status == ContentReportStatus.QueuedForNcmec
                || r.Status == ContentReportStatus.EscalatedToVendor)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SoftDeleteTargetAsync(
        ContentReportTargetType targetType,
        int resourceId,
        int? parentId,
        CancellationToken cancellationToken = default)
    {
        switch (targetType)
        {
            case ContentReportTargetType.ChatMessage:
            {
                var message = await context.ChatRoomMessages.FirstOrDefaultAsync(m => m.Id == resourceId, cancellationToken);
                if (message is not null)
                {
                    message.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.ForumPost:
            {
                var post = await context.ForumPosts.FirstOrDefaultAsync(p => p.Id == resourceId, cancellationToken);
                if (post is not null)
                {
                    post.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.ForumComment:
            {
                var comment = await context.ForumComments.FirstOrDefaultAsync(c => c.Id == resourceId, cancellationToken);
                if (comment is not null)
                {
                    comment.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.ProposalComment:
            {
                var comment = await context.ProposalComments.FirstOrDefaultAsync(c => c.Id == resourceId, cancellationToken);
                if (comment is not null)
                {
                    comment.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.Proposal:
            {
                var proposal = await context.Proposals.FirstOrDefaultAsync(p => p.Id == resourceId, cancellationToken);
                if (proposal is not null)
                {
                    proposal.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.DirectMessage:
            {
                var dm = await context.DirectMessages.FirstOrDefaultAsync(m => m.Id == resourceId, cancellationToken);
                if (dm is not null)
                {
                    dm.IsDeleted = true;
                }

                break;
            }
            case ContentReportTargetType.UserProfile:
                break;
        }
    }

    public async Task<int> PurgeExpiredNonCsamEvidenceAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        retentionDays = Math.Clamp(retentionDays, 1, 3650);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var expired = await context.ContentReports
            .Where(r => r.CreatedAt < cutoff
                && r.Reason != ContentReportReason.ChildSexualExploitation
                && r.Status != ContentReportStatus.QueuedForNcmec
                && r.EscalatedToNcmecAt == null
                && r.EvidenceCiphertext != string.Empty)
            .ToListAsync(cancellationToken);

        foreach (var report in expired)
        {
            report.EvidenceCiphertext = string.Empty;
            report.EvidenceNonce = string.Empty;
            if (report.Status is ContentReportStatus.Received or ContentReportStatus.Actioned or ContentReportStatus.EscalatedToVendor)
            {
                report.Status = ContentReportStatus.Closed;
                report.ClosedAt ??= DateTime.UtcNow;
            }
        }

        return expired.Count;
    }
}
