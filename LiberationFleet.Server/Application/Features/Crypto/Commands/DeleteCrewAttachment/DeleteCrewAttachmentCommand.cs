using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.DeleteCrewAttachment;

public record DeleteCrewAttachmentCommand(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int CrewId) : IRequest<CryptoOperationResponse>;

public class DeleteCrewAttachmentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCrewAttachmentCommand, CryptoOperationResponse>
{
    private static readonly HashSet<EncryptedContentType> AttachmentTypes =
    [
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    public async Task<CryptoOperationResponse> Handle(DeleteCrewAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            return new CryptoOperationResponse { Success = false, Message = "Attachment id is required." };
        }

        var domainType = CryptoMapper.ToDomain(request.ContentType);
        if (!AttachmentTypes.Contains(domainType))
        {
            return new CryptoOperationResponse { Success = false, Message = "Only file attachments can be deleted." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (viewerMembership is null || viewerMembership.CrewId != request.CrewId)
        {
            return new CryptoOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        if (!CrewRoleAuthorizationService.CanModerateAttachments(viewerMembership))
        {
            return new CryptoOperationResponse { Success = false, Message = "You do not have permission to delete attachments." };
        }

        var envelope = await cryptoRepository.GetEnvelopeAsync(domainType, request.ResourceId.Trim(), cancellationToken);
        if (envelope is null || envelope.CrewId != request.CrewId)
        {
            return new CryptoOperationResponse { Success = false, Message = "Attachment not found." };
        }

        await cryptoRepository.DeleteEnvelopesAsync(domainType, [request.ResourceId.Trim()], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CryptoOperationResponse { Success = true, Message = "Attachment deleted." };
    }
}
