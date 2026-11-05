#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Orders;
using Empire.Core.Tactics;
using Empire.Presentation.Feedback;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private void CheckTacticProjectionBoundaries(BattleEngine engine, SessionPresentationText presentation, GrayFlagSession session)
    {
        const string commander = "laiya-command";
        const string tacticId = "c01.gray-rally";
        var state = session.Snapshot();
        var before = EcmaJson.Stringify(state);
        var beforeRevision = session.Revision;
        var beforeEvents = EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()));
        var beforeActions = EcmaJson.Stringify(new JsonArray(session.ActionLog.Select(value => (JsonNode)value.DeepClone()).ToArray()));
        var control = new TacticalSelectionController(session.GetTacticalQueries(81));
        var option = engine.Support.Tactics(state, commander).Single(value => value.Id == tacticId);
        var plans = option.Targets.Where(at => control.InteractiveCells.Contains(new(at.X, at.Y)))
            .Select(at => engine.Support.PlanTactic(state, commander, tacticId, at)!).ToArray();
        var plan = plans.OrderByDescending(value => value.AlliedUnitIds.Count).First();
        Require(plan.AlliedUnitIds.Count > 0, "The unchanged formal rally witness admits allied recipients.");
        var legal = TacticalHit.Ground(new(plan.Target.X, plan.Target.Y));
        var invalidCell = control.InteractiveCells.First(at => !option.Targets.Contains(new Coord(at.X, at.Y)));
        var invalid = TacticalHit.Ground(invalidCell);
        var effects = option.Effects.Select(presentation.TacticEffects.Describe).ToArray();
        var text = presentation.Messages;
        string Number(double value) => EcmaJson.Stringify(JsonValue.Create(value));
        var costs = string.Join(" · ", option.Costs.Select(cost => $"{presentation.Content.ResourceName(cost.Resource)} {Number(cost.Amount)}"));
        TacticalHudProjection Hud() => session.ProjectHud(control.Stamp, control.Selection, control.CursorHit, control.InspectId, control.Busy);
        static TacticalHudSection Section(TacticalHudProjection hud, string key) => hud.Sections.Single(section => section.Key == key);
        static TacticalHudRow Row(TacticalHudProjection hud, string section, string key) => Section(hud, section).Rows.Single(row => row.Key == key);
        static bool HasForecast(TacticalHudProjection hud) => hud.Sections.Any(section => section.Key is "forecast" or "forecast-details");
        void Arm()
        {
            control.Bind(session.GetTacticalQueries(81));
            var result = control.ChooseTactic(control.Stamp, commander + ":" + tacticId);
            Require(result.Status == TacticalInputStatus.Ready && result.Request is null && control.Selection.Kind == TacticalSelectionKind.Tactic,
                "The real available rally composes a target without dispatching an order.");
        }
        void CheckNoCenter(TacticalHudProjection hud)
        {
            Require(Row(hud, "forecast", "tactic-allies").Label == text.Format("ui.hud.tactic.choose_center")
                && Section(hud, "forecast-details").Rows.All(row => row.Key != "tactic-center" && !row.Key.StartsWith("tactic-ally:", StringComparison.Ordinal)),
                "Without a legal center, tactic effects remain readable but no recipients or center are invented.");
        }
        void CheckPlan(TacticalHudProjection hud, TacticPlan expected)
        {
            var queries = session.GetTacticalQueries(control.Stamp.Binding);
            var visibleAllies = expected.AlliedUnitIds.Where(id => queries.Unit(id) is { Visible: true }).ToArray();
            var allyRows = Section(hud, "forecast-details").Rows.Where(row => row.Key.StartsWith("tactic-ally:", StringComparison.Ordinal)).ToArray();
            Require(allyRows.Select(row => row.Key).SequenceEqual(visibleAllies.Select(id => $"tactic-ally:{id}"))
                && allyRows.All(row => row.UnitIcon is not null && row.Label.Length > 0),
                "Tactic recipient rows follow the authoritative plan and actual visibility, with named portraits.");
            var centerText = text.Format("ui.hud.tactic.allies", expected.Target.X, expected.Target.Y, visibleAllies.Length);
            Require(Row(hud, "forecast", "tactic-allies").Label == centerText
                && Row(hud, "forecast-details", "tactic-center").Label == centerText,
                "Compact and detailed recipient counts describe the same planned center.");
            var enemies = state["units"]!.AsArray().OfType<JsonObject>()
                .Where(unit => TacticalQueries.AreEnemies(state, unit["owner"]!.GetValue<int>(), state["currentPlayer"]!.GetValue<int>()))
                .Select(unit => $"tactic-ally:{unit["id"]!.GetValue<int>()}").ToArray();
            Require(enemies.Length > 0 && allyRows.All(row => !enemies.Contains(row.Key)),
                "Actual opposing units never appear in the rally's allied recipient list.");
        }

        var choice = Section(Hud(), "tactics").Rows.Single(row => row.Intent?.Key == commander + ":" + tacticId);
        Require(effects.All(effect => choice.Hint.Contains(effect.Detail, StringComparison.Ordinal)) && choice.Hint.Contains(costs, StringComparison.Ordinal),
            "The real tactic choice discloses its planned effects and resource costs before arming.");
        Arm();
        Require(engine.Support.PlanTactic(state, commander, tacticId, null) is null
            && engine.Support.PlanTactic(state, commander, tacticId, new(invalidCell.X, invalidCell.Y)) is null,
            "The authoritative planner rejects missing and out-of-range tile centers.");
        CheckNoCenter(Hud());
        var armed = Hud();
        Require(Section(armed, "forecast").Title == presentation.Content.Name(ContentTextKind.Tactic, tacticId)
            && Row(armed, "forecast", "tactic-area").Label == text.Format("ui.hud.tactic.area_cost", Number(option.Radius), costs)
            && Row(armed, "forecast-details", "tactic-range").Label == text.Format("ui.hud.tactic.range", Number(option.Range))
            && Row(armed, "forecast-details", "tactic-cost").Label == costs,
            "Armed rally keeps its identity, authoritative range, radius and costs visible.");
        Require(Row(armed, "forecast", "tactic-effects").Label == string.Join(" · ", effects.Select(effect => effect.Compact))
            && effects.Select((effect, index) => Row(armed, "forecast-details", $"tactic-effect:{index}").Label == effect.Detail).All(value => value),
            "Compact and detailed descriptions preserve the registered planned status effects and duration.");
        control.Hover(control.Stamp, invalid);
        CheckNoCenter(Hud());
        Require(control.Click(control.Stamp, invalid).Status == TacticalInputStatus.InvalidTarget
            && control.Key(control.Stamp, "enter").Request is null, "An invalid center cannot dispatch a tactic.");

        control.Hover(control.Stamp, legal);
        CheckPlan(Hud(), plan);
        var legalRequest = control.Click(control.Stamp, legal).Request;
        Require(legalRequest?.Action is { } action && action.GetProperty("kind").ValueEquals("tactic")
            && action.GetProperty("commander").ValueEquals(commander) && action.GetProperty("tactic").ValueEquals(tacticId)
            && action.GetProperty("target").GetProperty("x").GetInt32() == plan.Target.X
            && action.GetProperty("target").GetProperty("y").GetInt32() == plan.Target.Y,
            "A legal live center still produces the exact canonical tactic request; this boundary check does not dispatch it.");
        var held = Hud();
        control.Hover(control.Stamp, invalid);
        Require(control.Selection.PreviewTarget == legal && Hud().Hint == text.Format("ui.hud.selection.preview_held")
            && Section(Hud(), "forecast").Rows.SequenceEqual(Section(held, "forecast").Rows)
            && control.Key(control.Stamp, "enter").Request is null && control.Click(control.Stamp, invalid).Request is null,
            "A held legal preview never authorizes the invalid live cursor to commit.");
        control.Leave(control.Stamp);
        Require(control.CursorHit is null && control.Selection.PreviewTarget is null, "True leave clears tactic target ownership.");
        CheckNoCenter(Hud());

        control.Hover(control.Stamp, legal);
        control.SetBusy(true);
        Require(!HasForecast(Hud()) && control.Overlay().Helpful.Count == 0
            && control.Key(control.Stamp, "enter").Status == TacticalInputStatus.Busy
            && control.Click(control.Stamp, legal).Request is null,
            "Busy presentation suppresses the tactic forecast and target painting and cannot submit the held order.");
        control.SetBusy(false);
        CheckPlan(Hud(), plan);
        control.Back(control.Stamp);
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.Selection.PreviewTarget is null && !HasForecast(Hud()),
            "Cancel removes the tactic forecast and held center.");

        Arm();
        control.Hover(control.Stamp, legal);
        var oldStamp = control.Stamp;
        control.Bind(session.GetTacticalQueries(82));
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.CursorHit is null && !HasForecast(Hud())
            && control.Click(oldStamp, legal).Status == TacticalInputStatus.StaleBinding,
            "Rebinding clears previews and rejects callbacks carrying the former binding.");
        Require(before == EcmaJson.Stringify(session.Snapshot()) && beforeRevision == session.Revision
            && beforeEvents == EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
            && beforeActions == EcmaJson.Stringify(new JsonArray(session.ActionLog.Select(value => (JsonNode)value.DeepClone()).ToArray())),
            "Planning, projecting and canceling tactics cannot alter canonical state, revision, events or action history.");
    }
}
