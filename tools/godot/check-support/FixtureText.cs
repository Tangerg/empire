using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Empire.Localization;

namespace Empire.Checks;

/// <summary>Loads the explicit JSON projection emitted by the catalog validator for pure checks.</summary>
internal static class FixtureText
{
    internal static GameText Load(string catalogPath)
    {
        var messages = (JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath))
            ?? throw new InvalidOperationException("Catalog fixture must be an object.")).ToFrozenDictionary(StringComparer.Ordinal);
        return new GameText(key => messages.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
    }
}
