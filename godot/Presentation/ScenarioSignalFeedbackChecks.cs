#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

/// <summary>Shipped White River turns with native human input, real AI and controlled animation time.</summary>
public partial class ScenarioSignalFeedbackChecks : Node
{
    private int assertions;

    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new Vector2I(1280, 720);
            await CheckWhiteRiver();
            CheckDeclaredSignalText();
            GD.Print($"Scenario signal feedback passed {assertions} assertions: unchanged C06 native retreat/end-turn orders, real AI, controlled playback, journal/load lifetime, and explicitly synthetic declared-signal text boundaries. Not GUI acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CheckWhiteRiver()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-06");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var opening = session.Snapshot();
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-06.json"));
            var missing = new List<string>();
            var seen = new List<string>();
            var movedTurn = 0;
            var completed = new List<(long Revision, int Index)>();
            var announced = new List<string>();
            screen.Board.EventPresented += (receipt, index) =>
            {
                completed.Add((receipt.Revision, index));
                if (receipt.Events[index].GetProperty("type").ValueEquals("scenarioSignal"))
                {
                    var expected = presentation.Battle.SignalName("c01-06", receipt.Events[index].GetProperty("signal").GetString()!);
                    var toast = (Label)screen.FindChild("BattleEventLog", true, false);
                    if (toast.IsVisibleInTree() && toast.Text == expected && screen.RecentMessages.Last() == expected)
                        announced.Add(expected);
                }
            };
            Require(StateDigest.Hash(opening) == "142efa8b" && !opening["rules"]!["fog"]!.GetValue<bool>(),
                "This real-play witness starts from the unchanged public, fog-off C06 battle.");
            var labels = new[] { "白河追兵抵达", "白河桥受损" };
            Require(labels.All(label => !screen.RecentMessages.Contains(label)), "No warning may be announced before its event commits.");
            void Press(Key key)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            }
            GD.Print($"SCENARIO_OPENING hash={StateDigest.Hash(session.Snapshot())}");
            for (var step = 0; step < 250 && seen.Count < 2; step++)
            {
                completed.Clear();
                var beforeRevision = session.Revision;
                Require(!session.IsConcluded, "The actual White River battle must survive to both warnings without editing its state.");
                if (session.IsComputerTurn)
                {
                    Require(session.AdvanceComputerStep() is { Committed: true }, "The actual AI must commit a legal step.");
                    screen.Refresh();
                }
                else
                {
                    GetViewport().GuiReleaseFocus();
                    var state = session.Snapshot();
                    var turn = state["turn"]!.GetValue<int>();
                    if (movedTurn != turn)
                    {
                        var actor = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "campaign-laiya");
                        var id = actor["id"]!.GetValue<int>();
                        var destination = session.GetTacticalQueries(0).MoveStops(id).OrderBy(cell => cell.Y).ThenBy(cell => Math.Abs(cell.X - 27)).ThenBy(cell => cell.X).First();
                        for (var cycle = 0; screen.SelectedUnitId != id && cycle < 20; cycle++)
                            Press(Key.Space);
                        Require(screen.SelectedUnitId == id, "Native cycling must select Laiya for a legal retreat.");
                        var dx = destination.X - actor["x"]!.GetValue<int>();
                        var dy = destination.Y - actor["y"]!.GetValue<int>();
                        for (var index = 0; index < Math.Abs(dx); index++)
                            Press(dx > 0 ? Key.Right : Key.Left);
                        for (var index = 0; index < Math.Abs(dy); index++)
                            Press(dy > 0 ? Key.Down : Key.Up);
                        Press(Key.Enter);
                        Press(Key.W);
                        movedTurn = turn;
                    }
                    else
                        Press(Key.E);
                }
                var transition = session.GetSnapshot().Transition!;
                Require(session.Revision == beforeRevision + 1 && transition.Revision == session.Revision && screen.IsPresenting, "A real committed order starts presentation.");
                for (var tick = 0; screen.IsPresenting && tick < 1000; tick++)
                {
                    screen.Board.AdvancePresentation(0.05);
                    await Task.Yield();
                }
                Require(!screen.IsPresenting && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count).Select(index => (transition.Revision, index))),
                    "Every committed event must complete once in order and bounded playback must settle.");
                var messages = screen.RecentMessages.ToArray();
                var published = completed.ToArray();
                screen.Refresh();
                screen.Refresh();
                Require(screen.RecentMessages.SequenceEqual(messages) && completed.SequenceEqual(published) && session.Revision == transition.Revision,
                    "Refreshing a settled receipt must not repeat its feedback or commit another order.");
                foreach (var (value, index) in transition.Events.Select((value, index) => (value, index)))
                {
                    if (!value.GetProperty("type").ValueEquals("scenarioSignal"))
                        continue;
                    var id = value.GetProperty("signal").GetString()!;
                    var expected = presentation.Battle.SignalName("c01-06", id);
                    seen.Add(id);
                    var entry = (Button)screen.FindChild("BattleLogButton", true, false);
                    entry.GrabFocus();
                    Press(Key.Space);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var dialog = (AcceptDialog)screen.FindChild("BattleLogDialog", true, false);
                    var journal = (RichTextLabel)screen.FindChild("BattleLogText", true, false);
                    Require(dialog.Visible && journal.IsVisibleInTree(), "A native focused activation must open the real player journal.");
                    var shown = screen.RecentMessages.Count(line => line == expected) == 1
                        && journal.Text.Split("\n\n", StringSplitOptions.None).Count(line => line == expected) == 1;
                    Require(!journal.BbcodeEnabled, "The journal renders authored descriptions as plain text.");
                    dialog.Hide();
                    GD.Print($"SCENARIO_SIGNAL turn={session.Snapshot()["turn"]} revision={session.Revision} id={id} description={transition.EventDescriptions[index]} journal={shown} hash={StateDigest.Hash(session.Snapshot())} events={EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))}");
                    var expectedHash = seen.Count == 1 ? "50ded0d0" : "9edccbb1";
                    var expectedEvents = seen.Count == 1 ? "ca903dea6f5a22882a213693b7bff1d59d496acdc0d4b289b30edacc5bb6674f"
                        : "851f9e5c794948c43ac07a9a80a4e02c76beec2d186ab85530325e0b0230b528";
                    Require(StateDigest.Hash(session.Snapshot()) == expectedHash
                        && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == expectedEvents,
                        "The signal feedback must preserve the current native state and ordered canonical events exactly.");
                    Require(transition.Before.GetProperty("map").GetRawText() == transition.After.GetProperty("map").GetRawText()
                        && transition.Before.GetProperty("structures").GetRawText() == transition.After.GetProperty("structures").GetRawText(),
                        "Neither warning may invent terrain or structure damage.");
                    if (seen.Count == 1)
                    {
                        Require(!screen.RecentMessages.Contains(labels[1]), "The T4 pursuit must not preannounce the later bridge warning.");
                        var restored = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), session.Save(),
                            new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()));
                        Require(EcmaJson.DeepEquals(session.Snapshot(), restored.Snapshot()) && restored.GetSnapshot().Transition is null,
                            "The real saved checkpoint reloads exactly without an uncommitted presentation receipt.");
                        var loadedScreen = new BattleScreen();
                        AddChild(loadedScreen);
                        try
                        {
                            loadedScreen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), restored,
                                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                                level, AuthoredScene.Load("res://data/scenes/c01-06.json"));
                            loadedScreen.Refresh();
                            Require(!loadedScreen.IsPresenting && labels.All(label => !loadedScreen.RecentMessages.Contains(label))
                                && restored.Events.Count == session.Events.Count,
                                "Binding a loaded battle keeps its canonical events without replaying old warnings or inventing future ones.");
                        }
                        finally
                        {
                            RemoveChild(loadedScreen);
                            loadedScreen.Free();
                        }
                    }
                    if (transition.EventDescriptions[index] != expected || !shown)
                        missing.Add(id);
                }
            }
            Require(seen.SequenceEqual(new[] { "white_river_pursuit_arrived", "white_river_bridge_damaged" }), "The unchanged native battle must trigger its T4 pursuit and T6 warning exactly once in order.");
            Require(announced.SequenceEqual(labels), "Each authored warning must be shown once as a live toast at its own EventPresented boundary.");
            Require(missing.Count == 0, "Committed scenario warnings are absent from player feedback: " + string.Join(", ", missing));
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckDeclaredSignalText()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        BattleLogContext Context(JsonObject level)
        {
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var state = EcmaJson.ToElement(session.Snapshot());
            return new(new(session.UnitName, presentation.Content.UnitName, presentation.Content.CareerName, presentation.Content.ResourceName,
                player => presentation.Battle.PlayerName(level["id"]!.GetValue<string>(), player)), state, state);
        }
        static JsonElement Signal(string id) => EcmaJson.ToElement(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = id });
        var count = 0;
        foreach (var level in levels.OfType<JsonObject>())
        {
            var id = level["id"]!.GetValue<string>();
            var context = Context(level);
            var signals = level["scenario"]?["triggers"] is JsonArray triggers
                ? triggers.OfType<JsonObject>().SelectMany(trigger => trigger["effects"]!.AsArray().OfType<JsonObject>())
                    .SelectMany(engine.ScenarioEffects.Signals).ToArray()
                : Array.Empty<string>();
            var events = signals.Select(Signal).ToArray();
            var before = context.State.GetRawText();
            var described = presentation.Events.DescribeAll(context, events);
            Require(described.SequenceEqual(signals.Select(signal => presentation.Battle.SignalName(id, signal)))
                && context.State.GetRawText() == before,
                "Every declared signal must use its own level's composed text without advancing its synthetic context.");
            count += signals.Length;
        }
        var whiteRiver = levels.OfType<JsonObject>().Single(level => level["id"]!.GetValue<string>() == "c01-06");
        var whiteContext = Context(whiteRiver);
        foreach (var id in new[] { "undeclared_signal", "found_prisoners" })
        {
            var rejected = false;
            try
            {
                presentation.Events.Describe(whiteContext, Signal(id));
            }
            catch (LocalizationException) { rejected = true; }
            Require(rejected, "Missing or wrong-level signal identity must fail explicitly, not leak an ID or disappear.");
        }
        Require(presentation.Events.Describe(whiteContext, EcmaJson.ToElement(new JsonObject { ["type"] = "unknownEvent" })) == "",
            "A genuinely unknown event type retains the existing silent registry behavior.");
        var metadata = PresentationJson.Load<JsonObject>("res://data/grayflag-battle-text.json");
        metadata["levels"]!["c01-06"]!["signals"]!["white_river_pursuit_arrived"] = new JsonObject { ["literal"] = "Injected pursuit notice" };
        var document = BattleTextDocument.Parse(new(levels, engine.Objectives, engine.ScenarioEffects), EcmaJson.Stringify(metadata));
        var injected = new BattleText(new(text, ObjectiveText.CreateDefault(text).Seal()), document);
        var descriptions = BattleEventText.CreateDefault(new(text, injected, presentation.Content));
        Require(descriptions.Describe(whiteContext, Signal("white_river_pursuit_arrived")) == "Injected pursuit notice",
            "Signal text must come from the injected level metadata, not duplicated labels or global content.");
        Require(descriptions.Clone().Replace("scenarioSignal", (_, _) => "").Seal().Describe(whiteContext, Signal("white_river_pursuit_arrived")) == "",
            "An explicitly composed presenter can suppress a custom signal without a hidden fallback.");
        GD.Print($"SCENARIO_DECLARED_TEXT {count} authored signal declarations across {levels.Count} levels; synthetic descriptions only, not trigger execution.");
    }

    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
