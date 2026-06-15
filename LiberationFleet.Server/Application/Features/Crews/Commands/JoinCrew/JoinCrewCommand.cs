using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommand : IRequest<CrewOperationResponse>
{
    public int? CrewId { get; set; }
    public string? JoinCode { get; set; }
}
