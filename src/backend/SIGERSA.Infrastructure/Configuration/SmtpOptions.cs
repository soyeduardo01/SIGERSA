namespace SIGERSA.Infrastructure.Configuration;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "SIGERSA";
    public string ApplicationUrl { get; init; } = "http://127.0.0.1:5173/";
    public string PasswordRecoveryUrl { get; init; } = "http://127.0.0.1:5173/recuperar-clave.html";
}
