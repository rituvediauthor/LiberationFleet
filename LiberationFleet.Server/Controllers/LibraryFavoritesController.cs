using LiberationFleet.Server.Application.Features.Library.Commands.SetLibraryFavorite;
using LiberationFleet.Server.Application.Features.Library.Queries.GetFavoriteLibraryUnits;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
public class LibraryFavoritesController(IMediator mediator) : ControllerBase
{
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites(
        [FromQuery] string? search,
        [FromQuery] int[]? categoryIds,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0)
    {
        var result = await mediator.Send(new GetFavoriteLibraryUnitsQuery(
            search,
            categoryIds ?? Array.Empty<int>(),
            limit,
            offset));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("favorites/ids")]
    public async Task<IActionResult> GetFavoriteIds()
    {
        var result = await mediator.Send(new GetFavoriteLibraryUnitIdsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    public class SetLibraryFavoriteRequest
    {
        public bool Favorited { get; set; }
    }

    [HttpPut("units/{unitId:int}/favorite")]
    public async Task<IActionResult> SetFavorite(int unitId, [FromBody] SetLibraryFavoriteRequest body)
    {
        var result = await mediator.Send(new SetLibraryFavoriteCommand(unitId, body.Favorited));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
