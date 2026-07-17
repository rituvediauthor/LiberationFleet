using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Features.Reports.Commands.CreateContentReport;

public record CreateContentReportCommand(
    ContentReportReason Reason,
    ContentReportTargetType TargetType,
    int? TargetResourceId,
    int? TargetParentId,
    int? TargetAuthorUserId,
    int? CrewId,
    int? FleetId,
    string? ReporterNote,
    string EvidencePlaintextJson,
    bool AlsoBlockAuthor) : IRequest<CreateContentReportResponse>;

public class CreateContentReportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ReportId { get; set; }
    public ContentReportStatus? Status { get; set; }
}

public class CreateContentReportCommandHandler(
    ICurrentUserService currentUser,
    IContentReportRepository reportRepository,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    IFriendshipRepository friendshipRepository,
    IReportEvidenceProtector evidenceProtector,
    IReportVendorNotifier vendorNotifier,
    IOptions<ReportEvidenceOptions> reportOptions,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateContentReportCommand, CreateContentReportResponse>
{
    private const int MaxEvidenceChars = 50_000;
    private const int MaxNoteChars = 1000;

    public async Task<CreateContentReportResponse> Handle(
        CreateContentReportCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        if (string.IsNullOrWhiteSpace(request.EvidencePlaintextJson))
        {
            return Fail("Report evidence is required.");
        }

        if (request.EvidencePlaintextJson.Length > MaxEvidenceChars)
        {
            return Fail("Report evidence is too large.");
        }

        if (!Enum.IsDefined(request.Reason) || !Enum.IsDefined(request.TargetType))
        {
            return Fail("Invalid report reason or target.");
        }

        try
        {
            using var _ = JsonDocument.Parse(request.EvidencePlaintextJson);
        }
        catch (JsonException)
        {
            return Fail("Report evidence must be valid JSON.");
        }

        var reporterId = currentUser.UserId.Value;
        var (nonce, ciphertext) = evidenceProtector.Seal(request.EvidencePlaintextJson);

        var isCsam = request.Reason == ContentReportReason.ChildSexualExploitation;
        var isNcii = request.Reason == ContentReportReason.NonConsensualIntimateImage;
        var autoEscalate = !isCsam && reportOptions.Value.AutoEscalateNonCsamToVendor;

        var status = isCsam
            ? ContentReportStatus.QueuedForNcmec
            : autoEscalate
                ? ContentReportStatus.EscalatedToVendor
                : ContentReportStatus.Received;

        var report = new ContentReport
        {
            ReporterUserId = reporterId,
            Reason = request.Reason,
            TargetType = request.TargetType,
            TargetResourceId = request.TargetResourceId,
            TargetParentId = request.TargetParentId,
            TargetAuthorUserId = request.TargetAuthorUserId,
            CrewId = request.CrewId,
            FleetId = request.FleetId,
            ReporterNote = Truncate(request.ReporterNote, MaxNoteChars),
            EvidenceNonce = nonce,
            EvidenceCiphertext = ciphertext,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            EscalatedToNcmecAt = isCsam ? DateTime.UtcNow : null,
            EscalatedToVendorAt = autoEscalate ? DateTime.UtcNow : null
        };

        var shouldQuarantine = (isCsam || isNcii)
            && request.TargetResourceId.HasValue
            && request.TargetType != ContentReportTargetType.UserProfile;

        if (shouldQuarantine)
        {
            await reportRepository.SoftDeleteTargetAsync(
                request.TargetType,
                request.TargetResourceId!.Value,
                request.TargetParentId,
                cancellationToken);
            report.TargetQuarantined = true;
        }

        if (isCsam && request.TargetAuthorUserId.HasValue && request.TargetAuthorUserId.Value != reporterId)
        {
            var author = await userRepository.GetByIdWithProfileAsync(request.TargetAuthorUserId.Value, cancellationToken);
            if (author is not null && author.IsActive)
            {
                author.IsActive = false;
                await userRepository.UpdateAsync(author, cancellationToken);
                report.TargetAuthorFrozen = true;
            }
        }

        if (request.AlsoBlockAuthor
            && request.TargetAuthorUserId.HasValue
            && request.TargetAuthorUserId.Value != reporterId)
        {
            var blockedId = request.TargetAuthorUserId.Value;
            var existing = await blockRepository.GetBlockAsync(reporterId, blockedId, cancellationToken);
            if (existing is null)
            {
                var friendship = await friendshipRepository.GetBetweenUsersAsync(reporterId, blockedId, cancellationToken);
                if (friendship is not null)
                {
                    friendshipRepository.Remove(friendship);
                }

                await blockRepository.AddAsync(new UserBlock
                {
                    BlockerUserId = reporterId,
                    BlockedUserId = blockedId,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        await reportRepository.AddAsync(report, cancellationToken);
        await reportRepository.AddAccessLogAsync(new ContentReportAccessLog
        {
            ContentReport = report,
            Actor = $"user:{reporterId}",
            Action = "created",
            AccessedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await vendorNotifier.NotifyNewReportAsync(report, cancellationToken);
        }
        catch
        {
            // Best-effort; notifier logs failures.
        }

        var message = isCsam
            ? "Report received. Child-exploitation reports are prioritized for required safety handling."
            : isNcii
                ? "Report received. The reported content has been hidden pending review."
                : "Report received. Thank you.";

        return new CreateContentReportResponse
        {
            Success = true,
            Message = message,
            ReportId = report.Id,
            Status = report.Status
        };
    }

    private static CreateContentReportResponse Fail(string message) =>
        new() { Success = false, Message = message };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : (value.Trim().Length <= max ? value.Trim() : value.Trim()[..max]);
}
