using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposalComment;

public record CreateProposalCommentCommand(
    int ProposalId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ProposalOperationResponse>;

public class CreateProposalCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProposalCommentCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(CreateProposalCommentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        if (request.ParentCommentId.HasValue)
        {
            var parent = await proposalRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent is null || parent.ProposalId != proposal.Id)
            {
                return new ProposalOperationResponse { Success = false, Message = "Parent comment not found." };
            }
        }

        var utcNow = DateTime.UtcNow;
        var comment = new ProposalComment
        {
            ProposalId = proposal.Id,
            AuthorUserId = userId,
            ParentCommentId = request.ParentCommentId,
            CreatedAt = utcNow
        };

        await proposalRepository.AddCommentAsync(comment, cancellationToken);
        proposal.LastActivityAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProposalComment,
            ResourceId = comment.Id.ToString(),
            CrewId = proposal.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
