using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;

internal static class TacticPlanChecks
{
    public static void Run(string oracleRoot)
    {
        var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(oracleRoot, "../../data/content-packs.json")))!.AsArray();
        var engine = new BattleEngine(packs);
        foreach (var grid in new[] { "square4", "square8", "hex" })
            CheckPlan(engine, Fixture(grid));
        var selfPacks = packs.DeepClone().AsArray();
        selfPacks.OfType<JsonObject>().Where(pack => pack["tactics"] is JsonArray)
            .SelectMany(pack => pack["tactics"]!.AsArray().OfType<JsonObject>())
            .Single(tactic => tactic["id"]!.GetValue<string>() == "c01.gray-rally")["target"] = "self";
        var selfEngine = new BattleEngine(selfPacks);
        var selfState = Fixture("square4");
        var self = selfEngine.Support.PlanTactic(selfState, "commander", "c01.gray-rally", new Coord(-99, -99));
        Require(self is { Target: { X: 3, Y: 3 } } && self.Tactic.TargetMode == "self"
            && self.Tactic.Targets.SequenceEqual(new[] { new Coord(3, 3) }), "Self tactics must resolve to the leader, ignoring a supplied center.");
        var selfEvents = new List<JsonObject>();
        selfEngine.Support.ExecuteTactic(selfState, "commander", "c01.gray-rally", null, selfEvents.Add);
        Require(selfEvents.Single(value => Text(value, "type") == "tacticUsed")["target"]!.ToJsonString() == "{\"x\":3,\"y\":3}",
            "Self execution and planning must normalize the same center.");
    }

    private static void CheckPlan(BattleEngine engine, JsonObject state)
    {
        var before = EcmaJson.Stringify(state);
        var expectedRecipients = Text(state["rules"]!.AsObject(), "grid") == "square8"
            ? new[] { 1, 2, 6, 7 } : new[] { 1, 2, 6 };
        var definition = engine.Catalog.Definition("tactics", "c01.gray-rally")!.Value.GetRawText();
        var plan = engine.Support.PlanTactic(state, "commander", "c01.gray-rally", new Coord(3, 3))
            ?? throw new Exception("A legal tactic center must have a plan.");
        Require(plan.Tactic.Id == "c01.gray-rally" && plan.Tactic.TargetMode == "tile"
            && plan.Tactic.Range == 3 && plan.Tactic.Radius == 2
            && plan.Tactic.Costs.SequenceEqual(new[] { new Empire.Core.Resources.BattleResources.Cost("command_points", 2) }),
            "Plan metadata must expose canonical identity, cost, range, and radius.");
        Require(plan.AlliedUnitIds.SequenceEqual(expectedRecipients),
            "Include allied radius boundaries using the configured grid: (5,5) is two steps from (3,3) only on square8; exclude enemies and neutrals.");
        Require(plan.Tactic.Effects.Count == 2
            && plan.Tactic.Effects[0].GetProperty("type").GetString() == "removeStatus"
            && plan.Tactic.Effects[0].GetProperty("status").GetString() == "shaken"
            && !plan.Tactic.Effects[0].TryGetProperty("duration", out _)
            && plan.Tactic.Effects[1].GetProperty("type").GetString() == "addStatus"
            && plan.Tactic.Effects[1].GetProperty("status").GetString() == "inspired"
            && plan.Tactic.Effects[1].GetProperty("duration").GetInt32() == 2,
            "Effects must retain authored order and duration without inventing a duration for removal.");
        ReadOnly(plan.AlliedUnitIds);
        ReadOnly(plan.Tactic.Targets);
        ReadOnly(plan.Tactic.Costs);
        ReadOnly(plan.Tactic.Effects);
        ReadOnly(engine.Support.Tactics(state, "commander"));
        Require(engine.Support.PlanTactic(state, "commander", "c01.gray-rally", null) is null
            && engine.Support.PlanTactic(state, "commander", "c01.gray-rally", new Coord(-1, 3)) is null
            && engine.Support.PlanTactic(state, "commander", "c01.gray-rally", new Coord(7, 3)) is null
            && engine.Support.PlanTactic(state, "missing", "c01.gray-rally", new Coord(3, 3)) is null
            && engine.Support.PlanTactic(state, "commander", "missing", new Coord(3, 3)) is null,
            "Unknown, missing, off-map, and out-of-range queries must return null.");
        Require(engine.Support.PlanTactic(state, "commander", "c01.gray-rally", new Coord(6, 3)) is not null,
            "The range boundary must remain legal.");
        Require(EcmaJson.Stringify(state) == before
            && engine.Catalog.Definition("tactics", "c01.gray-rally")!.Value.GetRawText() == definition,
            "Planning must not mutate state or content.");
        var unavailable = state.DeepClone().AsObject();
        unavailable["currentPlayer"] = 2;
        Require(engine.Support.PlanTactic(unavailable, "commander", "c01.gray-rally", new Coord(3, 3)) is null,
            "A non-owner must not receive a tactic plan.");
        unavailable = state.DeepClone().AsObject();
        unavailable["players"]![0]!["resources"]!["command_points"]!["current"] = 0;
        Require(engine.Support.PlanTactic(unavailable, "commander", "c01.gray-rally", new Coord(3, 3)) is null,
            "An unaffordable tactic must not receive a plan.");
        var afterAnnouncement = state.DeepClone().AsObject();
        var announcedEvents = new List<JsonObject>();
        engine.Support.ExecuteTactic(afterAnnouncement, "commander", "c01.gray-rally", plan.Target, value =>
        {
            announcedEvents.Add(value);
            if (Text(value, "type") == "tacticUsed")
                afterAnnouncement["units"]![1]!["x"] = 7;
        });
        Require(announcedEvents.Where(value => Text(value, "type") == "statusApplied")
            .Select(value => value["unit"]!.GetValue<int>()).SequenceEqual(expectedRecipients.Where(id => id != 2)),
            "Execution must continue selecting recipients after the tactic-used announcement.");
        var events = new List<JsonObject>();
        engine.Support.ExecuteTactic(state, "commander", "c01.gray-rally", plan.Target, events.Add);
        Require(events.Where(value => Text(value, "type") == "statusApplied").Select(value => value["unit"]!.GetValue<int>())
            .SequenceEqual(plan.AlliedUnitIds), "Execution must visit the same allied recipients in the same order.");
        Require(events.All(value => Text(value, "type") != "statusRemoved"),
            "A planned removal must not promise a change when every recipient lacks that status.");
        Require(events[0]["type"]!.GetValue<string>() == "resourceChanged"
            && Text(events[1], "type") == "tacticUsed" && Text(events[2], "type") == "statusApplied",
            "Spending, tactic use, and effects must retain their event order.");
        Require(engine.Support.PlanTactic(state, "commander", "c01.gray-rally", plan.Target) is null,
            "Used tactics must not remain available.");
        state["units"]![1]!["id"] = 99;
        Require(plan.AlliedUnitIds.SequenceEqual(expectedRecipients), "Plans must retain detached recipient identities.");
    }

    private static void ReadOnly<T>(IReadOnlyList<T> values)
    {
        Require(values is IList<T> { IsReadOnly: true }, "Query collections must be immutable to consumers.");
        try
        {
            ((IList<T>)values)[0] = values[0];
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new Exception("A query collection accepted mutation.");
    }

    private static JsonObject Fixture(string grid) => new()
    {
        ["currentPlayer"] = 1,
        ["rules"] = new JsonObject { ["grid"] = grid },
        ["map"] = new JsonObject { ["width"] = 8, ["height"] = 8 },
        ["players"] = new JsonArray(Player(1, 1), Player(2, 2), Player(3, 1)),
        ["commanders"] = new JsonArray(new JsonObject
        {
            ["id"] = "commander",
            ["owner"] = 1,
            ["unitId"] = 1,
            ["usedTactics"] = new JsonArray(),
            ["tactics"] = new JsonArray("c01.gray-rally")
        }),
        ["units"] = new JsonArray(Unit(1, 1, 3, 3), Unit(2, 1, 5, 3), Unit(3, 1, 6, 3),
            Unit(4, 2, 3, 4), Unit(5, 0, 3, 2), Unit(6, 3, 4, 3), Unit(7, 1, 5, 5))
    };
    private static JsonObject Player(int id, int team) => new()
    {
        ["id"] = id,
        ["team"] = team,
        ["resources"] = new JsonObject { ["command_points"] = new JsonObject { ["current"] = 5, ["capacity"] = 10 } }
    };
    private static JsonObject Unit(int id, int owner, int x, int y) => new()
    {
        ["id"] = id,
        ["owner"] = owner,
        ["x"] = x,
        ["y"] = y,
        ["statuses"] = new JsonArray()
    };
    private static string Text(JsonObject value, string field) => value[field]!.GetValue<string>();
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
