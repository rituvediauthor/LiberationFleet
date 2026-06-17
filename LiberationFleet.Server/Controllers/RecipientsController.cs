using LiberationFleet.Server.Application.Features.Recipients.Contracts;
using LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RecipientsController(IMediator mediator) : ControllerBase
{
    [HttpGet("reception-order")]
    public async Task<ActionResult<ReceptionOrderResponse>> GetReceptionOrder([FromQuery] int limit = 30)
    {
        var query = new GetReceptionOrderQuery(limit);
        var result = await mediator.Send(query);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
