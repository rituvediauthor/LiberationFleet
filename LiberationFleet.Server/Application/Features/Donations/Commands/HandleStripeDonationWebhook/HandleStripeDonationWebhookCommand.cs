using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LiberationFleet.Server.Application.Features.Donations.Commands.HandleStripeDonationWebhook;

public record HandleStripeDonationWebhookCommand(string Json, string StripeSignatureHeader)
    : IRequest<HandleStripeDonationWebhookResponse>;

public class HandleStripeDonationWebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class HandleStripeDonationWebhookCommandHandler(
    IAppDonationRepository donationRepository,
    IUnitOfWork unitOfWork,
    IOptions<StripeDonationOptions> stripeOptions) : IRequestHandler<HandleStripeDonationWebhookCommand, HandleStripeDonationWebhookResponse>
{
    public async Task<HandleStripeDonationWebhookResponse> Handle(
        HandleStripeDonationWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var options = stripeOptions.Value;
        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            return new HandleStripeDonationWebhookResponse { Success = false, Message = "Webhook not configured." };
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                request.Json,
                request.StripeSignatureHeader,
                options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            return new HandleStripeDonationWebhookResponse
            {
                Success = false,
                Message = $"Invalid signature: {ex.Message}"
            };
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted
            || stripeEvent.Type == EventTypes.CheckoutSessionAsyncPaymentSucceeded)
        {
            if (stripeEvent.Data.Object is not Session session)
            {
                return new HandleStripeDonationWebhookResponse { Success = true, Message = "Ignored." };
            }

            await CompleteSessionAsync(session, cancellationToken);
        }

        return new HandleStripeDonationWebhookResponse { Success = true, Message = "OK" };
    }

    private async Task CompleteSessionAsync(Session session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(session.Id))
        {
            return;
        }

        var donation = await donationRepository.GetByStripeCheckoutSessionIdAsync(session.Id, cancellationToken);
        if (donation is null)
        {
            // Session may have been created before we persisted the id — try metadata.
            if (session.Metadata != null
                && session.Metadata.TryGetValue("donationId", out var idText)
                && int.TryParse(idText, out var donationId))
            {
                // Fall through via creating if missing is risky; leave noop for unknown.
                _ = donationId;
            }

            return;
        }

        if (donation.Status == "completed")
        {
            return;
        }

        donation.Status = "completed";
        donation.CompletedAt = DateTime.UtcNow;
        donation.StripePaymentIntentId = session.PaymentIntentId;
        if (session.AmountTotal is long amount && amount > 0)
        {
            donation.AmountCents = amount;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
