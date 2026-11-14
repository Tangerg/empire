using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;
/// <summary>C01 objective contracts use detached adversarial fixtures, not a claimed playable route.</summary>
public partial class C01MissionChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Run(new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json")), BattleTextChecks.OpeningLevel());
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Run(BattleEngine engine, JsonObject level)
    {
        int passed = 0;
        IEnumerable<JsonObject> Units(JsonObject s) => s["units"]!.AsArray().Select(x => x!.AsObject());
        string Key(JsonObject u) => u["key"]!.GetValue<string>();
        void Require(bool yes, string name)
        {
            if (!yes)
                throw new InvalidOperationException(name);
            GD.Print("CONTRACT_PASS " + name);
            passed++;
        }
        string Status(JsonObject s, string id) => s["players"]!.AsArray()[0]!["objectiveStates"]![id]!["status"]!.GetValue<string>();
        void Kill(JsonObject s, Func<JsonObject, bool> target)
        {
            foreach (var u in Units(s).Where(target).ToArray())
                engine.Combat.Damage.Resolve(s, u["id"]!.GetValue<int>(), u["hp"]!.GetValue<double>(), false, _ => new JsonObject { ["type"] = "testDamage" }, _ => { });
        }
        bool Vanguard(JsonObject u) => new[] { "vanguard-1", "vanguard-2", "vanguard-archer" }.Contains(Key(u));
        void Own(JsonObject s, int owner) => s["map"]!["owners"]![13 * 36 + 18] = owner;
        var s = engine.CreateState(level);
        Own(s, 1);
        Require(engine.Victory.Evaluate(s, _ => { }) is null, "capture alone does not win");
        Require(Status(s, "village-control") == "completed", "control observes completed ownership");
        Own(s, 2);
        Kill(s, Vanguard);
        Require(engine.Victory.Evaluate(s, _ => { }) is not null, "documented existing control latch survives recapture");
        s = engine.CreateState(level);
        Kill(s, Vanguard);
        Require(engine.Victory.Evaluate(s, _ => { }) is null, "three vanguard alone do not win");
        Own(s, 1);
        Require(engine.Victory.Evaluate(s, _ => { }) is { Kind: "objectiveCompleted", Team: 1, Objective: "primary" }, "capture and three vanguard suffice");
        Require(Units(s).Any(u => Key(u) == "vanguard-cain") && Units(s).Any(u => Key(u) == "south-bow"), "Cain and optional south enemy can survive victory");
        s = engine.CreateState(level);
        Kill(s, u => u["owner"]!.GetValue<int>() == 2);
        var clear = engine.Victory.Evaluate(s, _ => { });
        Require(clear is { Kind: "lastTeamRemaining", Team: 1 }, "all enemy clearance is existing alternate victory");
        Require(Status(s, "village-control") == "active", "all enemy removal does not fabricate village control");
        s = engine.CreateState(level);
        Own(s, 1);
        Kill(s, u => Vanguard(u) || Key(u) == "campaign-laiya");
        engine.Victory.Evaluate(s, _ => { });
        Require(Status(s, "primary") == "failed", "Laiya failure dominates objective completion");
        s = engine.CreateState(level);
        Kill(s, u => Key(u) == "west-spear");
        var events = new List<JsonObject>();
        engine.Scenarios.Run(s, "afterAction", events.Add);
        Require(events.All(e => e["signal"]?.GetValue<string>() != "found_matching_orders"), "friendly death cannot reveal matching orders");
        var fixture = level.DeepClone().AsObject();
        var actor = fixture["units"]!.AsArray().Select(x => x!.AsObject()).Single(u => Key(u) == "campaign-torren");
        actor["x"] = 18;
        actor["y"] = 13;
        actor["hp"] = 49;
        var battle = engine.CreateBattle(fixture);
        var before = battle.Snapshot();
        var torren = Units(before).Single(u => Key(u) == "campaign-torren");
        Require(before["map"]!["owners"]![13 * 36 + 18]!.GetValue<int>() == 0, "standing on village is not capture");
        battle.Dispatch(new JsonObject { ["kind"] = "command", ["unit"] = torren["id"]!.DeepClone(), ["path"] = new JsonArray(new JsonObject { ["x"] = 18, ["y"] = 13 }), ["command"] = new JsonObject { ["ability"] = "capture" } });
        var partial = battle.Snapshot();
        GD.Print("CAPTURE owner=" + partial["map"]!["owners"]![13 * 36 + 18] + " progress=" + partial["map"]!["captureProgress"]![13 * 36 + 18]);
        Require(partial["map"]!["owners"]![13 * 36 + 18]!.GetValue<int>() == 0, "partial capture does not transfer owner");
        Require(Status(partial, "village-control") == "active", "incomplete progressive capture stays active");
        GD.Print($"CONTRACTS {passed} passed");
    }
}
