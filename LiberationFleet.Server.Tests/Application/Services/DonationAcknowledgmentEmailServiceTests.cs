using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Services;

public class DonationAcknowledgmentEmailServiceTests
{
    [Fact]
    public async Task TrySendAsync_SendsAcknowledgmentWithRequiredElements()
    {
        string? capturedBody = null;
        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        var sut = new DonationAcknowledgmentEmailService(
            email.Object,
            Options.Create(new OrganizationOptions
            {
                LegalName = "Liberation Fleet Co.",
                Ein = "12-3456789",
                MailingAddress = "123 Main St, Indianapolis, IN 46204",
                TaxExemptStatement = "Tax-exempt nonprofit statement."
            }),
            NullLogger<DonationAcknowledgmentEmailService>.Instance);

        var donation = new AppDonation
        {
            Id = 42,
            AmountCents = 2500,
            Currency = "usd",
            CompletedAt = new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc),
            StripePaymentIntentId = "pi_test_123"
        };
        var user = new User { Id = 1, Email = "donor@example.com", Username = "donor" };

        var sent = await sut.TrySendAsync(donation, user);

        Assert.True(sent);
        Assert.NotNull(capturedBody);
        Assert.Contains("Liberation Fleet Co.", capturedBody);
        Assert.Contains("EIN: 12-3456789", capturedBody);
        Assert.Contains("123 Main St, Indianapolis, IN 46204", capturedBody);
        Assert.Contains("$25.00", capturedBody);
        Assert.Contains("Donation reference: #42", capturedBody);
        Assert.Contains("pi_test_123", capturedBody);
        Assert.Contains("No goods or services were provided", capturedBody);
        Assert.Contains("Tax-exempt nonprofit statement.", capturedBody);
        email.Verify(e => e.SendAsync(
            "donor@example.com",
            It.Is<string>(s => s.Contains("Donation acknowledgment")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrySendAsync_ReturnsFalseWhenUserHasNoEmail()
    {
        var email = new Mock<IEmailSender>();
        var sut = new DonationAcknowledgmentEmailService(
            email.Object,
            Options.Create(new OrganizationOptions()),
            NullLogger<DonationAcknowledgmentEmailService>.Instance);

        var sent = await sut.TrySendAsync(
            new AppDonation { Id = 1, AmountCents = 1000 },
            new User { Id = 1, Email = "", Username = "x" });

        Assert.False(sent);
        email.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
