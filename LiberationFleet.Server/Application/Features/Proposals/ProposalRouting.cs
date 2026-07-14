using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalRouting
{
    public static string DetailUrl(Proposal proposal) =>
        proposal.FleetId.HasValue
            ? $"/app/fleet/proposals/{proposal.Id}"
            : $"/app/crew/proposals/{proposal.Id}";

    public static string CommentUrl(Proposal proposal, int commentId) =>
        $"{DetailUrl(proposal)}?commentId={commentId}";

    public static string RejectedListUrl(Proposal proposal) =>
        proposal.FleetId.HasValue
            ? "/app/fleet/proposals/list/rejected"
            : "/app/crew/proposals/list/rejected";
}
