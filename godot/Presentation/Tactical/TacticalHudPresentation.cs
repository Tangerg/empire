using System.Text.Json.Serialization;

namespace Empire.Presentation.Tactical;

/// <summary>Optional authored chrome only. Tactical rows, intents and camera geometry keep their existing owners.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TacticalHudPresentation
{
    [JsonRequired]
    public int Schema { get; init; } = 1;
    [JsonRequired]
    public bool CompactHeaders
    {
        get; init;
    }
    public static TacticalHudPresentation Default { get; } = new();

    public static TacticalHudPresentation Resolve(AuthoredScene scene)
    {
        if (scene.HudPresentationPath is not { } path)
            return Default;
        var document = PresentationJson.Load<TacticalHudPresentation>(path);
        if (document.Schema != 1)
            throw new InvalidOperationException("Unsupported tactical HUD presentation schema.");
        return document;
    }
}
