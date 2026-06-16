using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQuery : IRequest<UserProfileDto?> { }
