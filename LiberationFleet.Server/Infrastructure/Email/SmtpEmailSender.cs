using System.Net;
using System.Net.Mail;
using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? new MailAddress(_options.FromAddress)
            : new MailAddress(_options.FromAddress, _options.FromName);

        using var message = new MailMessage
        {
            From = from,
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        // SmtpClient is obsolete but used here because MailKit is not referenced.
#pragma warning disable CS0618
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword);
        }

        // SmtpClient.SendMailAsync does not accept CancellationToken; honor cancellation before send.
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
#pragma warning restore CS0618

        _logger.LogInformation("Email sent to {To} with subject {Subject}", to, subject);
    }
}
