using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Validation;

internal readonly record struct PlayerObjectiveReference(double Player, string Id);

/// <summary>The declarations owned by the core payload handlers, shared by level and save validation.</summary>
internal sealed class RulePayloadReferences
{
    internal List<string> Zones { get; } = new();
    internal List<double> Players { get; } = new();
    internal List<string> Structures { get; } = new();
    internal List<string> Composites { get; } = new();
    internal List<PlayerObjectiveReference> Objectives { get; } = new();
    internal List<string> Statuses { get; } = new();
    internal List<string> Terrains { get; } = new();
    internal List<string> Overlays { get; } = new();
    internal List<string> UnitTypes { get; } = new();
    internal List<string> Directives { get; } = new();
    internal List<JsonObject> Cells { get; } = new();
    internal List<JsonObject> Edges { get; } = new();
    internal List<JsonObject> Conditions { get; } = new();
    internal List<string> Faults { get; } = new();

    internal static RulePayloadReferences ForCondition(JsonObject condition)
    {
        var cited = new RulePayloadReferences();
        switch (StateJson.Text(condition, "type"))
        {
            case "turnCycle":
                if (!Whole(condition["every"], 1)) cited.Faults.Add("循环回合条件的间隔必须是正整数");
                break;
            case "currentPlayer": cited.Player(condition["player"]); break;
            case "unitInZone": cited.Zone(condition["zone"]); break;
            case "unitCount":
            case "unitHealth":
            case "markerCount": cited.Selector(condition["selector"]); break;
            case "structure": Add(cited.Structures, condition["id"]); break;
            case "composite": Add(cited.Composites, condition["id"]); break;
            case "objective":
                cited.Player(condition["player"]);
                cited.Objective(condition);
                break;
        }
        return cited;
    }

    internal static RulePayloadReferences ForEffect(JsonObject effect)
    {
        var cited = new RulePayloadReferences();
        switch (StateJson.Text(effect, "type"))
        {
            case "addStatus":
            case "removeStatus":
                Add(cited.Statuses, effect["status"]);
                cited.Selector(effect["selector"]);
                break;
            case "changeUnitOwner":
            case "withdrawUnits":
            case "reviveMarkers":
            case "removeMarkers":
            case "changeUnitResource":
            case "changeMorale":
            case "surrenderUnits": cited.Selector(effect["selector"]); break;
            case "spawnUnits":
                foreach (var source in StateJson.Objects(effect["units"], "spawn units"))
                {
                    Add(cited.UnitTypes, source["unit"]);
                    cited.Player(source["owner"]);
                    cited.Cells.Add(source);
                }
                break;
            case "setPlayerTeam": cited.Player(effect["player"]); break;
            case "forceMove":
                cited.Selector(effect["selector"]);
                cited.Cell(effect["source"]);
                if (!Whole(effect["distance"], 0)) cited.Faults.Add("强制位移距离必须是非负整数");
                break;
            case "teleportUnits":
            case "restoreWithdrawnUnits":
                cited.Zone(effect["zone"]);
                cited.Selector(effect["selector"]);
                break;
            case "addOverlay":
                Add(cited.Overlays, effect["overlay"]);
                cited.Zone(effect["zone"]);
                break;
            case "activateObjective":
            case "cancelObjective":
            case "completeObjective":
            case "revealObjective": cited.Objective(effect); break;
            case "setUnitDirective":
                var directive = StateJson.Object(effect["directive"], "directive");
                cited.Selector(effect["selector"]);
                cited.Zone(directive["zone"]);
                Add(cited.Directives, directive["mode"]);
                cited.Cells.AddRange(StateJson.Objects(directive["waypoints"], "waypoints"));
                break;
            case "addEngagementRule":
                var rule = StateJson.Object(effect["rule"], "engagement rule");
                cited.Zone(rule["zone"]);
                if (Blank(StateJson.Text(rule, "id"))) cited.Faults.Add("交战规则缺少 id");
                foreach (var owner in rule["players"] as JsonArray ?? new JsonArray()) cited.Player(owner);
                break;
            case "replaceTerrain":
                cited.Zone(effect["zone"]);
                Add(cited.Terrains, effect["terrain"]);
                break;
            case "setElevation":
            case "addElevation":
                cited.Zone(effect["zone"]);
                var value = effect[StateJson.Text(effect, "type") == "setElevation" ? "value" : "amount"];
                if (!Whole(value)) cited.Faults.Add("动态海拔必须使用整数");
                break;
            case "setCliffs": cited.Edges.AddRange(StateJson.Objects(effect["edges"], "cliff edges")); break;
            case "setDirectionalCover":
                foreach (var cover in StateJson.Objects(effect["covers"], "directional covers")) cited.Cell(cover["at"]);
                break;
            case "damageStructure":
            case "repairStructure": Add(cited.Structures, effect["id"]); break;
            case "moveComposite": Add(cited.Composites, effect["id"]); break;
        }
        return cited;
    }

    internal static RulePayloadReferences ForObjective(JsonObject objective)
    {
        var cited = new RulePayloadReferences();
        switch (StateJson.Text(objective, "type"))
        {
            case "eliminate": cited.Selector(objective["selector"]); break;
            case "destroy":
                foreach (var structure in StateJson.Array(objective["structures"], "structures")) Add(cited.Structures, structure);
                break;
            case "neutralizeComposite":
                Add(cited.Composites, objective["composite"]);
                if (objective.ContainsKey("minimumNeutralized") && !Whole(objective["minimumNeutralized"], 1))
                    cited.Faults.Add("瘫痪数量必须 >= 1");
                break;
            case "protect":
                cited.Selector(objective["selector"]);
                if (!(Number(objective["minimumAlive"]) >= 1 && Number(objective["untilTurn"]) >= 1))
                    cited.Faults.Add("保护目标的人数和截止回合必须 >= 1");
                break;
            case "escort":
                cited.Zone(objective["zone"]);
                cited.Selector(objective["selector"]);
                if (!(Number(objective["count"]) >= 1)) cited.Faults.Add("护送目标的抵达人数必须 >= 1");
                break;
            case "control": cited.Zone(objective["zone"]); break;
            case "failOn": cited.Conditions.Add(StateJson.Object(objective["condition"], "condition")); break;
        }
        return cited;
    }

    internal static IEnumerable<JsonObject> ConditionChildren(JsonObject condition) => StateJson.Text(condition, "type") switch
    {
        "all" or "any" => StateJson.Objects(condition["conditions"], "conditions"),
        "not" => new[] { StateJson.Object(condition["condition"], "condition") },
        _ => Array.Empty<JsonObject>(),
    };

    internal static IEnumerable<JsonObject> ObjectiveChildren(JsonObject objective) => StateJson.Text(objective, "type") switch
    {
        "all" or "any" or "sequence" => StateJson.Objects(objective["objectives"], "objectives"),
        "optional" or "failOn" => new[] { StateJson.Object(objective["objective"], "objective") },
        _ => Array.Empty<JsonObject>(),
    };

    internal static IEnumerable<JsonObject> DeclaredObjectiveChildren(JsonObject objective) => objective["objectives"] is JsonArray children
        ? children.Select(child => StateJson.Object(child, "objective"))
        : objective["objective"] is JsonObject child ? new[] { child } : Array.Empty<JsonObject>();

    internal static IEnumerable<string> ScheduleFaults(JsonObject trigger)
    {
        if (trigger["repeat"] is not JsonObject repeat) yield break;
        if (!Whole(repeat["everyRounds"], 1)) yield return "循环间隔必须是正整数";
        if (repeat.ContainsKey("startTurn") && Number(repeat["startTurn"]) < 1) yield return "起始回合必须 >= 1";
        if (repeat.ContainsKey("endTurn") && repeat.ContainsKey("startTurn") &&
            Number(repeat["endTurn"]) < Number(repeat["startTurn"])) yield return "结束回合早于起始回合";
        if (repeat.ContainsKey("maxFirings") && !Whole(repeat["maxFirings"], 1)) yield return "最大触发次数必须是正整数";
    }

    internal static double Number(JsonNode? value)
    {
        if (value is not JsonValue scalar || scalar.GetValueKind() != System.Text.Json.JsonValueKind.Number)
            return double.NaN;
        return double.TryParse(scalar.ToJsonString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }
    internal static bool Blank(string value) => value.All(character => character is
        '\u0009' or '\u000a' or '\u000b' or '\u000c' or '\u000d' or '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff');
    private static bool Whole(JsonNode? value, double minimum = double.NegativeInfinity)
    {
        var number = Number(value);
        return double.IsFinite(number) && number == Math.Truncate(number) && number >= minimum;
    }
    private static void Add(List<string> into, JsonNode? value)
    {
        if (value is not null) into.Add(StateJson.Text(value, "reference"));
    }
    private void Zone(JsonNode? value) => Add(Zones, value);
    private void Player(JsonNode? value)
    {
        if (value is not null) Players.Add(Number(value));
    }
    private void Cell(JsonNode? value)
    {
        if (value is not null) Cells.Add(StateJson.Object(value, "cell"));
    }
    private void Selector(JsonNode? value) => Zone(StateJson.Object(value, "selector")["zone"]);
    private void Objective(JsonObject value) => Objectives.Add(new(Number(value["player"]), StateJson.Text(value, "id")));
}
