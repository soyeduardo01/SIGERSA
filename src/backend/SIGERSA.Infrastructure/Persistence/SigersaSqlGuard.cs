using System.Text.RegularExpressions;

namespace SIGERSA.Infrastructure.Persistence;

public static partial class SigersaSqlGuard
{
    public const string Schema = "\"SIGERSA\"";

    public const string AuditColumns =
        "creado_en, creado_por, modificado_en, modificado_por, version_fila";

    public static string EnsureQualified(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        foreach (Match match in DataObjectRegex().Matches(sql))
        {
            var objectName = match.Groups[1].Value;
            if (!objectName.StartsWith($"{Schema}.\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Toda sentencia Dapper debe calificar sus objetos con el esquema {Schema}.");
            }
        }

        return sql;
    }

    public static string EnsureAuditedConcurrencyUpdate(string sql)
    {
        EnsureQualified(sql);

        string[] requiredFragments =
        [
            "modificado_en",
            "modificado_por",
            "version_fila = version_fila + 1",
            "version_fila = @VersionFila"
        ];

        if (requiredFragments.Any(fragment =>
                !sql.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "La actualización debe registrar auditoría y comprobar version_fila.");
        }

        return sql;
    }

    [GeneratedRegex(
        @"\b(?:FROM|JOIN|UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+([^\s;(]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataObjectRegex();
}
