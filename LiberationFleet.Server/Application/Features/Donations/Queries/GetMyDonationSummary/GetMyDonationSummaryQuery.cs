using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Features.Donations.Queries.GetMyDonationSummary;

public record GetMyDonationSummaryQuery : IRequest<DonationSummaryResponse>;

public class DonationSummaryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int CurrentTaxYear { get; set; }
    public int PreviousTaxYear { get; set; }
    public decimal CurrentTaxYearTotalUsd { get; set; }
    public decimal PreviousTaxYearTotalUsd { get; set; }
    public bool DonationsEnabled { get; set; }
}

public class GetMyDonationSummaryQueryHandler(
    ICurrentUserService currentUser,
    IAppDonationRepository donationRepository,
    IOptions<StripeDonationOptions> stripeOptions) : IRequestHandler<GetMyDonationSummaryQuery, DonationSummaryResponse>
{
    public async Task<DonationSummaryResponse> Handle(
        GetMyDonationSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new DonationSummaryResponse { Success = false, Message = "Unauthorized." };
        }

        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var previousYear = currentYear - 1;
        var currentStart = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousStart = new DateTime(previousYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextStart = new DateTime(currentYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var userId = currentUser.UserId.Value;
        var currentTotal = await donationRepository.SumCompletedUsdForUserInRangeAsync(
            userId, currentStart, nextStart, cancellationToken);
        var previousTotal = await donationRepository.SumCompletedUsdForUserInRangeAsync(
            userId, previousStart, currentStart, cancellationToken);

        return new DonationSummaryResponse
        {
            Success = true,
            Message = "OK",
            CurrentTaxYear = currentYear,
            PreviousTaxYear = previousYear,
            CurrentTaxYearTotalUsd = currentTotal,
            PreviousTaxYearTotalUsd = previousTotal,
            DonationsEnabled = stripeOptions.Value.IsConfigured
        };
    }
}
