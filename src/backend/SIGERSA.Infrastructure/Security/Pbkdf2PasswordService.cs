using System.Security.Cryptography;
using SIGERSA.Domain.Security;

namespace SIGERSA.Infrastructure.Security;

public sealed class BcryptPasswordService : IPasswordService
{
    private const string Prefix = "sigersa-pbkdf2-sha256";

    public string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return BCrypt.Net.BCrypt.HashPassword(secret, workFactor: 12);
    }

    public bool Verify(string hash, string secret)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrEmpty(secret)) return false;
        if (hash.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(secret, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        // Compatibilidad de lectura para cuentas creadas antes de la migración a bcrypt.
        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix ||
            !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var iterations) ||
            iterations < 100_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
