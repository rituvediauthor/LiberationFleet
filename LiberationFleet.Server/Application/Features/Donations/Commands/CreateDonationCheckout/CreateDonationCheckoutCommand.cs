using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LiberationFleet.Server.Application.Features.Donations.Commands.CreateDonationCheckout;

public record CreateDonationCheckoutCommand(long AmountCents) : IRequest<CreateDonationCheckoutResponse>;

public class CreateDonationCheckoutResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
}

public class CreateDonationCheckoutCommandHandler(
    ICurrentUserService currentUser,
    IAppDonationRepository donationRepository,
    IUnitOfWork unitOfWork,
    IOptions<StripeDonationOptions> stripeOptions) : IRequestHandler<CreateDonationCheckoutCommand, CreateDonationCheckoutResponse>
{
    private static readonly HashSet<long> PresetAmounts = [500, 1000, 2500, 5000, 10000];

    public async Task<CreateDonationCheckoutResponse> Handle(
        CreateDonationCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var options = stripeOptions.Value;
        if (!options.IsConfigured)
        {
            return Fail("Donations are not configured yet. Please try again later.");
        }

        if (request.AmountCents < 100 || request.AmountCents > 500_000)
        {
            return Fail("Choose an amount between $1 and $5,000.");
        }

        // Allow presets or any whole-dollar custom amount (cents % 100 == 0).
        if (!PresetAmounts.Contains(request.AmountCents) && request.AmountCents % 100 != 0)
        {
            return Fail("Custom amounts must be whole dollars.");
        }

        StripeConfiguration.ApiKey = options.SecretKey;
        var baseUrl = options.PublicAppBaseUrl.TrimEnd('/');
        var userId = currentUser.UserId.Value;

        var donation = new AppDonation
        {
            UserId = userId,
            AmountCents = request.AmountCents,
            Currency = "usd",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        await donationRepository.AddAsync(donation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{baseUrl}/app/donate?success=1&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/app/donate?canceled=1",
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["donationId"] = donation.Id.ToString(),
                ["purpose"] = "liberation_fleet_app"
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = request.AmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Liberation Fleet donation",
                            Description = "Support development and hosting of the Liberation Fleet app. Not a mutual-aid gift to a crewmate."
                        }
                    }
                }
            ]
        }, cancellationToken: cancellationToken);

        donation.StripeCheckoutSessionId = session.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDonationCheckoutResponse
        {
            Success = true,
            Message = "Checkout created.",
            CheckoutUrl = session.Url
        };
    }

    private static CreateDonationCheckoutResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
