namespace LiberationFleet.Server.Application.Features.Library.Contracts;

using LiberationFleet.Server.Application.Features.Crypto.Contracts;

public class LibraryCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class LibraryUnitListItemDto
{
    public int UnitId { get; set; }
    public int OfferingId { get; set; }
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int HolderUserId { get; set; }
    public string HolderUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public string? ThumbnailResourceId { get; set; }
    public bool HasEncryptedContent { get; set; }
    public int? RemainingStock { get; set; }
    public bool QuantityNotApplicable { get; set; }
    public bool IsOutOfStock { get; set; }
    public string OfferingKind { get; set; } = string.Empty;
    public string FulfillmentMode { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;

    /// <summary>Number of active (non-retired, non-broken) units in this unit's offering.</summary>
    public int OfferingUnitCount { get; set; } = 1;

    /// <summary>True when no Open request currently reserves this unit.</summary>
    public bool AvailableNow { get; set; } = true;

    /// <summary>When unavailable, the soonest date the unit is expected to free up.</summary>
    public DateTime? NextAvailableDate { get; set; }
}

public class LibraryCategoryListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryCategoryDto> Items { get; set; } = Array.Empty<LibraryCategoryDto>();
}

public class LibraryUnitListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryUnitListItemDto> Items { get; set; } = Array.Empty<LibraryUnitListItemDto>();
    public bool HasMore { get; set; }
}

public class CreateLibraryOfferingRequest
{
    public string Title { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public IReadOnlyList<int> CategoryIds { get; set; } = Array.Empty<int>();
    public decimal ValuePerUnit { get; set; }
    public string? UnitLabel { get; set; }
    public int Quantity { get; set; } = 1;
    public bool QuantityNotApplicable { get; set; }
    public string? ThumbnailResourceId { get; set; }
    public string Kind { get; set; } = "Durable";
    public string FulfillmentMode { get; set; } = "OnRequest";
    public string Visibility { get; set; } = "CrewOnly";
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class LibraryOfferingOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? OfferingId { get; set; }
    public int? GiftId { get; set; }
    public IReadOnlyList<int> UnitIds { get; set; } = Array.Empty<int>();
}

public class LibraryUnitViewerContextDto
{
    public bool IsHolder { get; set; }
    public bool CanRequest { get; set; }
    public bool CanRecordAcquisition { get; set; }
    public int MaxRequestQuantity { get; set; } = 1;
    public bool BrokenPendingConfirmation { get; set; }
    public bool IsRetired { get; set; }
    public bool CanReportBroken { get; set; }
    public bool CanReportFixed { get; set; }
    public bool CanConfirmBroken { get; set; }
    public bool CanRecordMaintenance { get; set; }
    public bool CanReportLost { get; set; }
    public int? ActiveRequestId { get; set; }
    public string? ActiveRequestStatus { get; set; }
}

public class LibraryUnitDetailDto
{
    public int UnitId { get; set; }
    public int OfferingId { get; set; }
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int HolderUserId { get; set; }
    public string HolderUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public string? ThumbnailResourceId { get; set; }
    public bool HasEncryptedContent { get; set; }
    public string UnitStatus { get; set; } = string.Empty;
    public decimal ValuePerUnit { get; set; }
    public string? UnitLabel { get; set; }
    public int? RemainingStock { get; set; }
    public bool QuantityNotApplicable { get; set; }
    public bool IsOutOfStock { get; set; }
    public string OfferingKind { get; set; } = string.Empty;
    public string FulfillmentMode { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public bool BrokenPendingConfirmation { get; set; }
    public bool IsRetired { get; set; }
    public LibraryUnitViewerContextDto Viewer { get; set; } = new();
}

public class UpdateLibraryOfferingRequest
{
    public bool? IsOutOfStock { get; set; }
    public string? Visibility { get; set; }
    public string? ThumbnailResourceId { get; set; }
    public string? Nonce { get; set; }
    public string? Ciphertext { get; set; }
    public int? KeyVersion { get; set; }
}

public class LibraryUnitDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public LibraryUnitDetailDto? Item { get; set; }
}

public class CreateLibraryRequestRequest
{
    public int Quantity { get; set; } = 1;
    public string PurposePreview { get; set; } = string.Empty;
    public DateTime NeededByStart { get; set; }
    public DateTime NeededByEnd { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpdateLibraryRequestRequest
{
    public string PurposePreview { get; set; } = string.Empty;
    public DateTime NeededByStart { get; set; }
    public DateTime NeededByEnd { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class LibraryRequestOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RequestId { get; set; }
}

public class LibraryRequestListItemDto
{
    public int RequestId { get; set; }
    public int UnitId { get; set; }
    public int OfferingId { get; set; }
    public int HolderUserId { get; set; }
    public string HolderUsername { get; set; } = string.Empty;
    public int RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public string PurposePreview { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public string? ThumbnailResourceId { get; set; }
    public bool HasEncryptedContent { get; set; }
    public bool HasEncryptedPurpose { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OfferingKind { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime NeededByStart { get; set; }
    public DateTime NeededByEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? RequesterPriorityScore { get; set; }
    public bool? HasHighestPriorityAmongOpenRequests { get; set; }
    public int? HigherPriorityRequestId { get; set; }
    public string? HigherPriorityRequesterUsername { get; set; }
}

public class LibraryRequestListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryRequestListItemDto> Items { get; set; } = Array.Empty<LibraryRequestListItemDto>();
}

public class LibraryRequestDetailDto : LibraryRequestListItemDto
{
    public bool IsPossessorView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanComplete { get; set; }
    public bool CanDeny { get; set; }
    public bool CanUndeny { get; set; }
    public bool CanMessage { get; set; }
    public int OpenRequestCountOnUnit { get; set; }
}

public class SendLibraryRequestMessageRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class LibraryRequestMessageDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class LibraryRequestMessageListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryRequestMessageDto> Items { get; set; } = Array.Empty<LibraryRequestMessageDto>();
    public bool HasMore { get; set; }
}

public class LibraryRequestMessageOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? MessageId { get; set; }
    public LibraryRequestMessageDto? Item { get; set; }
}

public class LibraryCompleteRequestResponse : LibraryRequestOperationResponse
{
    public int? GiftId { get; set; }
    public LibraryCreatorContributionGiftDto? ContributionGift { get; set; }
    public LibraryCreatorContributionGiftDto? CompleterGift { get; set; }
    public LibraryCreatorContributionGiftDto? ReceptionGift { get; set; }
}

public class LibraryCreatorContributionGiftDto
{
    public int GiftId { get; set; }
    public int ContributorUserId { get; set; }
    public string ContributorUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public int RecipientUserId { get; set; }
    public string RecipientUsername { get; set; } = string.Empty;
    public int CrewGiftRecipientUserId { get; set; }
}

public class RecordLibraryAcquisitionRequest
{
    public int Quantity { get; set; } = 1;
    public string PurposePreview { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class LibraryOfferingListItemDto
{
    public int OfferingId { get; set; }
    public int? UnitId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public string? ThumbnailResourceId { get; set; }
    public bool HasEncryptedContent { get; set; }
    public string OfferingKind { get; set; } = string.Empty;
    public string FulfillmentMode { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public int? RemainingStock { get; set; }
    public bool QuantityNotApplicable { get; set; }
    public bool IsOutOfStock { get; set; }
    public decimal ValuePerUnit { get; set; }
    public string? UnitLabel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LibraryOfferingListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryOfferingListItemDto> Items { get; set; } = Array.Empty<LibraryOfferingListItemDto>();
    public bool HasMore { get; set; }
}

public class LibraryRequestDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public LibraryRequestDetailDto? Item { get; set; }
}

public class LibraryUnitOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? UnitId { get; set; }
}

public class ReportLibraryUnitBrokenRequest
{
    public string ExplanationPreview { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class RecordLibraryMaintenanceRequest
{
    public decimal Cost { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class LibraryMaintenanceOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? MaintenanceId { get; set; }
    public int? GiftId { get; set; }
    public int? CrewGiftRecipientUserId { get; set; }
}

public class LibraryTaskListItemDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CreatorUsername { get; set; } = string.Empty;
    public int CreatorUserId { get; set; }
    public decimal Value { get; set; }
    public bool HasDeadline { get; set; }
    public bool DeleteOnCompletion { get; set; }
    public string ScheduleSummary { get; set; } = string.Empty;
    public DateTime? NextDueAt { get; set; }
    public bool HasEncryptedContent { get; set; }
}

public class LibraryTaskListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LibraryTaskListItemDto> Items { get; set; } = Array.Empty<LibraryTaskListItemDto>();
}

public class LibraryTaskInstanceDto
{
    public int InstanceId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ClaimedByUserId { get; set; }
    public string? ClaimedByUsername { get; set; }
    public bool ClaimedByCurrentUser { get; set; }
    public bool Selectable { get; set; }
}

public class LibraryTaskDetailDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool HasEncryptedContent { get; set; }
    public decimal Value { get; set; }
    public int CreatorUserId { get; set; }
    public string CreatorUsername { get; set; } = string.Empty;
    public bool IsCreator { get; set; }
    public bool HasDeadline { get; set; }
    public bool DeleteOnCompletion { get; set; }
    public bool CanCompleteAnytime { get; set; }
    public bool HasPendingConfirmation { get; set; }
    public IReadOnlyList<int> PendingConfirmationInstanceIds { get; set; } = Array.Empty<int>();
    public bool AwaitingConfirmationForCurrentUser { get; set; }
    public bool IsRecurring { get; set; }
    public string Frequency { get; set; } = "None";
    public bool TimeSpecific { get; set; }
    public int? SpecificTimeMinutes { get; set; }
    public bool IsSpaced { get; set; }
    public int Interval { get; set; } = 1;
    public bool DaySpecific { get; set; }
    public IReadOnlyList<int> WeekDays { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> MonthDays { get; set; } = Array.Empty<int>();
    public int? YearMonth { get; set; }
    public int? YearDay { get; set; }
    public DateTime? OneShotDueAt { get; set; }
    public string ScheduleSummary { get; set; } = string.Empty;
    public IReadOnlyList<LibraryTaskInstanceDto> Instances { get; set; } = Array.Empty<LibraryTaskInstanceDto>();
}

public class LibraryTaskDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public LibraryTaskDetailDto? Task { get; set; }
}

public class UpsertLibraryTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool HasDeadline { get; set; } = true;
    public bool DeleteOnCompletion { get; set; }
    public bool IsRecurring { get; set; }
    public string Frequency { get; set; } = "None";
    public bool TimeSpecific { get; set; }
    public int? SpecificTimeMinutes { get; set; }
    public bool IsSpaced { get; set; }
    public int Interval { get; set; } = 1;
    public bool DaySpecific { get; set; }
    public IReadOnlyList<int> WeekDays { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> MonthDays { get; set; } = Array.Empty<int>();
    public int? YearMonth { get; set; }
    public int? YearDay { get; set; }
    public DateTime? OneShotDueAt { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class LibraryTaskOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? TaskId { get; set; }
}

public class LibraryTaskInstanceIdsRequest
{
    public IReadOnlyList<int> InstanceIds { get; set; } = Array.Empty<int>();
}

public class LibraryTaskConfirmResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool TaskClosed { get; set; }
    public IReadOnlyList<LibraryCreatorContributionGiftDto> ContributionGifts { get; set; }
        = Array.Empty<LibraryCreatorContributionGiftDto>();
}
