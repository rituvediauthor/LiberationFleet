using LiberationFleet.Server.Application.Features.Activity.Queries.GetUserActivity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ActivityController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string category = "All",
        [FromQuery] DateTime? beforeCreatedAt = null,
        [FromQuery] string? beforeKey = null,
        [FromQuery] int limit = 50)
    {
        var result = await mediator.Send(new GetUserActivityQuery(category, beforeCreatedAt, beforeKey, limit));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
