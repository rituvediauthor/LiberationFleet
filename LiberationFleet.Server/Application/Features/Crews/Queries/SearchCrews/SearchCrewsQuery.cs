using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQuery : IRequest<CrewSearchResponse>
{
    public string Scope { get; set; } = "Online";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    /// <summary>Required for Local scope. Ephemeral — not read from stored profile.</summary>
    public string? CountryCode { get; set; }
    /// <summary>Required for Local scope. Ephemeral — not read from stored profile.</summary>
    public string? ZipCode { get; set; }
}
