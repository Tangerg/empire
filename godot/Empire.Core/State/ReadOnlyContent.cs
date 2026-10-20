using System.Text.Json;
using Empire.Core.Determinism;

namespace Empire.Core.State;

/// <summary>A detached read surface over the engine's already-composed catalog.</summary>
public sealed class ReadOnlyContent
{
    private readonly StateContent content;
    internal ReadOnlyContent(StateContent content) => this.content = content;
    public IReadOnlyList<string> Families => Array.AsReadOnly(content.FamilyIds.ToArray());
    public JsonElement? Definition(string family, string id) => content.TryGet(family, id) is { } entry
        ? EcmaJson.ToElement(entry) : null;
    public IReadOnlyList<JsonElement> Definitions(string family) => Array.AsReadOnly(content.All(family)
        .Select(entry => EcmaJson.ToElement(entry)).ToArray());
}
