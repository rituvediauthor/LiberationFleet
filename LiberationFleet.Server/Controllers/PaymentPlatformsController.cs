using LiberationFleet.Server.Application.Features.PaymentPlatforms.Queries.GetPaymentPlatforms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/payment-platforms")]
[Authorize]
public class PaymentPlatformsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentPlatformsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var platforms = await _mediator.Send(new GetPaymentPlatformsQuery());
        return Ok(platforms);
    }
}
