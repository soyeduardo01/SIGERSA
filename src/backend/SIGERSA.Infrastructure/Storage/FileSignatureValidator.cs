namespace SIGERSA.Infrastructure.Storage;

public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures =
        new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46, 0x2D]],
            ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
            ["image/png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
            ["video/mp4"] = [[0x66, 0x74, 0x79, 0x70]]
        };

    public static bool HasValidSignature(string mimeType, ReadOnlySpan<byte> content)
    {
        if (!Signatures.TryGetValue(mimeType, out var signatures))
        {
            return false;
        }

        foreach (var signature in signatures)
        {
            var offset = mimeType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
            if (content.Length >= offset + signature.Length &&
                content.Slice(offset, signature.Length).SequenceEqual(signature))
            {
                return true;
            }
        }

        return false;
    }
}
