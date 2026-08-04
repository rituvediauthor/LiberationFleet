using LiberationFleet.Server.Application.Common.Interfaces;

namespace LiberationFleet.Server.Infrastructure.Email;

/// <summary>
/// Dev-friendly sender that logs the email instead of delivering it (used when SmtpHost is empty).
/// </summary>
public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email (not sent — SmtpHost empty). To: {To}; Subject: {Subject}; Body: {Body}",
            to,
            subject,
            body);
        return Task.CompletedTask;
    }
}
