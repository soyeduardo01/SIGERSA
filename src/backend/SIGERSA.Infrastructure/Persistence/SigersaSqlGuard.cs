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
        var commonTableExpressions = CteRegex().Matches(sql)
            .Select(match => match.Groups[1].Value.Trim('"'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in DataObjectRegex().Matches(sql))
        {
            var prefix = sql.AsSpan(0, match.Index).TrimEnd();
            if (match.Value.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && prefix.EndsWith("DO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var objectName = match.Groups[1].Value;
            if (objectName.Equals("EXCLUDED", StringComparison.OrdinalIgnoreCase)
                && prefix.Contains("ON CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var remainder = sql.AsSpan(match.Groups[1].Index + match.Groups[1].Length).TrimStart();
            var isTableValuedExpression = remainder.StartsWith("(", StringComparison.Ordinal);
            if (!objectName.StartsWith($"{Schema}.\"", StringComparison.Ordinal)
                && !commonTableExpressions.Contains(objectName.Trim('"'))
                && !isTableValuedExpression)
            {
                throw new InvalidOperationException(
                    $"Toda sentencia Dapper debe calificar el objeto '{objectName}' con el esquema {Schema}.");
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
        @"(?<!@)\b(?:FROM|JOIN|UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+(?:ONLY\s+)?((?:""[^""]+""\.)?""[^""]+""|[A-Za-z_][A-Za-z0-9_$]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataObjectRegex();

    [GeneratedRegex(
        @"(?:\bWITH(?:\s+RECURSIVE)?|,)\s*""?([A-Za-z_][A-Za-z0-9_]*)""?\s+AS\s+(?:(?:NOT\s+)?MATERIALIZED\s+)?\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CteRegex();
}
