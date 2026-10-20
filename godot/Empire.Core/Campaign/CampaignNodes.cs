using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

public sealed record CampaignNodeAdvance(Action<JsonArray?> Apply, Action<string> MoveTo, Action<string> Settle, Action<string> Needs);
public sealed class CampaignNodeInspection
{
    public JsonObject Definition { get; }
    private readonly HashSet<string> nodes;
    private readonly HashSet<string> roster;
    public CampaignNodeInspection(JsonObject definition)
    {
        Definition = definition;
        nodes = Objects(definition["nodes"], "nodes").Select(node => Text(node, "id")).ToHashSet(StringComparer.Ordinal);
        roster = Objects(definition["initial"]?["roster"], "roster").Select(unit => Text(unit, "id")).ToHashSet(StringComparer.Ordinal);
    }
    public bool HasNode(string id) => nodes.Contains(id);
    public bool HasRosterUnit(string id) => roster.Contains(id);
    public void Reject(string message) => throw new CampaignInvariantException(message);
}
public sealed record CampaignNodeHandler(string Kind, Action<CampaignNodeAdvance, JsonObject> Advance,
    Action<CampaignNodeInspection, JsonObject>? Validate = null);
public sealed class CampaignNodeRegistry : CampaignRegistry<CampaignNodeHandler>
{
    public CampaignNodeRegistry() : base("campaign node handler") { }
    protected override string KeyOf(CampaignNodeHandler entry) => entry.Kind;
    public void Advance(CampaignNodeAdvance context, JsonObject node) => Get(Text(node, "type")).Advance(context, node);
    public void Validate(CampaignNodeInspection inspection, JsonObject node)
    {
        var handler = TryGet(Text(node, "type"));
        if (handler is null) throw new CampaignInvariantException($"campaign node \"{Text(node, "id")}\" has unknown type \"{Text(node, "type")}\"");
        handler.Validate?.Invoke(inspection, node);
    }
    public CampaignNodeRegistry Clone() => CopyInto(new CampaignNodeRegistry());
    public static CampaignNodeRegistry Core()
    {
        var registry = new CampaignNodeRegistry();
        registry.Register(new("story", (context, node) => { context.Apply(node["effects"] as JsonArray); context.MoveTo(Text(node, "next")); },
            (inspection, node) => RequireNext(inspection, Text(node, "id"), Text(node, "next"))));
        registry.Register(new("choice", (context, _) => context.Needs("choose()"), (inspection, node) =>
        {
            var choices = Array(node["choices"], "choices");
            if (choices.Count == 0) inspection.Reject($"choice node \"{Text(node, "id")}\" has no choices");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in Objects(choices, "choices"))
            {
                var id = Text(choice, "id");
                if (!seen.Add(id)) inspection.Reject($"duplicate choice \"{Text(node, "id")}:{id}\"");
                RequireNext(inspection, $"{Text(node, "id")}:{id}", Text(choice, "next"));
            }
        }));
        registry.Register(new("battle", (context, _) => context.Needs("beginBattle()"), (inspection, node) =>
        {
            var id = Text(node, "id");
            if (string.IsNullOrWhiteSpace(Text(node, "level"))) inspection.Reject($"battle node \"{id}\" has no level");
            foreach (var pair in Object(node["next"], "next"))
                if (pair.Value is not null && !string.IsNullOrEmpty(Text(pair.Value, "next"))) RequireNext(inspection, id, Text(pair.Value, "next"));
            var roster = new HashSet<string>(StringComparer.Ordinal); var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in Objects(node["rosterBindings"], "rosterBindings"))
            {
                var unit = Text(binding, "campaignUnit"); var key = Text(binding, "levelUnitKey");
                if (!roster.Add(unit) || !keys.Add(key)) inspection.Reject($"battle node \"{id}\" has duplicate roster binding");
                if (!inspection.HasRosterUnit(unit)) inspection.Reject($"battle node \"{id}\" binds unknown roster unit \"{unit}\"");
            }
        }));
        registry.Register(new("ending", (context, node) => { context.Apply(node["effects"] as JsonArray); context.Settle(Text(node, "outcome")); }));
        return registry;
    }
    private static void RequireNext(CampaignNodeInspection inspection, string source, string next)
    { if (!inspection.HasNode(next)) inspection.Reject($"{source} references unknown campaign node \"{next}\""); }
}
