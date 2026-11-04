#nullable enable
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.State;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private void CheckHudSnapshotLifetime()
    {
        Action<JsonObject>? onQuery = null;
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")),
            [new EnginePlugin("test.hud-read-lifetime", 1, context =>
            {
                var abilities = context.Require(BattleCapabilities.Abilities).Clone();
                var wait = abilities.Get("wait");
                abilities.Replace(wait with
                {
                    Usable = (rules, query) =>
                    {
                        onQuery?.Invoke(query.State);
                        return wait.Usable(rules, query);
                    }
                });
                context.Replace(BattleCapabilities.Abilities, abilities);
            }, overrides: [BattleCapabilities.Abilities], requiresCapabilities: [BattleCapabilities.Abilities])]);
        var rules = new BattleSessionRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()), new JsonArray());
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var level = levels.OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var session = new GrayFlagSession(rules, level);
        var initial = session.Snapshot();
        var queries = session.GetTacticalQueries(75);
        var actor = queries.Actors().First();
        var origin = queries.Unit(actor)!.At;
        var unit = new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = actor };
        var destination = unit with
        {
            Kind = TacticalSelectionKind.Destination,
            Destination = origin
        };
        var cells = queries.InteractiveCells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Take(8).ToArray();
        Require(cells.Length == 8, "The unchanged formal Redstone map provides distinct HUD read inputs.");
        TacticalHudProjection Hud(TacticalSelection selection, TacticalHit? hit = null, bool busy = false, long binding = 75) =>
            session.ProjectHud(new(binding, session.Revision), selection, hit, actor, busy);
        string Facts(TacticalHudProjection value) => JsonSerializer.Serialize(value with
        {
            Stamp = default,
            Sections = value.Sections.Select(section => section with
            {
                Rows = section.Rows.Select(row => row with
                {
                    Intent = row.Intent is { } intent ? intent with
                    {
                        Stamp = default
                    } : null
                }).ToArray()
            }).ToArray()
        });
        void MatchesFresh(TacticalSelection selection, TacticalHit? hit = null, bool busy = false, long binding = 75)
        {
            var fresh = GrayFlagSession.FromSave(rules, session.Save());
            var expected = fresh.ProjectHud(new(binding, 0), selection, hit, actor, busy);
            var actual = Hud(selection, hit, busy, binding);
            Require(Facts(actual) == Facts(expected), "Warm HUD facts match an independently admitted snapshot of the same canonical battle.");
            Require(actual.Stamp == new TacticalStamp(binding, session.Revision)
                && actual.Sections.SelectMany(section => section.Rows).All(row => row.Intent is null || row.Intent.Stamp == actual.Stamp),
                "Reused state must not retain old binding/revision intent stamps.");
        }
        var retained = Hud(destination);
        var retainedText = JsonSerializer.Serialize(retained);
        foreach (var selection in new[] { new TacticalSelection(), unit, destination })
            foreach (var cell in cells)
                foreach (var busy in new[] { false, true })
                    MatchesFresh(selection, TacticalHit.Ground(cell), busy, busy ? 76 : 75);
        Require(JsonSerializer.Serialize(retained) == retainedText && EcmaJson.DeepEquals(initial, session.Snapshot()),
            "Alternating HUD inputs cannot change an earlier projection or the canonical battlefield.");

        var otherLevel = levels.OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-07");
        var other = new GrayFlagSession(rules, otherLevel);
        var otherHud = other.ProjectHud(new(75, 0), unit, null, actor, false);
        Require(Facts(otherHud) != Facts(Hud(unit)) && JsonSerializer.Serialize(Hud(destination)) == retainedText,
            "Separate battles at the same revision and binding cannot reuse each other's HUD state.");

        var exported = session.Snapshot();
        exported["turn"] = 999;
        exported["units"]![0]!["hp"] = 1;
        exported["map"]!["tiles"]![0] = "castle";
        Require(JsonSerializer.Serialize(Hud(destination)) == retainedText && EcmaJson.DeepEquals(initial, session.Snapshot()),
            "Mutable public snapshots are never aliases of the private HUD read state or canonical battle.");
        Require(!session.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "unknown-test-order" }), session.Revision).Accepted,
            "The invalid order is refused through the normal command boundary.");
        Require(session.Revision == 0 && JsonSerializer.Serialize(Hud(destination)) == retainedText,
            "A refused order cannot invalidate facts or advance the HUD revision.");

        var failure = new DomainInvariantException("test HUD query failure");
        onQuery = state => { state["turn"] = 999; throw failure; };
        Exception? caught = null;
        try
        {
            Hud(destination);
        }
        catch (DomainInvariantException error) { caught = error; }
        finally { onQuery = null; }
        Require(ReferenceEquals(caught, failure), "A failed composed HUD query propagates its original exception.");
        Require(JsonSerializer.Serialize(Hud(destination)) == retainedText && EcmaJson.DeepEquals(initial, session.Snapshot()),
            "A partially modified failing query cannot poison the next HUD projection or the battle.");

        JsonElement FacingOrder() => Hud(unit).Sections.SelectMany(section => section.Rows)
            .Where(row => row.Intent?.Action is { } action && action.GetProperty("kind").ValueEquals("face") && !row.Selected)
            .Select(row => row.Intent!.Action!.Value).First();
        var cold = new GrayFlagSession(rules, level);
        onQuery = state => { state["turn"] = 999; throw failure; };
        caught = null;
        try
        {
            cold.ProjectHud(new(75, 0), destination, null, actor, false);
        }
        catch (DomainInvariantException error) { caught = error; }
        finally { onQuery = null; }
        Require(ReferenceEquals(caught, failure)
            && Facts(cold.ProjectHud(new(75, 0), destination, null, actor, false)) == Facts(retained)
            && EcmaJson.DeepEquals(initial, cold.Snapshot()),
            "The first failed HUD read must not publish a partially modified cache either.");

        var beforeFace = Facts(Hud(unit));
        var face = FacingOrder();
        var oldStamp = new TacticalStamp(75, session.Revision);
        Require(session.Dispatch(face, session.Revision).Accepted, "A real facing order advances the formal battle.");
        Require(Facts(Hud(unit)) != beforeFace, "A completed facing order exposes new HUD facts.");
        MatchesFresh(unit);
        caught = null;
        var staleQueries = 0;
        onQuery = _ => staleQueries++;
        try
        {
            session.ProjectHud(oldStamp, destination, null, actor, false);
        }
        catch (DomainInvariantException error) { caught = error; }
        finally { onQuery = null; }
        Require(caught?.Message == "HUD projection belongs to a stale session revision." && staleQueries == 0,
            "An old session revision is rejected before invoking any composed HUD query.");
        Require(session.Undo(session.Revision).Accepted && session.Revision == 2, "Undo advances revision instead of reviving the original read lifetime.");
        Require(Facts(Hud(destination)) == Facts(retained), "Undo restores the full initial HUD facts at its new revision.");
        MatchesFresh(destination);

        face = FacingOrder();
        onQuery = _ =>
        {
            onQuery = null;
            Require(session.Dispatch(face, session.Revision).Accepted, "The reentrant witness commits through the only battle writer.");
        };
        caught = null;
        try
        {
            Hud(destination);
        }
        catch (DomainInvariantException error) { caught = error; }
        finally { onQuery = null; }
        Require(caught?.Message == "Battle changed during HUD projection.", "A callback changing revision cannot publish mixed-time HUD facts.");
        MatchesFresh(destination);
        Require(session.Undo(session.Revision).Accepted, "The reentrant witness remains a normal undoable order.");
        Require(session.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), session.Revision).Accepted,
            "A real end-turn order changes the active side through the canonical lifecycle.");
        MatchesFresh(new());
        Require(JsonSerializer.Serialize(retained) == retainedText, "Later orders and undo never modify retained HUD actions or rows.");

        CheckHudSnapshotAllocation(rules, level, actor, cells);
        GD.Print("HUD_READ_LIFETIME passed: formal alternating inputs, immutable outputs, snapshot isolation, refusal, exception recovery, face/undo/end-turn and reentrant revision rejection.");
    }

    private void CheckHudSnapshotAllocation(BattleSessionRules rules, JsonObject level, int actor, BoardCell[] cells)
    {
        var session = new GrayFlagSession(rules, level);
        var stamp = new TacticalStamp(81, 0);
        var hits = cells.Select(TacticalHit.Ground).ToArray();
        var selections = new[] { new TacticalSelection(), new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = actor } };
        session.Snapshot();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var snapshot = session.Snapshot();
        var snapshotBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Require(snapshotBytes > 1024 * 1024, "The unchanged large formal map must exercise a substantial detached snapshot.");
        start = GC.GetAllocatedBytesForCurrentThread();
        session.ProjectHud(stamp, selections[0], hits[0], null, false);
        var coldBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        GD.Print($"HUD_READ_COLD bytes={coldBytes} snapshot={snapshotBytes}");
        foreach (var selection in selections)
        {
            foreach (var hit in hits)
                session.ProjectHud(stamp, selection, hit, null, false);
            for (var sample = 0; sample < 3; sample++)
            {
                start = GC.GetAllocatedBytesForCurrentThread();
                foreach (var hit in hits)
                    session.ProjectHud(stamp, selection, hit, null, false);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                GD.Print($"HUD_READ_ALLOCATION kind={selection.Kind} sample={sample} cells={hits.Length} bytes={allocated} snapshot={snapshotBytes}");
                Require(allocated < hits.Length * snapshotBytes * 3 / 4,
                    "Repeated formal HUD reads must not clone the whole unchanged battlefield for each hover.");
            }
        }
        Require(EcmaJson.DeepEquals(snapshot, session.Snapshot()) && session.Revision == 0 && session.ActionLog.Count == 0 && session.Events.Count == 0,
            "Measured HUD work cannot advance canonical state, actions, events or revision.");
    }
}
