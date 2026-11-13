using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

/// <summary>Read-only instructions for an existing one-shot zone trigger, never an objective or state owner.</summary>
public sealed record ScenarioHintProjection(string Trigger, string Title, string Instruction, string Completed,
    bool IsCompleted, IReadOnlyList<Coord> Cells);

public sealed partial class BattleTextDocument
{
    private sealed record ScenarioHintEntry(string Trigger, string Zone, int Owner,
        TextEntry Title, TextEntry Instruction, TextEntry Completed);

    private static IReadOnlyList<ScenarioHintEntry> ParseScenarioHints(JsonNode? raw, JsonObject level)
    {
        var result = new List<ScenarioHintEntry>();
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var scenario = Object(level["scenario"], "scenario hint source");
        var triggers = Array(scenario["triggers"], "scenario hint triggers").OfType<JsonObject>().ToArray();
        var zones = Array(scenario["zones"], "scenario hint zones").OfType<JsonObject>().ToArray();
        foreach (var item in Array(raw, "scenario hints"))
        {
            var fields = Fields(item, "scenario hint", "trigger", "zone", "owner", "title", "instruction", "completed");
            var triggerId = RequiredString(fields["trigger"], "scenario hint trigger");
            var zoneId = RequiredString(fields["zone"], "scenario hint zone");
            var owner = Integer(fields["owner"], "scenario hint owner");
            if (!declared.Add(triggerId))
                throw new LocalizationException($"Duplicate scenario hint trigger '{triggerId}'.");
            var trigger = triggers.SingleOrDefault(value => RequiredString(value["id"], "trigger id") == triggerId)
                ?? throw new LocalizationException($"Unknown scenario hint trigger '{triggerId}'.");
            var condition = Object(trigger["condition"], "scenario hint condition");
            if (RequiredString(trigger["timing"], "trigger timing") != "afterAction"
                || RequiredString(condition["type"], "trigger condition") != "unitInZone"
                || RequiredString(condition["zone"], "trigger zone") != zoneId
                || Integer(condition["owner"], "trigger owner") != owner
                || trigger["repeat"] is not null)
                throw new LocalizationException("Scenario hints require a matching one-shot afterAction unitInZone trigger.");
            var zone = zones.SingleOrDefault(value => RequiredString(value["id"], "zone id") == zoneId)
                ?? throw new LocalizationException($"Unknown scenario hint zone '{zoneId}'.");
            if (Array(zone["cells"], "hint zone cells").Count == 0
                || !Array(level["players"], "hint players").OfType<JsonObject>().Any(player => Integer(player["id"], "player id") == owner))
                throw new LocalizationException("Scenario hints require a nonempty zone and an existing owner.");
            result.Add(new(triggerId, zoneId, owner,
                Entry(fields["title"], "hint title", allowGenerated: false),
                Entry(fields["instruction"], "hint instruction", allowGenerated: false),
                Entry(fields["completed"], "hint completed", allowGenerated: false)));
        }
        return result.AsReadOnly();
    }

    internal IReadOnlyList<ScenarioHintProjection> ScenarioHints(GameText text, JsonObject state, int viewer)
    {
        var hints = Level(RequiredString(state["levelId"], "level id")).ScenarioHints.Where(hint => hint.Owner == viewer).ToArray();
        if (hints.Length == 0)
            return System.Array.Empty<ScenarioHintProjection>();
        var scenario = Object(state["scenario"], "scenario hint state");
        var zones = Object(scenario["zones"], "scenario hint runtime zones");
        var fired = Array(scenario["firedTriggerIds"], "scenario fired triggers")
            .Select(value => RequiredString(value, "fired trigger")).ToHashSet(StringComparer.Ordinal);
        return System.Array.AsReadOnly(hints.Select(hint => new ScenarioHintProjection(hint.Trigger,
            hint.Title.Resolve(text)!, hint.Instruction.Resolve(text)!, hint.Completed.Resolve(text)!, fired.Contains(hint.Trigger),
            System.Array.AsReadOnly(Array(zones[hint.Zone], "runtime hint zone").OfType<JsonObject>()
                .Select(cell => new Coord(Integer(cell["x"], "zone x"), Integer(cell["y"], "zone y"))).Distinct().ToArray()))).ToArray());
    }
}
