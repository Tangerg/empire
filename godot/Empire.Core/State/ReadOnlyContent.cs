using System.Text.Json;
using System.Text.Json.Nodes;

namespace Empire.Core.State;

/// <summary>
/// The engine's canonical, normalized catalog. Input packs are detached and all
/// public definitions are immutable, including nested collections. Independent
/// engines may safely share one catalog.
/// </summary>
public sealed class ReadOnlyContent
{
    private readonly StateContent content;
    public ReadOnlyContent(JsonArray contentPacks)
    {
        content = new StateContent(contentPacks);
    }
    internal StateContent Source => content;
    internal JsonArray CopyPacks() => content.CopyPacks();
    public IReadOnlyList<string> Families => Array.AsReadOnly(content.FamilyIds.ToArray());
    public JsonElement? Definition(string family, string id) => content.Find(family, id);
    public IReadOnlyList<JsonElement> Definitions(string family) => Array.AsReadOnly(content.ReadAll(family).ToArray());
}
