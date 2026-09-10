using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateLocation;

public class UpdateLocationCommand : IRequest<ProfileOperationResponse>
{
    public EncryptedLocationDto? EncryptedLocation { get; set; }
    public bool ClearLocation { get; set; }
}
