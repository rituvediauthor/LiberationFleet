using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.CreateLibraryOffering;

public record CreateLibraryOfferingCommand(
    string Title,
    string DescriptionPreview,
    IReadOnlyList<int> CategoryIds,
    decimal ValuePerUnit,
    string? UnitLabel,
    int Quantity,
    bool QuantityNotApplicable,
    int? StockTier1,
    int? StockTier2,
    int? StockTier3,
    int? StockTier4,
    int? StockTier5,
    int? StockTier6,
    int MinimumViewerTier,
    string? CountryCode,
    IReadOnlyList<string>? AllowedZipCodes,
    string? ThumbnailResourceId,
    LibraryOfferingKind Kind,
    LibraryFulfillmentMode FulfillmentMode,
    LibraryOfferingVisibility Visibility,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryOfferingOperationResponse>;

public class CreateLibraryOfferingCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLibraryOfferingCommand, LibraryOfferingOperationResponse>
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionPreviewLength = 200;
    private const int MaxUnitLabelLength = 64;
    private const int MaxQuantity = 100;

    public async Task<LibraryOfferingOperationResponse> Handle(
        CreateLibraryOfferingCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Title is required." };
        }

        if (title.Length > MaxTitleLength)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Title is too long." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Encrypted offering content is required." };
        }

        if (request.ValuePerUnit <= 0)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Value per unit must be greater than zero." };
        }

        if (!ZipCodeList.TryValidateOptional(
                request.CountryCode, request.AllowedZipCodes, out var country, out var allowedZips, out var zipError))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = zipError };
        }

        if (!request.QuantityNotApplicable && request.Kind != LibraryOfferingKind.Consumable
            && (request.Quantity < 1 || request.Quantity > MaxQuantity))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = $"Quantity must be between 1 and {MaxQuantity}." };
        }

        if (request.Kind == LibraryOfferingKind.Consumable && !request.QuantityNotApplicable)
        {
            var tiers = ResolveConsumableTier(request);
            if (tiers is null)
            {
                return new LibraryOfferingOperationResponse
                {
                    Success = false,
                    Message = $"Each tier stock must be between 0 and {MaxQuantity}, and at least one tier must have stock."
                };
            }
        }

        if (request.QuantityNotApplicable && request.Kind == LibraryOfferingKind.Durable)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Durable goods require a specific quantity." };
        }

        if (request.Kind == LibraryOfferingKind.Durable && request.FulfillmentMode == LibraryFulfillmentMode.OnDemand)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Durable goods must use on-request fulfillment." };
        }

        if (request.Kind == LibraryOfferingKind.Digital && request.FulfillmentMode != LibraryFulfillmentMode.OnDemand)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Digital goods must use on-demand download." };
        }

        if (request.Kind == LibraryOfferingKind.Digital && !request.QuantityNotApplicable)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Digital goods do not use quantity." };
        }

        var categoryIds = request.CategoryIds.Distinct().ToList();
        if (categoryIds.Count == 0)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Select at least one category." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (request.Kind == LibraryOfferingKind.Digital)
        {
            var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
            var canAttach = membership.IsOrganizer
                || membership.CanAttachFiles
                || (crew?.AllowCrewmateFileAttachments ?? false);
            if (!canAttach)
            {
                return new LibraryOfferingOperationResponse
                {
                    Success = false,
                    Message = "File attachment permission is required to list digital goods."
                };
            }
        }

        var categories = await libraryRepository.GetCategoriesByIdsAsync(categoryIds, cancellationToken);
        if (categories.Count != categoryIds.Count)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "One or more categories are invalid." };
        }

        var descriptionPreview = request.DescriptionPreview.Trim();
        if (descriptionPreview.Length > MaxDescriptionPreviewLength)
        {
            descriptionPreview = descriptionPreview[..MaxDescriptionPreviewLength];
        }

        var unitLabel = string.IsNullOrWhiteSpace(request.UnitLabel)
            ? null
            : request.UnitLabel.Trim();
        if (unitLabel?.Length > MaxUnitLabelLength)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Unit label is too long." };
        }

        var utcNow = DateTime.UtcNow;
        var isStock = request.Kind is LibraryOfferingKind.Consumable
            or LibraryOfferingKind.Service
            or LibraryOfferingKind.Digital;
        var quantityNotApplicable = request.QuantityNotApplicable
            || request.Kind is LibraryOfferingKind.Service or LibraryOfferingKind.Digital;
        var offering = new LibraryOffering
        {
            CrewId = membership.CrewId,
            CreatorUserId = userId,
            Kind = request.Kind,
            FulfillmentMode = request.FulfillmentMode,
            Visibility = request.Visibility,
            Title = title,
            TitleNormalized = title.ToLowerInvariant(),
            DescriptionPreview = descriptionPreview,
            ValuePerUnit = request.ValuePerUnit,
            UnitLabel = unitLabel,
            RemainingStock = null,
            QuantityNotApplicable = quantityNotApplicable,
            MinimumViewerTier = request.Kind == LibraryOfferingKind.Service
                ? LibraryPriorityTier.ClampTier(request.MinimumViewerTier)
                : 1,
            ThumbnailResourceId = string.IsNullOrWhiteSpace(request.ThumbnailResourceId)
                ? null
                : request.ThumbnailResourceId.Trim(),
            HasEncryptedContent = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Categories = categories
                .Select(category => new LibraryOfferingCategory { CategoryId = category.Id })
                .ToList()
        };

        offering.CountryCode = country;
        AllowedZipCodeSync.SetOfferingZips(offering, allowedZips);

        if (request.Kind == LibraryOfferingKind.Consumable && !quantityNotApplicable)
        {
            var tiers = ResolveConsumableTier(request)!;
            LibraryOfferingRules.SetTierStocks(
                offering, tiers[0], tiers[1], tiers[2], tiers[3], tiers[4], tiers[5]);
        }
        else if (isStock && !quantityNotApplicable)
        {
            offering.RemainingStock = request.Quantity;
        }

        await libraryRepository.AddOfferingAsync(offering, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        List<LibraryUnit> units;
        if (request.Kind == LibraryOfferingKind.Durable)
        {
            units = Enumerable.Range(0, request.Quantity)
                .Select(_ => new LibraryUnit
                {
                    OfferingId = offering.Id,
                    CurrentPossessorUserId = userId,
                    Status = LibraryUnitStatus.Available,
                    CreatedAt = utcNow
                })
                .ToList();
        }
        else
        {
            units =
            [
                new LibraryUnit
                {
                    OfferingId = offering.Id,
                    CurrentPossessorUserId = userId,
                    Status = LibraryUnitStatus.Available,
                    CreatedAt = utcNow
                }
            ];
        }

        await libraryRepository.AddUnitsAsync(units, cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryItem,
            ResourceId = offering.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryOfferingOperationResponse
        {
            Success = true,
            Message = "Offering created.",
            OfferingId = offering.Id,
            UnitIds = units.Select(u => u.Id).ToList()
        };
    }

    private static int[]? ResolveConsumableTier(CreateLibraryOfferingCommand request)
    {
        var anyExplicit = request.StockTier1.HasValue
            || request.StockTier2.HasValue
            || request.StockTier3.HasValue
            || request.StockTier4.HasValue
            || request.StockTier5.HasValue
            || request.StockTier6.HasValue;

        int[] tiers;
        if (anyExplicit)
        {
            tiers =
            [
                request.StockTier1 ?? 0,
                request.StockTier2 ?? 0,
                request.StockTier3 ?? 0,
                request.StockTier4 ?? 0,
                request.StockTier5 ?? 0,
                request.StockTier6 ?? 0
            ];
        }
        else
        {
            if (request.Quantity < 1 || request.Quantity > MaxQuantity)
            {
                return null;
            }

            tiers = [request.Quantity, 0, 0, 0, 0, 0];
        }

        if (tiers.Any(t => t < 0 || t > MaxQuantity) || tiers.Sum() < 1)
        {
            return null;
        }

        return tiers;
    }
}
