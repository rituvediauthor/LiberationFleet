using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Queries.GetProposalDetail;

public record GetProposalDetailQuery(int ProposalId) : IRequest<ProposalDetailResponse>;

public class GetProposalDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    CrewSettingsProposalService crewSettingsProposalService,
    CrewRulesProposalService crewRulesProposalService,
    CrewChatsProposalService crewChatsProposalService,
    CrewmateKickProposalService crewmateKickProposalService,
    CrewmateRejoinProposalService crewmateRejoinProposalService,
    CrewJoinRequestProposalService crewJoinRequestProposalService,
    CrewRoleProposalService crewRoleProposalService,
    ClaimPlaceholderIdentityProposalService claimPlaceholderIdentityProposalService,
    CrewmatePermissionProposalService crewmatePermissionProposalService,
    ProposalAnonymousAliasService aliasService,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<GetProposalDetailQuery, ProposalDetailResponse>
{
    public async Task<ProposalDetailResponse> Handle(GetProposalDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdWithAuthorAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalDetailResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        if (!ProposalMapper.IsVisibleDespiteBlock(proposal.Kind) && hiddenUserIds.Contains(proposal.AuthorUserId))
        {
            return new ProposalDetailResponse { Success = false, Message = "Proposal not found." };
        }

        var utcNow = DateTime.UtcNow;
        var statusBefore = proposal.Status;
        ProposalVotingService.TryAutoApproveOnTimer(proposal, utcNow);
        await ProposalApprovalCoordinator.ProcessNewlyApprovedAsync(
            proposal,
            statusBefore,
            crewSettingsProposalService,
            crewRulesProposalService,
            crewChatsProposalService,
            crewmateKickProposalService,
            crewmateRejoinProposalService,
            crewJoinRequestProposalService,
            crewRoleProposalService,
            claimPlaceholderIdentityProposalService,
            crewmatePermissionProposalService,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        EncryptedContentEnvelope? proposalEnvelope = null;
        if (proposal.Kind == ProposalKind.General)
        {
            proposalEnvelope = await cryptoRepository.GetEnvelopeAsync(
                EncryptedContentType.Proposal,
                proposal.Id.ToString(),
                cancellationToken);
        }

        var crewSettingChange = proposal.Kind == ProposalKind.CrewSettingChange
            ? await proposalRepository.GetCrewSettingChangeByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewRuleChange = proposal.Kind == ProposalKind.CrewRuleChange
            ? await proposalRepository.GetCrewRuleChangeByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewChatChange = proposal.Kind == ProposalKind.CrewChatChange
            ? await proposalRepository.GetCrewChatChangeByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewmateKick = proposal.Kind == ProposalKind.CrewmateKick
            ? await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewmateRejoin = proposal.Kind == ProposalKind.CrewmateRejoin
            ? await proposalRepository.GetCrewmateRejoinByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewJoinRequest = proposal.Kind == ProposalKind.CrewJoinRequest
            ? await proposalRepository.GetCrewJoinRequestByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewRoleChange = proposal.Kind == ProposalKind.CrewRoleChange
            ? await proposalRepository.GetCrewRoleChangeByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var claimPlaceholderIdentity = proposal.Kind == ProposalKind.ClaimPlaceholderIdentity
            ? await proposalRepository.GetClaimPlaceholderIdentityByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var crewmatePermissionGrant = proposal.Kind == ProposalKind.CrewmatePermissionGrant
            ? await proposalRepository.GetCrewmatePermissionGrantByProposalIdAsync(proposal.Id, cancellationToken)
            : null;

        var comments = await proposalRepository.GetCommentsByProposalIdAsync(proposal.Id, cancellationToken);
        var visibleComments = comments
            .Where(c => !hiddenUserIds.Contains(c.AuthorUserId))
            .ToList();
        var topLevel = visibleComments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = visibleComments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProposalComment,
            commentIds,
            proposal.CrewId,
            cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var usesAnonymousComments = proposal.Kind == ProposalKind.General;
        string? viewerAlias = null;
        IReadOnlyDictionary<int, string> nicknameByUserId = new Dictionary<int, string>();

        if (usesAnonymousComments)
        {
            var viewerAliasEntity = await aliasService.GetOrCreateAsync(proposal.Id, userId, cancellationToken);
            viewerAlias = viewerAliasEntity.Nickname;

            var authorIds = visibleComments.Select(c => c.AuthorUserId).Distinct();
            nicknameByUserId = await aliasService.GetNicknameMapAsync(proposal.Id, authorIds, cancellationToken);

            foreach (var authorId in authorIds.Where(id => !nicknameByUserId.ContainsKey(id)))
            {
                var created = await aliasService.GetOrCreateAsync(proposal.Id, authorId, cancellationToken);
                nicknameByUserId = new Dictionary<int, string>(nicknameByUserId)
                {
                    [created.UserId] = created.Nickname
                };
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = visibleComments.Count(c => c.ParentCommentId == comment.Id);
            return ProposalMapper.MapComment(
                comment,
                envelope,
                replyCount,
                userId,
                usesAnonymousComments,
                nicknameByUserId);
        }).ToList();

        var vote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);
        var currentUserVote = vote is null ? null : vote.IsApprove ? "approve" : "disapprove";

        return new ProposalDetailResponse
        {
            Success = true,
            Message = "Proposal loaded.",
            Proposal = ProposalMapper.MapDetail(
                proposal,
                proposalEnvelope,
                commentDtos,
                userId,
                crewSettingChange,
                crewRuleChange,
                crewChatChange,
                crewmateKick,
                crewmateRejoin,
                crewJoinRequest,
                crewRoleChange,
                claimPlaceholderIdentity,
                crewmatePermissionGrant,
                currentUserVote,
                viewerAlias)
        };
    }
}
