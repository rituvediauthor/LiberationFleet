using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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
    string? ThumbnailResourceId,
    LibraryOfferingKind Kind,
    LibraryFulfillmentMode FulfillmentMode,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryOfferingOperationResponse>;

public class CreateLibraryOfferingCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    LibraryContributionGiftService contributionGiftService,
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

        if (!request.QuantityNotApplicable && (request.Quantity < 1 || request.Quantity > MaxQuantity))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = $"Quantity must be between 1 and {MaxQuantity}." };
        }

        if (request.QuantityNotApplicable && request.Kind == LibraryOfferingKind.Durable)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Durable goods require a specific quantity." };
        }

        if (request.Kind == LibraryOfferingKind.Durable && request.FulfillmentMode == LibraryFulfillmentMode.OnDemand)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Durable goods must use on-request fulfillment." };
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
        var isStock = request.Kind is LibraryOfferingKind.Consumable or LibraryOfferingKind.Service;
        var quantityNotApplicable = request.QuantityNotApplicable || request.Kind == LibraryOfferingKind.Service;
        var offering = new LibraryOffering
        {
            CrewId = membership.CrewId,
            CreatorUserId = userId,
            Kind = request.Kind,
            FulfillmentMode = request.FulfillmentMode,
            Title = title,
            TitleNormalized = title.ToLowerInvariant(),
            DescriptionPreview = descriptionPreview,
            ValuePerUnit = request.ValuePerUnit,
            UnitLabel = unitLabel,
            RemainingStock = isStock && !quantityNotApplicable ? request.Quantity : null,
            QuantityNotApplicable = quantityNotApplicable,
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

        var contributionQuantity = request.Kind == LibraryOfferingKind.Durable
            ? request.Quantity
            : quantityNotApplicable
                ? 1
                : request.Quantity;
        var totalValue = request.ValuePerUnit * contributionQuantity;
        var gift = await contributionGiftService.CreateContributionGiftAsync(
            membership.CrewId,
            userId,
            totalValue,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryOfferingOperationResponse
        {
            Success = true,
            Message = "Offering created.",
            OfferingId = offering.Id,
            GiftId = gift.Id,
            UnitIds = units.Select(u => u.Id).ToList()
        };
    }
}
