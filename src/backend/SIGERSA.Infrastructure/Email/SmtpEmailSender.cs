using System.Net;
using System.Net.Mail;
using System.Net.Mime;
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
        await SendOtpAsync(
            recipient, recipientName, otp, expiresAt,
            "Código de recuperación de SIGERSA",
            "Código de recuperación",
            "Hemos recibido una solicitud para restablecer tu contraseña.",
            "Restablecer contraseña", _options.PasswordRecoveryUrl, cancellationToken);
    }

    public async Task SendTwoFactorOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await SendOtpAsync(
            recipient, recipientName, otp, expiresAt,
            "Código de acceso de SIGERSA",
            "Código de verificación",
            "Usa este código para completar el inicio de sesión.",
            "Volver a SIGERSA", _options.ApplicationUrl, cancellationToken);
    }

    private async Task SendOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        string subject,
        string heading,
        string introduction,
        string buttonText,
        string applicationUrl,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            LogSmtpDisabled(logger);
            throw new EmailDeliveryException(
                "El correo de recuperación no está configurado. Contacte al administrador del sistema.");
        }
        var plainTextBody = OtpEmailTemplate.BuildPlainText(
            recipientName, otp, expiresAt, introduction);
        var htmlBody = OtpEmailTemplate.BuildHtml(
            recipientName, otp, expiresAt, heading, introduction, buttonText,
            applicationUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = plainTextBody
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
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
                "No fue posible enviar el código. Inténtelo nuevamente más tarde.",
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
