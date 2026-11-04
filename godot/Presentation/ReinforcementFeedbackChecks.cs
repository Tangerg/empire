#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

/// <summary>Shipped arrivals through real AI, registered scenes and native journal activation.</summary>
public partial class ReinforcementFeedbackChecks : Node
{
    private int assertions;

    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new Vector2I(1280, 720);
            foreach (var levelId in new[] { "c01-03", "c01-10", "c01-12" })
                await CheckLevel(levelId);
            GD.Print($"Arrival feedback passed {assertions} assertions: unchanged C03/C10/C12 commands and receipts, registered scenes, native toast/journal, visibility and save/refresh lifetime. Not GUI acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CheckLevel(string levelId)
    {
        var fixture = PresentationJson.Load<JsonObject>($"res://tests/native/{levelId}-spawn-feedback.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "The witness starts at the actual unchanged level.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(), "Every prefix action is legal and reaches the recorded checkpoint.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        var expected = levelId switch
        {
            "c01-03" => new[] { "灰旗军：灰旗卫士进入战场", "灰旗军：工兵进入战场", "灰旗军：边境弓手进入战场" },
            "c01-10" => new[] { "敌对部队：王国骑士进入战场", "敌对部队：军团盾卫进入战场", "敌对部队：边境弓手进入战场" },
            "c01-12" => new[] { "敌对部队：失名幽魂进入战场", "敌对部队：失名幽魂进入战场" },
            _ => throw new InvalidOperationException("Unexpected formal witness.")
        };
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            var scene = AuthoredScene.Load($"res://data/scenes/{levelId}.json");
            var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
            void Bind(GrayFlagSession active)
            {
                screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), active,
                    new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
                screen.ConfigureGround(rendering.Ground);
                screen.ConfigureDynamicState();
            }
            Bind(session);
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new(1280, 720);
            await Layout();
            var map = (Control)screen.FindChild("MapArea", true, false);
            var world = map.GetNode<Node2D>("World");
            var initialCamera = world.Transform;
            var presented = new List<int>();
            var announced = new List<string>();
            void OnEvent(BattlePresentationTransition receipt, int index)
            {
                presented.Add(index);
                if (receipt.Events[index].GetProperty("type").ValueEquals("unitSpawned"))
                {
                    var toast = (Label)screen.FindChild("BattleEventLog", true, false);
                    Require(toast.IsVisibleInTree() && toast.Text == expected[announced.Count]
                        && receipt.EventDescriptions[index] == toast.Text,
                        "The arrival toast is delivered from its own completed event with the authored owner and unit name.");
                    announced.Add(toast.Text);
                }
                else if (announced.Count == 0)
                    Require(!HasArrival(screen), "Earlier events cannot preannounce a later arrival in the same batch.");
            }
            screen.Board.EventPresented += OnEvent;
            Require(!HasArrival(screen), "Loading the real checkpoint cannot announce an uncommitted arrival.");
            Require(session.IsComputerTurn && session.AdvanceComputerStep() is { Committed: true }, "Actual enemy AI must produce the final end-turn.");
            screen.Refresh();
            Require(!HasArrival(screen), "Committing the turn cannot skip its controlled playback and announce future events.");
            var receipt = session.GetSnapshot().Transition!;
            Require(EcmaJson.DeepEquals(session.ActionLog.Single(), fixture["action"])
                && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>(),
                "Actual action, state and all ordered events remain exactly equal to the original witness.");
            await Settle(screen);
            Require(presented.SequenceEqual(Enumerable.Range(0, receipt.Events.Count)) && announced.SequenceEqual(expected),
                "Every real event and arrival announcement completes exactly once and in order.");
            Require(world.Transform == initialCamera && screen.Board.RenderedUnitCount == receipt.After.GetProperty("units").GetArrayLength(),
                "Text feedback cannot move the camera or replace the actual settled unit projection.");
            var indices = receipt.Events.Select((value, index) => (value, index)).Where(pair => pair.value.GetProperty("type").ValueEquals("unitSpawned"))
                .Select(pair => pair.index).ToArray();
            foreach (var index in indices)
            {
                var value = receipt.Events[index];
                var at = value.GetProperty("at");
                var point = world.GetGlobalTransformWithCanvas() * screen.Board.Projection.CellCenter(new Coord(at.GetProperty("x").GetInt32(), at.GetProperty("y").GetInt32()));
                GD.Print($"SPAWN_NATIVE level={levelId} unit={value.GetProperty("unit")} owner={value.GetProperty("owner")} screen={point} inMap={map.GetGlobalRect().HasPoint(point)} description=[{receipt.EventDescriptions[index]}]");
            }
            var journalText = await OpenJournal(screen);
            var expectedJournal = levelId == "c01-12" ? new[] { expected[0] + " ×2" } : expected;
            Require(receipt.EventDescriptions.Count == receipt.Events.Count
                && expectedJournal.All(line => screen.RecentMessages.Count(value => value == line) == 1
                    && journalText.Split("\n\n", StringSplitOptions.None).Count(value => value == line) == 1),
                "The native journal records every arrival once, including the existing consecutive-run folding for identical ghosts.");
            GD.Print($"SPAWN_JOURNAL level={levelId} [{journalText}] renderedUnits={screen.Board.RenderedUnitCount} state={StateDigest.Hash(session.Snapshot())}");
            if (levelId == "c01-03")
                CheckVisibility(session, presentation, receipt, indices[0], expected[0]);
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            Require(!screen.IsPresenting && presented.Count == receipt.Events.Count && announced.Count == expected.Length
                && screen.RecentMessages.SequenceEqual(messages) && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>(),
                "Refreshing an already settled receipt cannot replay arrivals or change the battle.");
            screen.Board.EventPresented -= OnEvent;
            var restored = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), session.Save(),
                new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()));
            Require(EcmaJson.DeepEquals(session.Snapshot(), restored.Snapshot()) && restored.GetSnapshot().Transition is null,
                "Strict save/load preserves the real spawned units without manufacturing a playback receipt.");
            Bind(restored);
            screen.Refresh();
            Require(!screen.IsPresenting && !HasArrival(screen) && restored.Events.Count == session.Events.Count,
                "Loaded historical events cannot be announced again when their screen is bound.");
            if (levelId == "c01-12")
            {
                var fogBefore = battle.Snapshot();
                fogBefore["rules"]!["fog"] = true;
                var fogSession = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(fogBefore));
                Bind(fogSession);
                Require(fogSession.IsComputerTurn && fogSession.AdvanceComputerStep() is { Committed: true },
                    "The explicitly fog-only boundary still executes the same real AI end-turn.");
                screen.Refresh();
                var fogReceipt = fogSession.GetSnapshot().Transition!;
                var expectedAfter = EcmaJson.Parse(receipt.After.GetRawText())!.AsObject();
                expectedAfter["rules"]!["fog"] = true;
                Require(EcmaJson.DeepEquals(fogSession.ActionLog.Single(), fixture["action"])
                    && EcmaJson.DeepEquals(fogSession.Snapshot(), expectedAfter)
                    && EcmaJson.Sha256(new JsonArray(fogSession.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>(),
                    "The visibility variant changes only fog, preserving real births, positions, rules outcomes and ordered events.");
                var visible = fogSession.GetSnapshot().VisibleUnitIds!;
                var hiddenCount = indices.Count(index => !visible.Contains(fogReceipt.Events[index].GetProperty("unit").GetInt32()));
                Require(hiddenCount > 0 && indices.All(index => (fogReceipt.EventDescriptions[index].Length > 0)
                    == visible.Contains(fogReceipt.Events[index].GetProperty("unit").GetInt32())),
                    "Actual application visibility must suppress genuinely hidden arrivals while retaining every event slot.");
                await Settle(screen);
                var fogJournal = await OpenJournal(screen);
                var shownCount = indices.Length - hiddenCount;
                var visibleLine = shownCount == 2 ? expected[0] + " ×2" : expected[0];
                Require(screen.Board.RenderedUnitCount == visible.Count && (shownCount == 0
                    ? !HasArrival(screen) && !fogJournal.Contains("进入战场", StringComparison.Ordinal)
                    : screen.RecentMessages.Count(line => line == visibleLine) == 1),
                    "Hidden arrivals neither create a visible actor nor leak through the real journal or duplicate folding.");
                GD.Print($"SPAWN_FOG_BOUNDARY hidden={hiddenCount}/{indices.Length} shown={shownCount} state={StateDigest.Hash(fogSession.Snapshot())}");
            }
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckVisibility(GrayFlagSession session, SessionPresentationText presentation,
        BattlePresentationTransition receipt, int index, string expected)
    {
        var id = receipt.Events[index].GetProperty("unit").GetInt32();
        var names = new BattleLogNames(session.UnitName, presentation.Content.UnitName, presentation.Content.CareerName,
            presentation.Content.ResourceName, player => presentation.Battle.PlayerName("c01-03", player));
        var visible = new HashSet<int> { id };
        var hidden = new HashSet<int>();
        IReadOnlySet<int>?[] boundaries = { null, visible, hidden };
        foreach (var before in boundaries)
            foreach (var after in boundaries)
            {
                var context = new BattleLogContext(names, receipt.Before, receipt.After, before, after);
                var descriptions = presentation.Events.DescribeAll(context, receipt.Events);
                Require(descriptions.Count == receipt.Events.Count && descriptions[index] == (after == hidden ? "" : expected),
                    "Synthetic visibility boundaries use after visibility for a birth, not the necessarily absent before identity.");
                Require(context.CanObserveDeparture(id) == (before != hidden) && context.CanObserveUnit(id) == (before != hidden && after != hidden),
                    "Arrival handling cannot alter departure or continuing-unit visibility contracts.");
            }
        foreach (var initiallyVisible in new[] { true, false })
        {
            var mutableAfter = new HashSet<int>(initiallyVisible ? new[] { id } : Array.Empty<int>());
            var frozen = new BattleLogContext(names, receipt.Before, receipt.After, hidden, mutableAfter);
            if (initiallyVisible)
                mutableAfter.Clear();
            else
                mutableAfter.Add(id);
            Require(presentation.Events.DescribeAll(frozen, receipt.Events)[index] == (initiallyVisible ? expected : ""),
                "The receipt freezes arrival visibility before later caller mutation.");
        }
        var unknown = EcmaJson.Parse(receipt.Events[index].GetRawText())!.AsObject();
        unknown["unit"] = int.MaxValue;
        Require(presentation.Events.Describe(new(names, receipt.Before, receipt.After, afterVisible: hidden), EcmaJson.ToElement(unknown)) == "",
            "The isolated hidden formatter boundary filters an unavailable private identity before resolving its name.");
        var rejected = false;
        try
        {
            presentation.Events.Describe(new(names, receipt.Before, receipt.After), EcmaJson.ToElement(unknown));
        }
        catch (LocalizationException) { rejected = true; }
        Require(rejected, "An observable event without an observed creation identity remains an explicit error.");
    }

    private async Task<string> OpenJournal(BattleScreen screen)
    {
        ((Button)screen.FindChild("BattleLogButton", true, false)).GrabFocus();
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
        await Layout();
        var dialog = (AcceptDialog)screen.FindChild("BattleLogDialog", true, false);
        var journal = (RichTextLabel)screen.FindChild("BattleLogText", true, false);
        Require(dialog.Visible && journal.IsVisibleInTree() && !journal.BbcodeEnabled, "Native activation opens the actual plain-text player journal.");
        var result = journal.Text;
        dialog.Hide();
        return result;
    }

    private static bool HasArrival(BattleScreen screen) => screen.RecentMessages.Any(line => line.Contains("进入战场", StringComparison.Ordinal));

    private async Task Settle(BattleScreen screen)
    {
        for (var tick = 0; screen.IsPresenting && tick < 500; tick++)
        {
            screen.Board.AdvancePresentation(0.05);
            await Task.Yield();
        }
        Require(!screen.IsPresenting && screen.Board.GetNodeOrNull("BattleEffects") is null, "Bounded playback settles without adding or leaking an arrival effect.");
        await Layout();
    }

    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
