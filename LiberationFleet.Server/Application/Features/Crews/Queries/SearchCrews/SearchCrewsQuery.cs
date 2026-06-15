using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQuery : IRequest<CrewSearchResponse>
{
    public string Scope { get; set; } = "Online";
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
