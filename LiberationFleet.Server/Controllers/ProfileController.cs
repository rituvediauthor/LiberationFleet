using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateContentPreferences;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Queries.GetContentPreferences;
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

    [HttpGet("content-preferences")]
    public async Task<IActionResult> GetContentPreferences()
    {
        var result = await _mediator.Send(new GetContentPreferencesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("content-preferences")]
    public async Task<IActionResult> UpdateContentPreferences([FromBody] UpdateContentPreferencesRequest body)
    {
        var result = await _mediator.Send(new UpdateContentPreferencesCommand(body.AdultContentPreference, body.SettingsPassword));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
