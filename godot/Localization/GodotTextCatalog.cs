using System.Globalization;
using Godot;

namespace Empire.Localization;

/// <summary>The native resource is the catalog owner. Core and pure projections never depend on Godot.</summary>
public static class GodotTextCatalog
{
    public const string ResourcePath = "res://Localization/zh_CN.po";
    public const string Locale = "zh_CN";

    public static GameText LoadSimplifiedChinese()
    {
        var translation = ResourceLoader.Load<Translation>(ResourcePath)
            ?? throw new LocalizationException($"Required translation resource is missing: {ResourcePath}.");
        if (translation.Locale != Locale)
            throw new LocalizationException($"Translation resource locale is '{translation.Locale}', expected '{Locale}'.");
        var keys = translation.GetMessageList();
        if (keys.Length == 0)
            throw new LocalizationException("The native translation catalog is empty.");
        var available = keys.ToHashSet(StringComparer.Ordinal);
        var text = new GameText(key => available.Contains(key) ? translation.GetMessage(key).ToString() : null,
            CultureInfo.GetCultureInfo("zh-CN"));
        text.ValidateKeys(keys);
        return text;
    }
}
