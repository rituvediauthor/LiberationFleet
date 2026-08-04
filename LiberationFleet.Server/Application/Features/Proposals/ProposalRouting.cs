using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalRouting
{
    public static string DetailUrl(Proposal proposal) =>
        proposal.FleetId.HasValue
            ? $"/app/fleet/proposals/{proposal.Id}"
            : $"/app/crew/proposals/{proposal.Id}";

    public static string CommentUrl(Proposal proposal, int commentId) =>
        $"{DetailUrl(proposal)}?commentId={commentId}";

    public static string StatusListUrl(Proposal proposal, ProposalStatus? status = null)
    {
        var resolved = status ?? proposal.Status;
        var segment = resolved switch
        {
            ProposalStatus.Approved => "approved",
            ProposalStatus.Rejected => "rejected",
            _ => "pending"
        };
        var basePath = proposal.FleetId.HasValue
            ? $"/app/fleet/proposals/list/{segment}"
            : $"/app/crew/proposals/list/{segment}";
        return $"{basePath}?highlightId={proposal.Id}";
    }

    public static string PendingListUrl(Proposal proposal) =>
        StatusListUrl(proposal, ProposalStatus.Pending);

    public static string ApprovedListUrl(Proposal proposal) =>
        StatusListUrl(proposal, ProposalStatus.Approved);

    public static string RejectedListUrl(Proposal proposal) =>
        StatusListUrl(proposal, ProposalStatus.Rejected);
}
