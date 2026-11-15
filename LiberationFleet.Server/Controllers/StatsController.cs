using LiberationFleet.Server.Application.Features.Stats.Queries.GetCommunityActivityStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Public product-page counters: crews and crewmates active in the last 30 days.
    /// </summary>
    [HttpGet("community-activity")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommunityActivity(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCommunityActivityStatsQuery(), cancellationToken);
        return Ok(result);
    }
}
