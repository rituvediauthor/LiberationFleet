using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContent;

public record UpsertEncryptedContentCommand(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId,
    int KeyVersion,
    string Nonce,
    string Ciphertext) : IRequest<CryptoOperationResponse>;

public class UpsertEncryptedContentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertEncryptedContentCommand, CryptoOperationResponse>
{
    private static readonly HashSet<EncryptedContentType> AttachmentTypes =
    [
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    public async Task<CryptoOperationResponse> Handle(UpsertEncryptedContentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.ResourceId)
            || string.IsNullOrWhiteSpace(request.Nonce)
            || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted content payload is required." };
        }

        var userId = currentUser.UserId.Value;
        var domainType = CryptoMapper.ToDomain(request.ContentType);

        if (request.CrewId.HasValue)
        {
            if (!await membershipRepository.IsUserInCrewAsync(userId, request.CrewId.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not in this crew." };
            }

            if (AttachmentTypes.Contains(domainType))
            {
                var membership = await membershipRepository.GetMembershipAsync(userId, request.CrewId.Value, cancellationToken);
                if (membership is null || !membership.CanAttachFiles)
                {
                    return new CryptoOperationResponse
                    {
                        Success = false,
                        Message = "You are not allowed to attach files in this crew."
                    };
                }
            }
        }

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = CryptoMapper.ToDomain(request.ContentType),
            ResourceId = request.ResourceId.Trim(),
            CrewId = request.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Encrypted content saved." };
    }
}
