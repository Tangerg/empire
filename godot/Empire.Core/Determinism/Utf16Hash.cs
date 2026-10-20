namespace Empire.Core.Determinism;

internal static class Utf16Hash
{
    internal static uint Hash(string value)
    {
        var hash = 0x811c9dc5u;
        // Iterating chars, not Unicode scalars or UTF-8 bytes, matches charCodeAt.
        foreach (var codeUnit in value)
            hash = unchecked((hash ^ codeUnit) * 0x01000193u);
        return hash;
    }
}
