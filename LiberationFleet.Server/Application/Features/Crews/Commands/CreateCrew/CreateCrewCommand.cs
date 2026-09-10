using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommand : IRequest<CrewOperationResponse>
{
    public string Name { get; set; } = string.Empty;
    public int MaxSize { get; set; }
    public string Privacy { get; set; } = "Public";
    public string Scope { get; set; } = "Online";
    public string? CountryCode { get; set; }
    public List<string> AllowedZipCodes { get; set; } = [];
}
