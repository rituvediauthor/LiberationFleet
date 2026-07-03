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
    public bool BrokenPendingConfirmation { get; set; }
    public bool IsRetired { get; set; }
    public LibraryUnitViewerContextDto Viewer { get; set; } = new();
}

public class UpdateLibraryOfferingRequest
{
    public bool? IsOutOfStock { get; set; }
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
    public int Quantity { get; set; }
    public DateTime NeededByStart { get; set; }
    public DateTime NeededByEnd { get; set; }
    public DateTime CreatedAt { get; set; }
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
}
