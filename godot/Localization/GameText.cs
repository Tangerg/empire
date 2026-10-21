using System.Globalization;
using System.Text;

namespace Empire.Localization;

public sealed class LocalizationException : InvalidOperationException
{
    public LocalizationException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Presentation-only formatting over an explicitly supplied, immutable catalog lookup.</summary>
public sealed class GameText
{
    private readonly Func<string, string?> lookup;
    private readonly CultureInfo culture;
    private readonly Dictionary<string, CompositeFormat> formats = new(StringComparer.Ordinal);

    public GameText(Func<string, string?> lookup, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(culture);
        this.lookup = lookup;
        this.culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
    }

    public string Format(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var format = RequireFormat(key);
        if (arguments.Length != format.MinimumArgumentCount)
            throw new LocalizationException($"Message '{key}' requires {format.MinimumArgumentCount} arguments; received {arguments.Length}.");
        try
        {
            return string.Format(culture, format, arguments);
        }
        catch (FormatException error) { throw new LocalizationException($"Message '{key}' has an incompatible argument format.", error); }
    }

    public void ValidateKeys(IEnumerable<string> requiredKeys)
    {
        ArgumentNullException.ThrowIfNull(requiredKeys);
        foreach (var key in requiredKeys)
            RequireFormat(key);
    }

    private CompositeFormat RequireFormat(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new LocalizationException("A message key must not be empty.");
        if (formats.TryGetValue(key, out var cached))
            return cached;
        var value = lookup(key) ?? throw new LocalizationException($"Missing localization key '{key}'.");
        if (string.IsNullOrWhiteSpace(value))
            throw new LocalizationException($"Localization key '{key}' has an empty value.");
        CompositeFormat format;
        try
        {
            format = CompositeFormat.Parse(value);
        }
        catch (FormatException error) { throw new LocalizationException($"Localization key '{key}' has a malformed composite format.", error); }
        ValidateArgumentSet(key, format);
        formats.Add(key, format);
        return format;
    }

    private static void ValidateArgumentSet(string key, CompositeFormat format)
    {
        // Ask the BCL formatter which arguments it actually consumes, rather than maintaining a second format parser.
        var probe = new ArgumentProbe();
        var arguments = Enumerable.Range(0, format.MinimumArgumentCount).Select(index => (object?)index).ToArray();
        _ = string.Format(probe, format, arguments);
        if (probe.Used.Count != format.MinimumArgumentCount)
            throw new LocalizationException($"Localization key '{key}' has a gap in its argument indices.");
    }

    private sealed class ArgumentProbe : IFormatProvider, ICustomFormatter
    {
        public HashSet<int> Used { get; } = new();
        public object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : null;
        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            Used.Add((int)arg!);
            return "";
        }
    }
}
