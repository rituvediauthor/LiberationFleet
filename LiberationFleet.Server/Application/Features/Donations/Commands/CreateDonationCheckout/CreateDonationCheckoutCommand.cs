using System.Net.Mail;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LiberationFleet.Server.Application.Features.Donations.Commands.CreateDonationCheckout;

public record CreateDonationCheckoutCommand(long AmountCents, string? ReceiptEmail)
    : IRequest<CreateDonationCheckoutResponse>;

public class CreateDonationCheckoutResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
}

public class CreateDonationCheckoutCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IAppDonationRepository donationRepository,
    IUnitOfWork unitOfWork,
    IOptions<StripeDonationOptions> stripeOptions) : IRequestHandler<CreateDonationCheckoutCommand, CreateDonationCheckoutResponse>
{
    private static readonly HashSet<long> PresetAmounts = [500, 1000, 2500, 5000, 10000];

    public async Task<CreateDonationCheckoutResponse> Handle(
        CreateDonationCheckoutCommand request,
        CancellationToken cancellationToken)
    {
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

        string receiptEmail;
        int? userId = currentUser.UserId;
        User? user = null;

        if (userId.HasValue)
        {
            user = await userRepository.GetByIdAsync(userId.Value, cancellationToken);
            if (user is null)
            {
                return Fail("Unauthorized.");
            }

            receiptEmail = !string.IsNullOrWhiteSpace(request.ReceiptEmail)
                ? request.ReceiptEmail.Trim()
                : user.Email?.Trim() ?? string.Empty;
        }
        else
        {
            receiptEmail = request.ReceiptEmail?.Trim() ?? string.Empty;
        }

        if (!TryNormalizeEmail(receiptEmail, out var normalizedEmail))
        {
            return Fail(userId.HasValue
                ? "Your account needs a valid email, or enter one for the donation receipt."
                : "Enter a valid email address for your donation receipt.");
        }

        StripeConfiguration.ApiKey = options.SecretKey;
        var baseUrl = options.PublicAppBaseUrl.TrimEnd('/');

        var donation = new AppDonation
        {
            UserId = userId,
            ReceiptEmail = normalizedEmail,
            AmountCents = request.AmountCents,
            Currency = "usd",
            Status = AppDonationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await donationRepository.AddAsync(donation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var sessionService = new SessionService();
        Session session;
        try
        {
            var metadata = new Dictionary<string, string>
            {
                ["donationId"] = donation.Id.ToString(),
                ["purpose"] = "liberation_fleet_app"
            };
            if (userId.HasValue)
            {
                metadata["userId"] = userId.Value.ToString();
            }

            var sessionOptions = new SessionCreateOptions
            {
                Mode = "payment",
                SubmitType = "donate",
                SuccessUrl = $"{baseUrl}/app/donate?success=1&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{baseUrl}/app/donate?canceled=1",
                CustomerEmail = normalizedEmail,
                ClientReferenceId = userId?.ToString() ?? $"guest-{donation.Id}",
                Metadata = metadata,
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
                                Description = "Support development and hosting of the Liberation Fleet app. Not a mutual-aid gift to a crewmate.",
                                TaxCode = "txcd_00000000"
                            }
                        }
                    }
                ]
            };
            sessionOptions.AddExtraParam("managed_payments[enabled]", false);

            session = await sessionService.CreateAsync(sessionOptions, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            donation.Status = AppDonationStatus.Failed;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Fail(ex.Message);
        }

        donation.StripeCheckoutSessionId = session.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDonationCheckoutResponse
        {
            Success = true,
            Message = "Checkout created.",
            CheckoutUrl = session.Url
        };
    }

    private static bool TryNormalizeEmail(string email, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(email.Trim());
            if (string.IsNullOrWhiteSpace(parsed.Address) || !parsed.Address.Contains('@'))
            {
                return false;
            }

            normalized = parsed.Address;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CreateDonationCheckoutResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
