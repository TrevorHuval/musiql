using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MusiQL.Api.Spotify;

public interface IAccessRequestMailer
{
    Task SendAsync(string subject, string body, CancellationToken ct);
}

// Plain-text mail to the operator over SMTP with STARTTLS. The recipient,
// sender and subject never come from user input.
public sealed class SmtpAccessRequestMailer(
    IOptions<AccessRequestOptions> options, ILogger<SmtpAccessRequestMailer> log) : IAccessRequestMailer
{
    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        using var message = new MailMessage(o.FromEmail, o.NotifyEmail, subject, body) { IsBodyHtml = false };
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = true };
        if (!string.IsNullOrEmpty(o.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(o.SmtpUsername, o.SmtpPassword);
        }

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (SmtpException ex)
        {
            log.LogWarning(ex, "Spotify access request email failed to send.");
            throw new AccessRequestDeliveryException();
        }
    }
}
