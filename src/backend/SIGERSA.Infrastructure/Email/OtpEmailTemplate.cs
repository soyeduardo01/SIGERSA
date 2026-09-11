using System.Net;
using System.Globalization;
using System.Text;

namespace SIGERSA.Infrastructure.Email;

public static class OtpEmailTemplate
{
    public static string BuildHtml(
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        string heading,
        string introduction,
        string buttonText,
        string applicationUrl)
    {
        var name = WebUtility.HtmlEncode(recipientName);
        var code = WebUtility.HtmlEncode(otp);
        var title = WebUtility.HtmlEncode(heading);
        var message = WebUtility.HtmlEncode(introduction);
        var button = WebUtility.HtmlEncode(buttonText);
        var url = WebUtility.HtmlEncode(applicationUrl);
        var expiration = WebUtility.HtmlEncode(expiresAt.ToString(
            "dd/MM/yyyy 'a las' hh:mm tt 'UTC'", CultureInfo.GetCultureInfo("es-DO")));

        return $$"""
            <!doctype html>
            <html lang="es">
            <body style="margin:0;padding:0;background:#eef6f2;font-family:Arial,Helvetica,sans-serif;color:#173d34;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef6f2;padding:28px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="width:100%;max-width:620px;background:#ffffff;border-radius:22px;overflow:hidden;box-shadow:0 18px 45px rgba(18,86,64,.14);">
                    <tr><td style="padding:30px 42px;background:#07523e;color:#ffffff;">
                      <table role="presentation" width="100%"><tr>
                        <td style="font-size:34px;font-weight:800;letter-spacing:.5px;">SIGERSA<div style="margin-top:7px;font-size:13px;line-height:19px;font-weight:400;color:#d8eee6;">Sistema Integral de Gestión de Riesgo<br>y Seguridad Alimentaria</div></td>
                        <td align="right" style="font-size:15px;line-height:21px;color:#e7f5ef;">Alimentos seguros,<br>una sociedad más sana<div style="width:44px;height:3px;margin-top:10px;margin-left:auto;background:#8bd36d;border-radius:3px;"></div></td>
                      </tr></table>
                    </td></tr>
                    <tr><td style="padding:30px 46px 16px;text-align:center;">
                      <div style="display:inline-block;width:70px;height:70px;line-height:70px;border-radius:50%;background:#e8f6ef;color:#087452;font-size:34px;">&#128274;</div>
                      <h1 style="margin:18px 0 12px;font-size:34px;line-height:40px;color:#07523e;">{{title}}</h1>
                      <p style="margin:0;font-size:18px;line-height:28px;color:#536b65;">Hola, <strong style="color:#173d34;">{{name}}</strong></p>
                      <p style="margin:6px 0 22px;font-size:16px;line-height:25px;color:#657a75;">{{message}}</p>
                      <div style="padding:24px 16px;border:1px dashed #79c696;border-radius:14px;background:#f2fbf6;font-size:46px;font-weight:800;letter-spacing:10px;color:#07523e;">{{code}}</div>
                      <p style="margin:18px 0 24px;font-size:14px;color:#657a75;">&#128339; Este código expira el <strong style="color:#173d34;">{{expiration}}</strong>.</p>
                      <a href="{{url}}" style="display:block;margin:0 auto;max-width:390px;padding:15px 24px;border-radius:12px;background:#087452;color:#ffffff;text-decoration:none;font-size:17px;font-weight:700;">{{button}} &nbsp;&#8594;</a>
                    </td></tr>
                    <tr><td style="padding:14px 46px 26px;">
                      <div style="padding:18px 20px;border-radius:14px;background:#eff7f4;color:#41675d;font-size:14px;line-height:22px;">&#128737; Si no solicitaste este cambio, puedes ignorar este mensaje con seguridad.</div>
                      <p style="margin:20px 0 0;padding-top:18px;border-top:1px solid #dce6e2;text-align:center;font-size:11px;color:#81918c;">Mensaje automático de SIGERSA. No respondas a este correo.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string BuildPlainText(
        string recipientName, string otp, DateTimeOffset expiresAt, string introduction)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SIGERSA — Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria");
        builder.AppendLine();
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Hola, {0}", recipientName));
        builder.AppendLine(introduction);
        builder.AppendLine();
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Código: {0}", otp));
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Expira: {0:O}", expiresAt));
        builder.AppendLine();
        builder.Append("Si no solicitaste este cambio, ignora este mensaje.");
        return builder.ToString();
    }
}
