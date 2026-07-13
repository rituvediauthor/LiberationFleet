using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateKickFromComment;

public record CreateKickFromCommentCommand(int ProposalId, int CommentId, string Reason) : IRequest<ProposalOperationResponse>;

public class CreateKickFromCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ProposalAnonymousAliasService aliasService,
    CrewmateKickProposalService kickProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateKickFromCommentCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(
        CreateKickFromCommentCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        if (proposal.Kind != ProposalKind.General)
        {
            return new ProposalOperationResponse { Success = false, Message = "Kick proposals are only available on general proposals." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId!.Value, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var comment = await proposalRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ProposalId != proposal.Id)
        {
            return new ProposalOperationResponse { Success = false, Message = "Comment not found." };
        }

        if (comment.AuthorUserId == userId)
        {
            return new ProposalOperationResponse { Success = false, Message = "You cannot kick yourself." };
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new ProposalOperationResponse { Success = false, Message = "A reason is required to submit a kick proposal." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(comment.AuthorUserId, proposal.CrewId!.Value, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "That crewmate is no longer in the crew." };
        }

        var alias = await aliasService.GetOrCreateAsync(proposal.Id, comment.AuthorUserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var kickResult = await kickProposalService.CreateFromAnonymousCommentAsync(
            proposal.CrewId!.Value,
            userId,
            comment.AuthorUserId,
            proposal.Id,
            comment.Id,
            alias.Nickname,
            request.Reason.Trim(),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse
        {
            Success = kickResult.Success,
            Message = kickResult.Message,
            ProposalId = kickResult.ProposalId
        };
    }
}
