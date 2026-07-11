using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryMapper
{
    public static LibraryCategoryDto MapCategory(LibraryCategory category) =>
        new()
        {
            Id = category.Id,
            Name = category.Name
        };

    public static LibraryUnitListItemDto MapUnitListItem(LibraryUnit unit)
    {
        var isStock = LibraryOfferingRules.IsStockBased(unit.Offering);
        return new LibraryUnitListItemDto
        {
            UnitId = unit.Id,
            OfferingId = unit.OfferingId,
            HolderUserId = isStock ? unit.Offering.CreatorUserId : unit.CurrentPossessorUserId,
            HolderUsername = isStock
                ? unit.Offering.CreatorUser?.Username ?? unit.CurrentPossessorUser.Username
                : unit.CurrentPossessorUser.Username,
            Title = unit.Offering.Title,
            DescriptionPreview = unit.Offering.DescriptionPreview,
            Categories = unit.Offering.Categories
                .Select(c => c.Category.Name)
                .OrderBy(name => name)
                .ToList(),
            ThumbnailResourceId = unit.Offering.ThumbnailResourceId,
            HasEncryptedContent = unit.Offering.HasEncryptedContent,
            RemainingStock = unit.Offering.RemainingStock,
            QuantityNotApplicable = unit.Offering.QuantityNotApplicable,
            IsOutOfStock = LibraryOfferingRules.IsOutOfStock(unit.Offering),
            OfferingKind = unit.Offering.Kind.ToString(),
            FulfillmentMode = unit.Offering.FulfillmentMode.ToString()
        };
    }

    public static LibraryOfferingListItemDto MapOfferingListItem(LibraryOffering offering) =>
        new()
        {
            OfferingId = offering.Id,
            UnitId = offering.Units.FirstOrDefault()?.Id,
            Title = offering.Title,
            DescriptionPreview = offering.DescriptionPreview,
            Categories = offering.Categories
                .Select(c => c.Category.Name)
                .OrderBy(name => name)
                .ToList(),
            ThumbnailResourceId = offering.ThumbnailResourceId,
            HasEncryptedContent = offering.HasEncryptedContent,
            OfferingKind = offering.Kind.ToString(),
            FulfillmentMode = offering.FulfillmentMode.ToString(),
            RemainingStock = offering.RemainingStock,
            QuantityNotApplicable = offering.QuantityNotApplicable,
            IsOutOfStock = LibraryOfferingRules.IsOutOfStock(offering),
            ValuePerUnit = offering.ValuePerUnit,
            UnitLabel = offering.UnitLabel,
            CreatedAt = offering.CreatedAt
        };

    public static LibraryUnitDetailDto MapUnitDetail(LibraryUnit unit, LibraryUnitViewerContextDto viewer)
    {
        var isStock = LibraryOfferingRules.IsStockBased(unit.Offering);
        return new LibraryUnitDetailDto
        {
            UnitId = unit.Id,
            OfferingId = unit.OfferingId,
            HolderUserId = isStock ? unit.Offering.CreatorUserId : unit.CurrentPossessorUserId,
            HolderUsername = isStock
                ? unit.Offering.CreatorUser?.Username ?? unit.CurrentPossessorUser.Username
                : unit.CurrentPossessorUser.Username,
            Title = unit.Offering.Title,
            DescriptionPreview = unit.Offering.DescriptionPreview,
            Categories = unit.Offering.Categories
                .Select(c => c.Category.Name)
                .OrderBy(name => name)
                .ToList(),
            ThumbnailResourceId = unit.Offering.ThumbnailResourceId,
            HasEncryptedContent = unit.Offering.HasEncryptedContent,
            UnitStatus = unit.Status.ToString(),
            ValuePerUnit = unit.Offering.ValuePerUnit,
            UnitLabel = unit.Offering.UnitLabel,
            RemainingStock = unit.Offering.RemainingStock,
            QuantityNotApplicable = unit.Offering.QuantityNotApplicable,
            IsOutOfStock = LibraryOfferingRules.IsOutOfStock(unit.Offering),
            OfferingKind = unit.Offering.Kind.ToString(),
            FulfillmentMode = unit.Offering.FulfillmentMode.ToString(),
            BrokenPendingConfirmation = unit.BrokenPendingConfirmation,
            IsRetired = unit.IsRetired,
            Viewer = viewer
        };
    }

    public static LibraryRequestListItemDto MapRequestListItem(LibraryRequest request) =>
        new()
        {
            RequestId = request.Id,
            UnitId = request.UnitId,
            OfferingId = request.Unit.OfferingId,
            HolderUserId = request.Unit.CurrentPossessorUserId,
            HolderUsername = request.Unit.CurrentPossessorUser.Username,
            RequesterUserId = request.RequesterUserId,
            RequesterUsername = request.RequesterUser?.Username ?? string.Empty,
            Title = request.Unit.Offering.Title,
            DescriptionPreview = request.Unit.Offering.DescriptionPreview,
            PurposePreview = request.PurposePreview,
            Categories = request.Unit.Offering.Categories
                .Select(c => c.Category.Name)
                .OrderBy(name => name)
                .ToList(),
            ThumbnailResourceId = request.Unit.Offering.ThumbnailResourceId,
            HasEncryptedContent = request.Unit.Offering.HasEncryptedContent,
            HasEncryptedPurpose = request.HasEncryptedContent,
            Status = request.Status.ToString(),
            Quantity = request.Quantity,
            NeededByStart = request.NeededByStart,
            NeededByEnd = request.NeededByEnd,
            CreatedAt = request.CreatedAt
        };

    public static LibraryRequestDetailDto MapRequestDetail(
        LibraryRequest request,
        int viewerUserId,
        int openRequestCountOnUnit = 1) =>
        new()
        {
            RequestId = request.Id,
            UnitId = request.UnitId,
            OfferingId = request.Unit.OfferingId,
            HolderUserId = request.Unit.CurrentPossessorUserId,
            HolderUsername = request.Unit.CurrentPossessorUser.Username,
            RequesterUserId = request.RequesterUserId,
            RequesterUsername = request.RequesterUser?.Username ?? string.Empty,
            Title = request.Unit.Offering.Title,
            DescriptionPreview = request.Unit.Offering.DescriptionPreview,
            PurposePreview = request.PurposePreview,
            Categories = request.Unit.Offering.Categories
                .Select(c => c.Category.Name)
                .OrderBy(name => name)
                .ToList(),
            ThumbnailResourceId = request.Unit.Offering.ThumbnailResourceId,
            HasEncryptedContent = request.Unit.Offering.HasEncryptedContent,
            HasEncryptedPurpose = request.HasEncryptedContent,
            Status = request.Status.ToString(),
            Quantity = request.Quantity,
            NeededByStart = request.NeededByStart,
            NeededByEnd = request.NeededByEnd,
            CreatedAt = request.CreatedAt,
            IsPossessorView = LibraryRequestAccess.IsPossessor(request, viewerUserId),
            CanEdit = LibraryRequestAccess.IsRequester(request, viewerUserId)
                && request.Status == LibraryRequestStatus.Open,
            CanCancel = LibraryRequestAccess.IsRequester(request, viewerUserId)
                && (request.Status == LibraryRequestStatus.Open || request.Status == LibraryRequestStatus.Denied),
            CanComplete = LibraryRequestAccess.IsPossessor(request, viewerUserId)
                && request.Status == LibraryRequestStatus.Open,
            CanDeny = LibraryRequestAccess.IsPossessor(request, viewerUserId)
                && request.Status == LibraryRequestStatus.Open,
            CanUndeny = LibraryRequestAccess.IsPossessor(request, viewerUserId)
                && request.Status == LibraryRequestStatus.Denied,
            CanMessage = LibraryRequestAccess.CanMessage(request, viewerUserId),
            OpenRequestCountOnUnit = openRequestCountOnUnit
        };

    public static LibraryRequestMessageDto MapRequestMessage(
        LibraryRequestMessage message,
        EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = message.Id,
            AuthorUserId = message.AuthorUserId,
            AuthorUsername = message.AuthorUser.Username,
            CreatedAt = message.CreatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is null ? null : CryptoMapper.MapPayload(envelope)
        };

    public static LibraryCreatorContributionGiftDto? MapContributionGift(CreatorContributionGiftDetails? details) =>
        details is null
            ? null
            : new LibraryCreatorContributionGiftDto
            {
                GiftId = details.GiftId,
                ContributorUserId = details.ContributorUserId,
                ContributorUsername = details.ContributorUsername,
                Amount = details.Amount,
                ItemTitle = details.ItemTitle,
                RecipientUserId = details.RecipientUserId,
                RecipientUsername = details.RecipientUsername,
                CrewGiftRecipientUserId = details.CrewGiftRecipientUserId
            };
}
