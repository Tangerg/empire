using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;
/// <summary>Detached objective negatives and authored campaign routes test different boundaries.</summary>
public partial class C01MissionChecks : Node
{
    public override void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            Run(engine, BattleTextChecks.OpeningLevel());
            CheckCampaignRoutes(engine);
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void CheckCampaignRoutes(BattleEngine engine)
    {
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var rules = new CampaignApplicationRules(engine, presentation);
        foreach (var file in new[] { "c01-steady-victory", "c01-rush-victory" })
        {
            var route = PresentationJson.Load<JsonObject>($"res://tests/native/{file}.json");
            var choice = route["choice"]!.GetValue<string>();
            var campaign = new GrayFlagCampaign(rules, definition, levels);
            ReachOpening(campaign, choice, route["levelId"]!.GetValue<string>());
            campaign.BeginBattle();
            Check(StateDigest.Hash(campaign.Battle!.Snapshot()) == route["initialStateHash"]!.GetValue<string>(),
                $"{choice} must start from the authored C01 deployment and campaign funds.");
            var actions = route["actions"]!.AsArray();
            for (var index = 0; index < actions.Count; index++)
            {
                var battle = campaign.Battle!;
                var action = actions[index]!.AsObject();
                Check(!battle.IsConcluded, $"{choice} cannot finish before recorded order {index}.");
                if (battle.IsComputerTurn)
                {
                    Check(EcmaJson.DeepEquals(engine.Ai.ChooseAction(battle.Snapshot()), action),
                        $"{choice} recorded order {index} must match the live opponent.");
                    Check(battle.AdvanceComputerStep() is { Committed: true }, $"AI order {index} failed to commit.");
                }
                else
                {
                    var result = battle.Dispatch(EcmaJson.ToElement(action), battle.Revision);
                    Check(result.Accepted, $"{choice} order {index} was rejected: {result.Message}");
                }

                if (index == actions.Count / 2)
                {
                    var reloaded = new GrayFlagCampaign(rules, definition, levels);
                    reloaded.Load(campaign.Save());
                    Check(EcmaJson.DeepEquals(battle.Snapshot(), reloaded.Battle!.Snapshot()),
                        $"{choice} midpoint save must preserve the complete battle.");
                    campaign = reloaded;
                }
            }

            var state = campaign.Battle!.Snapshot();
            Check(StateDigest.Hash(state) == route["finalStateHash"]!.GetValue<string>()
                && state["conclusion"]?["objective"]?.GetValue<string>() == "primary"
                && campaign.BattleSummary().Outcome == "victory", $"{choice} must win by the authored primary objective.");
            campaign.FinishBattle();
            Check(campaign.Node()["id"]!.GetValue<string>() == "aftermath-01", $"{choice} must reach the matching-orders aftermath.");
            ReachOpening(campaign, "accept-truce", "c01-02");
            campaign.BeginBattle();
            Check(campaign.Battle!.Snapshot()["levelId"]!.GetValue<string>() == "c01-02", $"{choice} must carry into C02.");
            GD.Print($"C01_CAMPAIGN_ROUTE_OK choice={choice} orders={actions.Count} turn={state["turn"]} hash={StateDigest.Hash(state)}");
        }
    }

    private static void ReachOpening(GrayFlagCampaign campaign, string choice, string levelId)
    {
        for (var step = 0; step < 48; step++)
        {
            var node = campaign.Node();
            switch (node["type"]!.GetValue<string>())
            {
                case "battle":
                    Check(node["level"]!.GetValue<string>() == levelId, $"Expected campaign level {levelId}.");
                    return;
                case "story":
                    campaign.Advance();
                    break;
                case "choice":
                    campaign.Choose(choice);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected campaign node before {levelId}: {node}.");
            }
        }
        throw new InvalidOperationException($"Campaign did not reach {levelId}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
        // Detached scenario fixture supplies the death count normally owned by ActionExecutionContext.
        s["scenario"]!["eventCounts"]!["death"] = 1;
        var events = new List<JsonObject>();
        engine.Scenarios.Run(s, "afterAction", events.Add);
        Require(events.All(e => e["signal"]?.GetValue<string>() != "found_matching_orders"), "friendly death cannot reveal matching orders");
        var regression = level.DeepClone().AsObject();
        regression["scenario"]!["triggers"] = JsonNode.Parse("""
            [{"id":"matching-orders","timing":"afterAction",
              "condition":{"type":"eventCount","event":"death","op":"gte","value":1},
              "effects":[{"type":"emitSignal","signal":"found_matching_orders"}]}]
            """);
        var oldTrigger = engine.CreateState(regression);
        oldTrigger["scenario"]!["eventCounts"]!["death"] = 1;
        var oldEvents = new List<JsonObject>();
        engine.Scenarios.Run(oldTrigger, "afterAction", oldEvents.Add);
        Require(oldEvents.Any(e => e["signal"]?.GetValue<string>() == "found_matching_orders"),
            "death-count fixture detects the removed matching-orders trigger");
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
