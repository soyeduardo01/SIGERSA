using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Infrastructure.Email;

public sealed partial class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendPasswordRecoveryOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            LogSmtpDisabled(logger);
            throw new EmailDeliveryException(
                "El correo de recuperación no está configurado. Contacte al administrador del sistema.");
        }
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
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? null
                : new NetworkCredential(_options.Username, _options.Password)
        };
        try
        {
            await client.SendMailAsync(message, cancellationToken);
            LogOtpSent(logger);
        }
        catch (SmtpException exception)
        {
            LogSmtpFailure(logger, exception, exception.StatusCode);
            throw new EmailDeliveryException(
                "No fue posible enviar el código de recuperación. Inténtelo nuevamente más tarde.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            LogSmtpFailure(logger, exception, SmtpStatusCode.GeneralFailure);
            throw new EmailDeliveryException(
                "El servicio de correo no está disponible. Contacte al administrador del sistema.",
                exception);
        }
    }

    [LoggerMessage(EventId = 2100, Level = LogLevel.Error, Message = "El envío SMTP está deshabilitado.")]
    private static partial void LogSmtpDisabled(ILogger logger);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "OTP de recuperación enviado correctamente.")]
    private static partial void LogOtpSent(ILogger logger);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Falló el envío SMTP. Estado: {StatusCode}.")]
    private static partial void LogSmtpFailure(ILogger logger, Exception exception, SmtpStatusCode statusCode);
}
