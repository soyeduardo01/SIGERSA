using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendPasswordRecoveryOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) throw new InvalidOperationException("El servicio SMTP no está habilitado.");
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName, Encoding.UTF8),
            Subject = "Código de recuperación de SIGERSA",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = $"Hola {recipientName},\n\nTu código de recuperación es: {otp}\n" +
                   $"Expira a las {expiresAt:O}. Si no solicitaste este cambio, ignora este mensaje."
        };
        message.To.Add(new MailAddress(recipient));
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
