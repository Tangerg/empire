#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

internal static class BattleScreenRecruitmentQueryChecks
{
    public static void Check()
    {
        RecordingSpace? recording = null;
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")),
            [new EnginePlugin("test.native-recruitment-queries", 1, context =>
            {
                recording = new(new(context.Require(BattleCapabilities.Content), context.Require(BattleCapabilities.Grids)));
                context.Replace<ITacticalSpace>(BattleCapabilities.Space, recording);
            }, overrides: [BattleCapabilities.Space], requiresCapabilities: [BattleCapabilities.Content, BattleCapabilities.Grids])]);
        var space = recording ?? throw new InvalidOperationException("The recording spatial plugin was not installed.");
        var rules = new BattleSessionRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()), new JsonArray());
        CheckCampaignResults(rules);
        CheckAvailabilityAndIsolation(rules);
        CheckRecruitAndUndo(rules);
        CheckOccupiedAndInvisibleUnits(rules, space);
        CheckTurnAndPhaseChanges(rules);
        CheckReadPostconditions(rules, space);
        Godot.GD.Print("RECRUITMENT_QUERY_CONTRACTS passed: 16 shipped levels; canonical hint parity, ownership, occupancy, funds/cap, recruit/move/undo, binding/session/reload isolation, visibility, phase changes and revision guards.");
    }

    private static void CheckCampaignResults(BattleSessionRules rules)
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().ToArray();
        Require(levels.Length == 16, "Recruitment hint parity must cover all sixteen unchanged shipped levels.");
        foreach (var level in levels)
        {
            var session = new GrayFlagSession(rules, level);
            foreach (var binding in new long[] { 7, 7, 8 })
                CheckIdle(rules, session, $"{level["id"]}", binding);
            Require(session.Revision == 0 && !session.CanUndo && session.Events.Count == 0 && session.ActionLog.Count == 0,
                "Campaign hint reads cannot create actions, events or undo history.");
        }
    }

    private static void CheckAvailabilityAndIsolation(BattleSessionRules rules)
    {
        var state = RecruitmentFixture(rules, new(0, 0));
        var available = Restore(rules, state);
        Require(available.GetTacticalQueries(0).CanOpenRecruitment(new(0, 0)), "The detached owned castle must expose canonical recruitment.");
        state["map"]!["owners"]![0] = 0;
        var neutral = Restore(rules, state);
        state["map"]!["owners"]![0] = state["players"]!.AsArray().OfType<JsonObject>()
            .Single(player => player["id"]!.GetValue<int>() != state["currentPlayer"]!.GetValue<int>())["id"]!.DeepClone();
        var otherOwner = Restore(rules, state);
        foreach (var (session, expected) in new[]
        {
            (available, true), (neutral, false), (otherOwner, false), (available, true),
            (Restore(rules, neutral.Snapshot()), false), (GrayFlagSession.FromSave(rules, available.Save()), true)
        })
            CheckIdle(rules, session, "Session and save isolation at revision zero", 11, expected);
        Require(!neutral.GetTacticalQueries(0).CanOpenRecruitment(new(0, 0))
            && !otherOwner.GetTacticalQueries(0).CanOpenRecruitment(new(0, 0)), "Neutral and enemy ownership must not open recruitment.");

        state = RecruitmentFixture(rules, new(0, 0));
        var player = TacticalQueries.Player(state, state["currentPlayer"]!.GetValue<int>());
        player["resources"]!["funds"]!["current"] = 0;
        var unfunded = Restore(rules, state);
        var cards = rules.Engine.Recruitment.Cards(unfunded.Snapshot(), new(0, 0));
        Require(cards.Count > 0 && cards.All(card => !card.Affordable), "The zero-funds fixture must offer only unaffordable cards.");
        CheckIdle(rules, unfunded, "Unaffordable cards still open their panel", 12, true);

        player["resources"]!["funds"]!["current"] = 1000;
        state["rules"]!["maxUnitsPerPlayer"] = state["units"]!.AsArray().OfType<JsonObject>()
            .Count(unit => unit["owner"]!.GetValue<int>() == state["currentPlayer"]!.GetValue<int>());
        var capped = Restore(rules, state);
        cards = rules.Engine.Recruitment.Cards(capped.Snapshot(), new(0, 0));
        Require(cards.Count > 0 && cards.All(card => card.Affordable)
            && cards.All(card => rules.Engine.Recruitment.Refusal(capped.Snapshot(), card.Unit, new(0, 0))?.Code == "recruitment.unit_limit"),
            "The funded cap fixture must have cards but no legal purchase.");
        CheckIdle(rules, capped, "Unit cap does not hide recruitment cards", 12, true);
        var before = Capture(capped);
        var request = RecruitRequest(capped, new(0, 0), cards[0].Unit);
        Require(!capped.Dispatch(request.Action!.Value, request.Stamp.Revision).Accepted && Capture(capped) == before,
            "A refused capped recruit cannot advance revision or mutate the cached answer's battle.");
        CheckIdle(rules, capped, "Refusal preserves open-panel guidance", 13, true);

        var actor = available.GetTacticalQueries(11).Actors()[0];
        foreach (var binding in new long[] { 11, 12 })
        {
            var stamp = new TacticalStamp(binding, available.Revision);
            var selected = available.ProjectHud(stamp, new()
            {
                Kind = TacticalSelectionKind.Unit,
                UnitId = actor
            }, null, actor, false);
            Require(selected.Hint == rules.Presentation.Messages.Format("ui.hud.selection.unit") && selected.Portrait is not null
                && selected.Sections.Any(section => section.Key == "commands"), "A warm idle answer cannot reuse the previous HUD instead of projecting the selected unit.");
            AssertStamp(selected, stamp);
            var busy = available.ProjectHud(stamp, new(), null, null, true);
            Require(busy.Busy && busy.Portrait is null && busy.Sections.All(section => section.Key != "commands"),
                "Busy and selection fields must be projected afresh even when recruitment availability is unchanged.");
            CheckIdle(rules, available, "Binding and selection isolation", binding, true);
        }
    }

    private static void CheckRecruitAndUndo(BattleSessionRules rules)
    {
        var session = Restore(rules, RecruitmentFixture(rules, new(0, 0)));
        var original = Capture(session);
        var old = session.GetTacticalQueries(21);
        CheckIdle(rules, session, "Before recruit", 21, true);
        var card = rules.Engine.Recruitment.Cards(session.Snapshot(), new(0, 0)).First(card => card.Affordable);
        var request = RecruitRequest(session, new(0, 0), card.Unit);
        Require(session.Dispatch(request.Action!.Value, request.Stamp.Revision).Accepted && session.Revision == 1,
            "The controller's real recruit request must commit in the funded detached fixture.");
        CheckIdle(rules, session, "Recruit occupies the only recruiting site", 21, false);
        ExpectInvariant(() => old.CanOpenRecruitment(new(0, 0)), "stale");
        ExpectInvariant(() => session.ProjectHud(new(21, 0), new(), null, null, false), "stale");
        var occupiedSave = GrayFlagSession.FromSave(rules, session.Save());
        CheckIdle(rules, occupiedSave, "Reloaded occupied site starts at revision zero", 21, false);
        Require(session.Undo(session.Revision).Accepted && session.Revision == 2, "Recruitment must be undoable in this human turn.");
        CheckIdle(rules, session, "Undo restores recruitment availability", 22, true);
        Require(Capture(session) == (original with
        {
            Revision = 2
        }), "Undo restores the complete state and ordered logs without reviving revision zero.");
        CheckIdle(rules, occupiedSave, "Another session's undo cannot change the occupied save", 22, false);
        CheckIdle(rules, GrayFlagSession.FromSave(rules, session.Save()), "Reloaded undo result", 22, true);
    }

    private static void CheckOccupiedAndInvisibleUnits(BattleSessionRules rules, RecordingSpace space)
    {
        var initial = new GrayFlagSession(rules, Level());
        var id = initial.GetTacticalQueries(0).Actors()[0];
        var origin = initial.GetTacticalQueries(0).Unit(id)!.At;
        var session = Restore(rules, RecruitmentFixture(rules, origin));
        CheckIdle(rules, session, "Initial occupied owned castle", 31, false);
        space.HiddenUnit = id;
        try
        {
            var hidden = session.GetTacticalQueries(31);
            Require(hidden.UnitAt(origin) is null && TacticalQueries.UnitAt(session.Snapshot(), Coord(origin)) is not null
                && !hidden.CanOpenRecruitment(origin), "Visibility may hide an occupant, but recruitment must still inspect the complete battlefield.");
            CheckIdle(rules, session, "Hidden occupant still blocks the castle", 31, false);
            CheckIdle(rules, Restore(rules, session.Snapshot()), "Cold hint with hidden occupant", 32, false);
        }
        finally { space.HiddenUnit = null; }
        var queries = session.GetTacticalQueries(31);
        var path = queries.MoveStops(id).Where(at => at != origin).Select(at => queries.PathTo(id, at))
            .First(value => value is { Count: > 1 })!;
        Wait(session, id, path);
        Require(session.Revision == 1 && TacticalQueries.UnitAt(session.Snapshot(), Coord(origin)) is null,
            "A real canonical move-and-wait must vacate the initial castle.");
        CheckIdle(rules, session, "A cached false becomes true after moving away", 31, true);
        Require(session.Undo(session.Revision).Accepted, "The move-and-wait must be undoable.");
        CheckIdle(rules, session, "Undo restores the occupying unit", 31, false);
    }

    private static void CheckTurnAndPhaseChanges(BattleSessionRules rules)
    {
        var session = Restore(rules, RecruitmentFixture(rules, new(0, 0)));
        CheckIdle(rules, session, "Human turn before handoff", 41, true);
        Require(session.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), session.Revision).Accepted
            && session.IsComputerTurn, "The detached C01-03 fixture must hand off to its real AI player.");
        var computer = CheckIdle(rules, session, "AI handoff replaces warmed recruitment guidance", 41, false);
        Require(computer.Hint == rules.Presentation.Messages.Format("ui.hud.computer_thinking"), "AI guidance cannot reuse the human idle HUD.");

        var state = RecruitmentFixture(rules, new(0, 0));
        var owner = state["currentPlayer"]!.GetValue<int>();
        var unit = state["units"]!.AsArray().OfType<JsonObject>().First(value => value["owner"]!.GetValue<int>() == owner);
        state["scenario"]!["zones"]!["recruitment-fixture"] = new JsonArray(TacticalQueries.CoordinateJson(TacticalQueries.Position(unit)));
        state["phase"] = "deployment";
        state["deployment"] = new JsonObject
        {
            ["order"] = new JsonArray(owner),
            ["currentIndex"] = 0,
            ["assignments"] = new JsonArray(new JsonObject
            {
                ["player"] = owner,
                ["zone"] = "recruitment-fixture",
                ["unitIds"] = new JsonArray(unit["id"]!.DeepClone())
            })
        };
        var deploying = Restore(rules, state);
        CheckIdle(rules, deploying, "Deployment cannot open recruitment", 42, false);
        var control = new TacticalSelectionController(deploying.GetTacticalQueries(42));
        var deploymentHud = deploying.ProjectHud(control.Stamp, control.Selection, null, null, false);
        Require(deploymentHud.Hint == rules.Presentation.Messages.Format("ui.hud.selection.deploying")
            && deploymentHud.Sections.Any(section => section.Key == "deployment"), "Deployment uses its own fresh HUD rather than an idle projection.");
        Require(deploying.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "finishDeployment" }), deploying.Revision).Accepted,
            "The admitted deployment fixture must enter play through its canonical action.");
        CheckIdle(rules, deploying, "Deployment-to-playing invalidates cached false", 43, true);

        state = RecruitmentFixture(rules, new(0, 0));
        state["phase"] = "over";
        state["conclusion"] = BattleConclusion.TurnLimitReached().ToJson();
        var concluded = Restore(rules, state);
        var terminal = CheckIdle(rules, concluded, "Concluded save is isolated from a warmed playing session", 43, false);
        Require(terminal.Hint == rules.Presentation.Messages.Format("ui.hud.battle_over"), "Terminal guidance must not reuse recruitment guidance.");
    }

    private static void CheckReadPostconditions(BattleSessionRules rules, RecordingSpace space)
    {
        foreach (var warm in new[] { false, true })
        {
            var session = Restore(rules, RecruitmentFixture(rules, new(0, 0)));
            var queries = session.GetTacticalQueries(51);
            var id = queries.Actors()[0];
            var origin = queries.Unit(id)!.At;
            if (warm)
                CheckIdle(rules, session, "Warm answer before reentrant projection", 51, true);
            space.AfterVisibility = () => Wait(session, id, new[] { origin });
            try
            {
                ExpectInvariant(() => session.ProjectHud(new(51, 0), new(), null, null, false), "HUD projection");
                Require(session.Revision == 1, "The HUD postcondition witness must successfully commit inside the composed visibility policy.");
                ExpectInvariant(() => queries.CanOpenRecruitment(new(0, 0)), "stale");
            }
            finally { space.AfterVisibility = null; }
            CheckIdle(rules, session, "Rejected projection cannot poison the current revision", 52, true);
        }
        var constructing = Restore(rules, RecruitmentFixture(rules, new(0, 0)));
        var actor = constructing.GetTacticalQueries(0).Actors()[0];
        var at = constructing.GetTacticalQueries(0).Unit(actor)!.At;
        space.AfterVisibility = () => Wait(constructing, actor, new[] { at });
        try
        {
            ExpectInvariant(() => constructing.GetTacticalQueries(0), "stale");
            Require(constructing.Revision == 1, "Query construction must preserve its revision postcondition around extension callbacks.");
        }
        finally { space.AfterVisibility = null; }
    }

    private static TacticalHudProjection CheckIdle(BattleSessionRules rules, GrayFlagSession session, string label, long binding, bool? available = null)
    {
        var before = Capture(session);
        var queries = session.GetTacticalQueries(binding);
        // The oracle is a fresh canonical query, never a second implementation of recruitment rules.
        var canRecruit = queries.InteractiveCells.Any(queries.CanOpenRecruitment);
        Require(available is null || canRecruit == available, $"{label}: the fixture must have the intended canonical availability.");
        var key = queries.Phase == "over" ? "ui.hud.battle_over" : !queries.HumanInput ? "ui.hud.computer_thinking"
            : canRecruit ? "ui.hud.selection.idle_recruit" : "ui.hud.selection.idle";
        var stamp = new TacticalStamp(binding, session.Revision);
        var hud = session.ProjectHud(stamp, new(), null, null, false);
        Require(hud.Hint == rules.Presentation.Messages.Format(key) && !hud.Busy && hud.Portrait is null,
            $"{label}: idle guidance must match fresh canonical recruitment queries and current projection arguments.");
        AssertStamp(hud, stamp);
        Require(Capture(session) == before, $"{label}: queries and HUD projection must preserve state, revision, undo and ordered logs.");
        return hud;
    }

    private static JsonObject Level() => PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
        .Single(value => value["id"]!.GetValue<string>() == "c01-03");

    private static JsonObject RecruitmentFixture(BattleSessionRules rules, BoardCell site)
    {
        // Detached boundary fixture, deliberately not an assertion about any shipped campaign site.
        var state = rules.Engine.CreateState(Level());
        var index = rules.Engine.Space.Board(state).IndexOf(Coord(site));
        state["map"]!["tiles"]![index] = "castle";
        state["map"]!["owners"]![index] = state["currentPlayer"]!.DeepClone();
        TacticalQueries.Player(state, state["currentPlayer"]!.GetValue<int>())["resources"]!["funds"]!["current"] = 1000;
        return state;
    }

    private static GrayFlagSession Restore(BattleSessionRules rules, JsonObject state) => GrayFlagSession.FromSave(rules, rules.Engine.SaveBattle(state));

    private static TacticalRequest RecruitRequest(GrayFlagSession session, BoardCell site, string unit)
    {
        var controller = new TacticalSelectionController(session.GetTacticalQueries(21));
        Require(controller.Click(controller.Stamp, TacticalHit.Object(site)).Request is null && controller.Selection.Kind == TacticalSelectionKind.Recruit,
            "The canonical controller must open recruitment before issuing a purchase.");
        var request = controller.Recruit(controller.Stamp, unit).Request;
        Require(request?.Action is not null, "The existing controller must produce the canonical recruitment action.");
        return request!;
    }

    private static void Wait(GrayFlagSession session, int id, IReadOnlyList<BoardCell> path) => Require(session.Dispatch(EcmaJson.ToElement(new JsonObject
    {
        ["kind"] = "command",
        ["unit"] = id,
        ["path"] = TacticalQueries.PathJson(path.Select(Coord)),
        ["command"] = new JsonObject { ["ability"] = "wait" }
    }), session.Revision).Accepted, "The fixture must accept a canonical path followed by wait.");

    private sealed record ReadState(string State, long Revision, bool CanUndo, string Actions, string Events);
    private static ReadState Capture(GrayFlagSession session) => new(EcmaJson.Stringify(session.Snapshot()), session.Revision, session.CanUndo,
        string.Join("\n", session.ActionLog.Select(value => EcmaJson.Stringify(value))), string.Join("\n", session.Events.Select(value => EcmaJson.Stringify(value))));
    private static Coord Coord(BoardCell at) => new(at.X, at.Y);
    private static void AssertStamp(TacticalHudProjection hud, TacticalStamp expected) => Require(hud.Stamp == expected
        && hud.Sections.SelectMany(section => section.Rows).All(row => row.Intent is null || row.Intent.Stamp == expected),
        "A reused recruitment answer must never reuse a HUD or intent stamp from another binding or revision.");
    private static void ExpectInvariant(Action read, string reason)
    {
        try
        {
            read();
        }
        catch (DomainInvariantException error) when (error.Message.Contains(reason, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException($"Recruitment/HUD reads must reject {reason} with a domain invariant error.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingSpace(TacticalSpaceRules rules) : DefaultTacticalSpace(rules)
    {
        public int? HiddenUnit
        {
            get; set;
        }
        public Action? AfterVisibility
        {
            get; set;
        }
        public override bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null)
        {
            var visible = unit["id"]!.GetValue<int>() != HiddenUnit && base.IsUnitVisible(state, viewer, unit, seen);
            var after = AfterVisibility;
            AfterVisibility = null;
            after?.Invoke();
            return visible;
        }
    }
}
