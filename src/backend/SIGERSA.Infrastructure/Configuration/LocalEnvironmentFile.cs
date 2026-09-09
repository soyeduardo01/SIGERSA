using System.Text.RegularExpressions;

namespace SIGERSA.Infrastructure.Configuration;

public static partial class LocalEnvironmentFile
{
    public static void LoadIfEnabled()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
        var explicitlyEnabled = string.Equals(
            Environment.GetEnvironmentVariable("SIGERSA_LOAD_ENV_FILE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!explicitlyEnabled && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var file = CandidateDirectories()
            .Select(directory => Path.Combine(directory.FullName, ".env.local"))
            .FirstOrDefault(File.Exists);
        if (file is null) return;

        foreach (var rawLine in File.ReadLines(file))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            if (!EnvironmentVariableName().IsMatch(key)) continue;
            if (Environment.GetEnvironmentVariable(key) is not null) continue;
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"')
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static IEnumerable<DirectoryInfo> CandidateDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (seen.Add(directory.FullName)) yield return directory;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableName();
}
