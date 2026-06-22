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
    void RemoveVote(ProposalVote vote);
}
