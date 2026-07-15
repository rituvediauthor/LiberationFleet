using LiberationFleet.Server.Application.Features.Donations.Commands.AcknowledgeDonationCampaignPrompt;
using LiberationFleet.Server.Application.Features.Donations.Commands.CreateDonationCheckout;
using LiberationFleet.Server.Application.Features.Donations.Commands.HandleStripeDonationWebhook;
using LiberationFleet.Server.Application.Features.Donations.Queries.GetDonationCampaignPrompt;
using LiberationFleet.Server.Application.Features.Donations.Queries.GetMyDonationSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/donations")]
public class DonationsController(IMediator mediator) : ControllerBase
{
    public class CreateCheckoutBody
    {
        public long AmountCents { get; set; }
    }

    [Authorize]
    [HttpGet("campaign-prompt")]
    public async Task<IActionResult> GetCampaignPrompt([FromQuery] string variant = "crew")
    {
        var result = await mediator.Send(new GetDonationCampaignPromptQuery(variant));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("campaign-prompt/ack")]
    public async Task<IActionResult> AcknowledgeCampaignPrompt()
    {
        var result = await mediator.Send(new AcknowledgeDonationCampaignPromptCommand());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await mediator.Send(new GetMyDonationSummaryQuery());
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutBody body)
    {
        var result = await mediator.Send(new CreateDonationCheckoutCommand(body.AmountCents));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Stripe webhook. Configure endpoint to send checkout.session.completed (and async success) events.
    /// Card/payment details are never stored here — only session ids and completed amounts.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();
        var result = await mediator.Send(new HandleStripeDonationWebhookCommand(json, signature));
        if (!result.Success && result.Message.StartsWith("Invalid signature", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
