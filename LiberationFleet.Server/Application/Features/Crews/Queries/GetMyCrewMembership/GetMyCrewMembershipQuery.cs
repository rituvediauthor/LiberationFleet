using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQuery : IRequest<CrewMembershipStatusDto>;
