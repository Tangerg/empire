#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Presentation-only ledger instructions from legal commands applied to an unmodified production C08 opening.</summary>
public partial class LedgerHintChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    public override void _Ready()
    {
        try
        {
            Check();
            GD.Print($"LEDGER_HINT_CHECKS_OK {assertions} assertions; presentation only, no new victory condition.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Check()
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var level = levels.OfType<JsonObject>().Single(item => item["id"]!.GetValue<string>() == "c01-08");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var rules = new BattleSessionRules(engine, presentation, new JsonArray());
        var session = new GrayFlagSession(rules, level);
        var before = session.Snapshot();
        var hint = presentation.Battle.ScenarioHints(before, 1).Single();
        var zone = hint.Cells.ToHashSet();
        TacticalHudRow Hint(GrayFlagSession value, bool busy = false) => value.ProjectHud(new(1, value.Revision), new(), null, null, busy)
            .Sections.Single(section => section.Key == "objectives").Rows.Single(row => row.Key == $"scenario-hint:{hint.Trigger}");

        // Find a real player-turn unit that can legally traverse the zone and stop beyond it.
        // Reuse the same path prefix for the positive stop case; never move actors by editing state.
        JsonObject? passingAction = null;
        JsonObject? stoppingAction = null;
        var battle = engine.CreateBattle(level);
        var prefix = PresentationJson.Load<JsonArray>("res://data/checks/c08-ledger-prefix.json");
        var prefixLength = 0;
        foreach (var recorded in prefix.OfType<JsonObject>())
        {
            Require(StateDigest.Hash(battle.Snapshot()) == recorded["beforeHash"]!.GetValue<string>(), "Legal prefix before-state hash matches retained provenance.");
            var receipt = battle.Dispatch(recorded["action"]!.AsObject());
            Require(receipt.StateHash == recorded["stateHash"]!.GetValue<string>(), "Legal prefix production receipt hash matches.");
            prefixLength++;
        }
        before = battle.Snapshot();
        Require(before["currentPlayer"]!.GetValue<int>() == 1, "Witness is a real player turn.");
        foreach (var unit in before["units"]!.AsArray().OfType<JsonObject>().Where(unit => unit["owner"]!.GetValue<int>() == 1 && !unit["done"]!.GetValue<bool>()))
        {
            var field = engine.Space.ComputeMoveField(before, unit);
            var board = engine.Space.Board(before);
            foreach (var index in field.Stops.OrderBy(index => index))
            {
                var destination = board.CoordOf(index);
                if (zone.Contains(destination))
                    continue;
                var path = engine.Space.PathTo(field, before, destination);
                if (path is null || !path.Any(zone.Contains)
                    || !engine.CommandsAt(before, unit, destination).Any(option => option.Ability == "wait"))
                    continue;
                var stop = path.First(zone.Contains);
                if (!field.Stops.Contains(board.IndexOf(stop))
                    || !engine.CommandsAt(before, unit, stop).Any(option => option.Ability == "wait"))
                    continue;
                JsonObject Wait(Coord[] route) => new()
                {
                    ["kind"] = "command",
                    ["unit"] = unit["id"]!.DeepClone(),
                    ["path"] = new JsonArray(route.Select(cell => (JsonNode)TacticalQueries.CoordinateJson(cell)).ToArray()),
                    ["command"] = new JsonObject { ["ability"] = "wait" }
                };
                passingAction = Wait(path.ToArray());
                stoppingAction = Wait(path.TakeWhile(cell => cell != stop).Append(stop).ToArray());
                break;
            }
            if (passingAction is not null)
                break;
        }
        Require(passingAction is not null && stoppingAction is not null, "Production legal prefix must provide the ledger traversal witness.");
        GD.Print($"Ledger witness after {prefixLength} legal commands: {stoppingAction}");
        session = GrayFlagSession.FromSave(rules, engine.SaveBattle(before));
        var row = Hint(session);
        Require(row.Label.Contains("取回佣兵账本", StringComparison.Ordinal) && row.Hint.Contains("仅经过", StringComparison.Ordinal)
            && row.Intent?.Kind == TacticalHudIntentKind.FocusObjective
            && row.TargetCells!.SequenceEqual(hint.Cells.Select(cell => new BoardCell(cell.X, cell.Y))),
            "Pending HUD projects the authored trigger zone, not copied coordinates.");
        Require(session.ProjectHud(new(1, session.Revision), new(), null, null, false).Sections.Single(section => section.Key == "objectives")
            .Rows.Any(item => item.Key.EndsWith(":instruction", StringComparison.Ordinal) && item.Label.Contains("待机即可", StringComparison.Ordinal)),
            "Instructions are visible text rather than tooltip-only.");
        Require(Hint(session, true).Intent is null && Hint(session, true).TargetCells is null, "Busy hint cannot locate.");
        Require(EcmaJson.DeepEquals(before, session.Snapshot()), "HUD projection cannot change canonical state.");
        Require(presentation.Battle.ScenarioHints(before, 2).Count == 0, "Hint cannot leak to another owner.");
        var revision = session.Revision;
        Require(session.Dispatch(EcmaJson.ToElement(stoppingAction!), revision).Accepted, "Production legal zone-stop wait must dispatch.");
        var completed = Hint(session);
        Require(completed.Label == "已取得佣兵账本" && completed.Selected && completed.Intent is null && completed.TargetCells is null,
            "Canonical fired state projects completed non-locatable hint.");
        Require(EcmaJson.ToElement(session.Snapshot()["scenario"]!["variables"]!["ledgers"]!).GetDouble() == 1,
            "Existing afterAction effect acquires exactly one ledger.");
        var signals = session.Events.Where(item => item["type"]!.GetValue<string>() == "scenarioSignal").ToArray();
        Require(signals.Length == 1 && signals[0]["signal"]!.GetValue<string>() == "mercenary_dead_paid"
            && presentation.Battle.SignalName("c01-08", "mercenary_dead_paid") == completed.Label, "Original signal announces collection, not payment.");
        var saved = EcmaJson.Parse(EcmaJson.Stringify(session.Save()))!;
        var loaded = GrayFlagSession.FromSave(rules, saved);
        Require(EcmaJson.DeepEquals(session.Snapshot(), loaded.Snapshot()) && Hint(loaded) == completed,
            "Production serialized save/load reproduces completed hint without UI memory.");
        var staleRejected = false;
        try
        {
            session.ProjectHud(new(1, revision), new(), null, null, false);
        }
        catch (DomainInvariantException) { staleRejected = true; }
        Require(staleRejected, "Old HUD stamp cannot project advanced state.");
        Require(session.Undo(session.Revision).Accepted && EcmaJson.DeepEquals(before, session.Snapshot())
            && Hint(session).TargetCells is { Count: > 0 } && !Hint(session).Selected, "Undo restores canonical pending hint.");
        Require(session.Dispatch(EcmaJson.ToElement(passingAction!), session.Revision).Accepted
            && !Hint(session).Selected && EcmaJson.ToElement(session.Snapshot()["scenario"]!["variables"]!["ledgers"]!).GetDouble() == 0
            && !session.Events.Any(item => item["type"]!.GetValue<string>() == "scenarioSignal"),
            "Legal traversal without stopping emits no collection signal and remains pending.");
        CheckMetadata(engine, levels);
    }

    private void CheckMetadata(BattleEngine engine, JsonArray levels)
    {
        var source = PresentationJson.Load<JsonObject>("res://data/grayflag-battle-text.json");
        void Reject(string label, Action<JsonObject> mutate)
        {
            var copy = source.DeepClone().AsObject();
            mutate(copy);
            var rejected = false;
            try
            {
                BattleTextDocument.Parse(new(levels, engine.Objectives, engine.ScenarioEffects), copy.ToJsonString());
            }
            catch (LocalizationException) { rejected = true; }
            Require(rejected, "Metadata must reject " + label);
        }
        JsonObject Entry(JsonObject document) => document["levels"]!["c01-08"]!["scenarioHints"]![0]!.AsObject();
        Reject("unknown hint fields", document => Entry(document)["cells"] = new JsonArray());
        Reject("missing zone", document => Entry(document).Remove("zone"));
        Reject("unknown zone", document => Entry(document)["zone"] = "missing");
        Reject("unknown trigger", document => Entry(document)["trigger"] = "missing");
        Reject("mismatched owner", document => Entry(document)["owner"] = 2);
        Reject("unvalidated localized text shape", document => Entry(document)["title"] = new JsonObject { ["key"] = "x", ["literal"] = "y" });
        Reject("duplicate trigger hints", document => document["levels"]!["c01-08"]!["scenarioHints"]!.AsArray().Add(Entry(document).DeepClone()));
        Reject("null hint array", document => document["levels"]!["c01-08"]!["scenarioHints"] = null);
        Reject("unrelated unknown level field", document => document["levels"]!["c01-08"]!["hintz"] = new JsonArray());
        Require(source["schema"]!.GetValue<int>() == 4, "Existing schema remains explicit; hints are a validated optional extension.");
    }
}
