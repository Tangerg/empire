using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Core.State;

/// <summary>A detached read surface over the engine's already-composed catalog.</summary>
public sealed class ReadOnlyContent
{
    private readonly JsonArray packs;
    private readonly StateContent content;
    public ReadOnlyContent(JsonArray contentPacks)
    {
        packs = EcmaJson.Normalize(contentPacks)!.AsArray();
        content = new StateContent(packs);
    }
    internal StateContent Source => content;
    internal JsonArray CopyPacks() => (JsonArray)packs.DeepClone();
    public IReadOnlyList<string> Families => Array.AsReadOnly(content.FamilyIds.ToArray());
    public JsonElement? Definition(string family, string id) => content.TryGet(family, id) is { } entry
        ? EcmaJson.ToElement(entry) : null;
    public IReadOnlyList<JsonElement> Definitions(string family) => Array.AsReadOnly(content.All(family)
        .Select(entry => EcmaJson.ToElement(entry)).ToArray());
}
