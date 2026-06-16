using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var profile = await _mediator.Send(new GetMyProfileQuery());
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
