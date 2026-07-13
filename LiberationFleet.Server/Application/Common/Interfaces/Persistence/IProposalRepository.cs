using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Proposal?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> GetByCrewAndStatusAsync(
        int crewId,
        ProposalStatus status,
        CancellationToken cancellationToken = default);
    Task<int> GetActiveCrewMemberCountAsync(int crewId, CancellationToken cancellationToken = default);
    Task<ProposalVote?> GetVoteAsync(int proposalId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalComment>> GetCommentsByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default);
    Task<ProposalComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task AddProposalAsync(Proposal proposal, CancellationToken cancellationToken = default);
    Task AddVoteAsync(ProposalVote vote, CancellationToken cancellationToken = default);
    Task AddCommentAsync(ProposalComment comment, CancellationToken cancellationToken = default);
    Task<ProposalCrewSettingChange?> GetCrewSettingChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewSettingChange>> GetCrewSettingChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewSettingChangeAsync(ProposalCrewSettingChange change, CancellationToken cancellationToken = default);
    Task<ProposalCrewRuleChange?> GetCrewRuleChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewRuleChange>> GetCrewRuleChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewRuleChangeAsync(ProposalCrewRuleChange change, CancellationToken cancellationToken = default);
    Task<ProposalCrewChatChange?> GetCrewChatChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewChatChange>> GetCrewChatChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewChatChangeAsync(ProposalCrewChatChange change, CancellationToken cancellationToken = default);
    Task<ProposalCrewmateKick?> GetCrewmateKickByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewmateKick>> GetCrewmateKicksByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewmateKickAsync(ProposalCrewmateKick kick, CancellationToken cancellationToken = default);
    Task<ProposalCrewmateKick?> GetPendingCrewmateKickForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewmateKick?> GetPendingSeasonKickForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewmateRejoin?> GetCrewmateRejoinByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewmateRejoin>> GetCrewmateRejoinsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewmateRejoinAsync(ProposalCrewmateRejoin rejoin, CancellationToken cancellationToken = default);
    Task<ProposalCrewmateRejoin?> GetPendingCrewmateRejoinForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewJoinRequest?> GetCrewJoinRequestByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewJoinRequest>> GetCrewJoinRequestsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewJoinRequestAsync(ProposalCrewJoinRequest joinRequest, CancellationToken cancellationToken = default);
    Task<ProposalCrewRoleChange?> GetCrewRoleChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewRoleChange>> GetCrewRoleChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewRoleChangeAsync(ProposalCrewRoleChange roleChange, CancellationToken cancellationToken = default);
    Task<ProposalCrewRoleChange?> GetPendingCrewRoleChangeForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default);
    Task<ProposalClaimPlaceholderIdentity?> GetClaimPlaceholderIdentityByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalClaimPlaceholderIdentity>> GetClaimPlaceholderIdentitiesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddClaimPlaceholderIdentityAsync(
        ProposalClaimPlaceholderIdentity claim,
        CancellationToken cancellationToken = default);
    Task<ProposalClaimPlaceholderIdentity?> GetPendingClaimPlaceholderIdentityForPlaceholderAsync(
        int crewId,
        int placeholderUserId,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewmatePermissionGrant?> GetCrewmatePermissionGrantByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewmatePermissionGrant>> GetCrewmatePermissionGrantsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddCrewmatePermissionGrantAsync(
        ProposalCrewmatePermissionGrant grant,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewmatePermissionGrant?> GetPendingCrewmatePermissionGrantForTargetAsync(
        int crewId,
        int targetUserId,
        CrewmatePermissionGrantType grantType,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewJoinRequest?> GetPendingJoinRequestForApplicantAndCrewAsync(
        int applicantUserId,
        int crewId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> GetJoinRequestProposalsByApplicantAsync(
        int applicantUserId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetPendingJoinApplicantUserIdsForCrewAsync(
        int crewId,
        CancellationToken cancellationToken = default);
    Task RejectPendingJoinRequestsForApplicantAsync(
        int applicantUserId,
        int exceptProposalId,
        CancellationToken cancellationToken = default);
    Task<ProposalCrewApplyToFleet?> GetCrewApplyToFleetByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddCrewApplyToFleetAsync(ProposalCrewApplyToFleet apply, CancellationToken cancellationToken = default);
    Task<ProposalCrewApplyToFleet?> GetPendingCrewApplyToFleetAsync(
        int crewId,
        int fleetId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> GetPendingCrewApplyToFleetProposalsByCrewAsync(
        int crewId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalCrewApplyToFleet>> GetCrewApplyToFleetsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task<ProposalFleetJoinRequest?> GetFleetJoinRequestByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddFleetJoinRequestAsync(ProposalFleetJoinRequest joinRequest, CancellationToken cancellationToken = default);
    Task<ProposalFleetJoinRequest?> GetPendingFleetJoinRequestAsync(
        int fleetId,
        int applicantCrewId,
        CancellationToken cancellationToken = default);
    Task<ProposalFleetSettingChange?> GetFleetSettingChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddFleetSettingChangeAsync(ProposalFleetSettingChange change, CancellationToken cancellationToken = default);
    Task<ProposalFleetKickCrew?> GetFleetKickCrewByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddFleetKickCrewAsync(ProposalFleetKickCrew kick, CancellationToken cancellationToken = default);
    Task<ProposalFleetKickCrew?> GetPendingFleetKickCrewAsync(
        int fleetId,
        int targetCrewId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> GetByFleetAndStatusAsync(
        int fleetId,
        ProposalStatus status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalFleetSettingChange>> GetFleetSettingChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalFleetJoinRequest>> GetFleetJoinRequestsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalFleetKickCrew>> GetFleetKickCrewsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalFleetRuleChange>> GetFleetRuleChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task<ProposalFleetNotice?> GetFleetNoticeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, ProposalFleetNotice>> GetFleetNoticesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default);
    Task AddFleetNoticeAsync(ProposalFleetNotice notice, CancellationToken cancellationToken = default);
    Task<ProposalFleetRuleChange?> GetFleetRuleChangeByProposalIdAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddFleetRuleChangeAsync(ProposalFleetRuleChange change, CancellationToken cancellationToken = default);
    Task<ProposalAnonymousAlias?> GetAnonymousAliasAsync(int proposalId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalAnonymousAlias>> GetAnonymousAliasesAsync(
        int proposalId,
        IEnumerable<int> userIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAnonymousNicknamesAsync(int proposalId, CancellationToken cancellationToken = default);
    Task AddAnonymousAliasAsync(ProposalAnonymousAlias alias, CancellationToken cancellationToken = default);
    void RemoveVote(ProposalVote vote);
}
