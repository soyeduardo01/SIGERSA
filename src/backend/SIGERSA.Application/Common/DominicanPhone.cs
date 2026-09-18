namespace SIGERSA.Application.Common;

public static class DominicanPhone
{
    private static readonly string[] AllowedPrefixes = ["809", "829", "849"];

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Any(character => !char.IsDigit(character) && character is not (' ' or '-' or '(' or ')')))
            return false;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 10
            && AllowedPrefixes.Any(prefix => digits.StartsWith(prefix, StringComparison.Ordinal));
    }
}
