using LiberationFleet.Server.Application.Features.Donations.Commands.AcknowledgeDonationCampaignPrompt;
using LiberationFleet.Server.Application.Features.Donations.Commands.CreateDonationCheckout;
using LiberationFleet.Server.Application.Features.Donations.Commands.HandleStripeDonationWebhook;
using LiberationFleet.Server.Application.Features.Donations.Queries.GetDonationCampaignPrompt;
using LiberationFleet.Server.Application.Features.Donations.Queries.GetMyDonationSummary;
using LiberationFleet.Server.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/donations")]
public class DonationsController(
    IMediator mediator,
    IOptions<StripeDonationOptions> stripeOptions) : ControllerBase
{
    public class CreateCheckoutBody
    {
        public long AmountCents { get; set; }
        /// <summary>Required for guests; optional for signed-in users (defaults to account email).</summary>
        public string? ReceiptEmail { get; set; }
    }

    public class DonationStatusResponse
    {
        public bool DonationsEnabled { get; set; }
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(new DonationStatusResponse { DonationsEnabled = stripeOptions.Value.IsConfigured });

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

    [AllowAnonymous]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutBody body)
    {
        var result = await mediator.Send(new CreateDonationCheckoutCommand(body.AmountCents, body.ReceiptEmail));
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
