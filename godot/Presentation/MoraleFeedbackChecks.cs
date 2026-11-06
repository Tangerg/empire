#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

/// <summary>Formal morale values and an actual nonlethal AI rout, using native dossier/journal input.</summary>
public partial class MoraleFeedbackChecks : Node
{
    private int assertions;
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new Vector2I(1280, 720);
            await CheckRout();
            await CheckOpening();
            CheckProjectionBoundaries();
            GD.Print($"Morale feedback passed {assertions} assertions: formal values, legal C06 AI rout, native dossier/journal, visibility and save/refresh lifetime. Not GUI acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CheckRout()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-06-routed-feedback.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "The rout witness starts from unchanged formal C06 content.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(), "The unchanged legal prefix reaches the exact recorded checkpoint.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var bindings = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json")["nodes"]!.AsArray().OfType<JsonObject>()
            .Single(node => node["type"]!.GetValue<string>() == "battle" && node["level"]!.GetValue<string>() == "c01-06")["rosterBindings"]!.AsArray();
        var session = GrayFlagSession.FromSave(new(engine, presentation, bindings), engine.SaveBattle(battle.Snapshot()));
        CheckProjectedMorale(session, fixture["routedUnit"]!.GetValue<int>(), 32, 100, "10%");
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-06.json"));
            await Layout();
            var completed = new List<int>();
            var announced = new List<string>();
            screen.Board.EventPresented += (receipt, index) =>
            {
                completed.Add(index);
                var routedIndex = receipt.Events.Select((value, slot) => (value, slot))
                    .Single(pair => pair.value.GetProperty("type").ValueEquals("unitRouted")).slot;
                if (index < routedIndex)
                    Require(!screen.RecentMessages.Any(line => line.Contains("溃退离场", StringComparison.Ordinal)),
                        "A departure cannot be announced by an earlier event in the same committed batch.");
                if (receipt.Events[index].GetProperty("type").ValueEquals("unitRouted"))
                {
                    var toast = (Label)screen.FindChild("BattleEventLog", true, false);
                    Require(toast.IsVisibleInTree() && toast.Text == receipt.EventDescriptions[index]
                        && screen.RecentMessages.Last() == toast.Text,
                        "A committed departure is announced at its own completed event, not before playback.");
                    announced.Add(toast.Text);
                }
            };
            Require(!screen.RecentMessages.Any(line => line.Contains("溃退离场", StringComparison.Ordinal)),
                "The checkpoint cannot announce a rout before its attack commits.");
            Require(session.IsComputerTurn && session.AdvanceComputerStep() is { Committed: true },
                "The genuine default AI must produce and commit the recorded attack.");
            screen.Refresh();
            Require(!screen.RecentMessages.Any(line => line.Contains("溃退离场", StringComparison.Ordinal)),
                "Committing the attack cannot skip ahead of the controlled departure playback.");
            var transition = session.GetSnapshot().Transition!;
            Require(EcmaJson.DeepEquals(session.ActionLog.Single(), fixture["action"])
                && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>(),
                "Native playback must preserve the exact original legal command, canonical state and ordered events.");
            var id = fixture["routedUnit"]!.GetValue<int>();
            var marker = transition.After.GetProperty("markers").EnumerateArray().Single(value => value.GetProperty("fallenUnit").GetProperty("id").GetInt32() == id);
            Require(marker.GetProperty("kind").ValueEquals("routed") && marker.GetProperty("fallenUnit").GetProperty("hp").GetDouble() == 3
                && marker.GetProperty("fallenUnit").GetProperty("morale").GetProperty("current").GetDouble() == 0
                && transition.Events.Single(value => value.GetProperty("type").ValueEquals("attack")).GetProperty("killed").GetBoolean() == false
                && transition.Events.All(value => !value.GetProperty("type").ValueEquals("death")),
                "The formal victim is alive at 3 HP and leaves because morale reaches zero, not because the strike killed it.");
            var index = transition.Events.Select((value, index) => (value, index))
                .Single(pair => pair.value.GetProperty("type").ValueEquals("unitRouted")).index;
            for (var tick = 0; screen.IsPresenting && tick < 1000; tick++)
            {
                screen.Board.AdvancePresentation(0.05);
                await Task.Yield();
            }
            Require(!screen.IsPresenting && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count)),
                "The real committed batch must finish every event once and in order.");
            ((Button)screen.FindChild("BattleLogButton", true, false)).GrabFocus();
            Press(Key.Space);
            await Layout();
            var dialog = (AcceptDialog)screen.FindChild("BattleLogDialog", true, false);
            var journal = (RichTextLabel)screen.FindChild("BattleLogText", true, false);
            Require(dialog.Visible && journal.IsVisibleInTree(), "Native activation opens the actual battle journal.");
            Require(session.UnitName(marker.GetProperty("fallenUnit")) == "托伦",
                "The formal campaign identity survives departure instead of falling back to a generic troop type.");
            var expected = "托伦：士气耗尽，溃退离场";
            GD.Print($"MORALE_ROUT state={StateDigest.Hash(session.Snapshot())} hp=3 morale=0 description={transition.EventDescriptions[index]} journal=[{journal.Text}]");
            Require(transition.EventDescriptions[index] == expected && announced.SequenceEqual(new[] { expected })
                && screen.RecentMessages.Count(line => line == expected) == 1
                && journal.Text.Split("\n\n", StringSplitOptions.None).Count(line => line == expected) == 1,
                "The player must be told why a living visible unit left the battle instead of receiving only a nonlethal strike receipt.");
            dialog.Hide();
            CheckDepartureVisibility(session, presentation, transition, index, expected);
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            Require(completed.Count == transition.Events.Count && announced.Count == 1 && screen.RecentMessages.SequenceEqual(messages)
                && !screen.IsPresenting && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>(),
                "Refreshing a settled receipt cannot replay a departure or advance the battle.");
            var restored = GrayFlagSession.FromSave(new(engine, presentation, bindings), session.Save(),
                new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()));
            Require(EcmaJson.DeepEquals(session.Snapshot(), restored.Snapshot()) && restored.GetSnapshot().Transition is null,
                "Strict save/load preserves the routed marker and does not manufacture a presentation receipt.");
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), restored,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-06.json"));
            screen.Refresh();
            Require(!screen.IsPresenting && !screen.RecentMessages.Contains(expected) && restored.Events.Count == session.Events.Count,
                "Rebinding a loaded battle retains the historical events without announcing its old rout again.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private async Task CheckOpening()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-05");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var before = EcmaJson.Stringify(session.Snapshot());
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-05.json"));
            await Layout();
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(960, 640) })
            {
                GetWindow().Size = size;
                await Layout();
                foreach (var id in new[] { 8, 1 })
                {
                    GetViewport().GuiReleaseFocus();
                    for (var cycle = 0; screen.SelectedUnitId != id && cycle < 20; cycle++)
                        Press(Key.Space);
                    Require(screen.SelectedUnitId == id, "Native cycling reaches the unchanged C05 civilian or commander.");
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    ((Button)hud.FindChild("DossierButton", true, false)).GrabFocus();
                    Press(Key.Space);
                    await Layout();
                    var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
                    var details = (VBoxContainer)hud.FindChild("unit-details", true, false);
                    if (!details.GetNode<Control>("Content").Visible)
                    {
                        details.GetChildren().OfType<Button>().Single().GrabFocus();
                        Press(Key.Space, popup);
                        await Layout();
                    }
                    Require(popup.Visible && details.GetNode<Control>("Content").Visible,
                        "Native keyboard activation opens the real dossier and unit details.");
                    var current = id == 8 ? 80 : 120;
                    var resilience = id == 8 ? "0%" : "35%";
                    CheckProjectedMorale(session, id, current, current, resilience);
                    var labels = details.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).ToArray();
                    var valueLabel = labels.Single(label => label.Text == $"士气 {current} / {current}");
                    var ruleLabel = labels.Single(label => label.Text == $"士气损失减免 {resilience} · 士气耗尽时溃退离场");
                    var bar = details.FindChildren("*", "ProgressBar", true, false).OfType<ProgressBar>().Single();
                    Require(bar.IsVisibleInTree() && bar.Value == current && bar.MaxValue == current && !bar.ShowPercentage,
                        "The native gauge renders the same authoritative morale values as the visible label.");
                    var scroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
                    var clip = scroll.GetGlobalRect();
                    if (scroll.GetVScrollBar().IsVisibleInTree())
                        clip.Size -= new Vector2(scroll.GetVScrollBar().Size.X, 0);
                    Require(ruleLabel.GetLineCount() > 0 && ruleLabel.Size.Y >= ruleLabel.GetMinimumSize().Y
                        && clip.Encloses(ruleLabel.GetGlobalRect()) && clip.Encloses(valueLabel.GetGlobalRect()) && clip.Encloses(bar.GetGlobalRect()),
                        $"Morale labels and gauge fit the real scroll clipping area at both supported widths, with enough native text height: window={size}, unit={id}, clip={clip}, rule={ruleLabel.GetGlobalRect()}, minimum={ruleLabel.GetMinimumSize()}, value={valueLabel.GetGlobalRect()}, gauge={bar.GetGlobalRect()}, scroll={scroll.GetGlobalRect()}, scrollbar={scroll.GetVScrollBar().GetGlobalRect()}, offset={scroll.ScrollVertical}.");
                    GD.Print($"MORALE_DOSSIER size={size} unit={id} text={valueLabel.Text} rule={ruleLabel.Text} lines={ruleLabel.GetLineCount()} hash={StateDigest.Hash(session.Snapshot())}");
                    popup.Hide();
                    await Layout();
                }
            }
            Require(EcmaJson.Stringify(session.Snapshot()) == before && session.Revision == 0 && session.Events.Count == 0,
                "Reading morale cannot advance battle state or create an order.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckProjectedMorale(GrayFlagSession session, int id, double current, double maximum, string resilience)
    {
        var before = EcmaJson.Stringify(session.Snapshot());
        var revision = session.Revision;
        var events = session.Events.Count;
        var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == id);
        var morale = unit["morale"]!.AsObject();
        var rows = session.ProjectHud(new(0, session.Revision), new(), null, id, false).Sections
            .Single(section => section.Key == "unit-details").Rows;
        var row = rows.Single(value => value.Key == "morale");
        Require(morale["current"]!.GetValue<double>() == current && morale["maximum"]!.GetValue<double>() == maximum
            && row.Gauge == new TacticalHudGauge(current, maximum) && row.Label == $"士气 {current} / {maximum}"
            && rows.Single(value => value.Key == "morale-rule").Label == $"士气损失减免 {resilience} · 士气耗尽时溃退离场",
            "Morale projection must match the live instance, including actual depleted values and non-default caps/resilience.");
        Require(EcmaJson.Stringify(session.Snapshot()) == before && session.Revision == revision && session.Events.Count == events,
            "Projecting morale is a read, never another rule transition.");
    }

    private void CheckProjectionBoundaries()
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().ToArray();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        foreach (var levelId in new[] { "c01-01", "c01-04" })
        {
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), levels.Single(value => value["id"]!.GetValue<string>() == levelId));
            var state = session.Snapshot();
            var rows = session.ProjectHud(new(0, 0), new(), null, 1, false).Sections.SelectMany(section => section.Rows).ToArray();
            Require(!state["rules"]!["moraleEnabled"]!.GetValue<bool>() && state["units"]![0]!["morale"] is JsonObject
                && rows.Any(row => row.Key == "hp") && rows.All(row => !row.Key.StartsWith("morale", StringComparison.Ordinal)),
                "Formal disabled-morale levels retain ordinary inspection without claiming that morale can route a unit.");
        }
        var golem = new GrayFlagSession(new(engine, presentation, new JsonArray()), levels.Single(value => value["id"]!.GetValue<string>() == "c01-14"));
        CheckProjectedMorale(golem, 12, 140, 140, "70%");
        var fog = engine.CreateBattle(levels.Single(value => value["id"]!.GetValue<string>() == "c01-06")).Snapshot();
        fog["rules"]!["fog"] = true;
        var hidden = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(fog));
        var visible = hidden.GetSnapshot().VisibleUnitIds!;
        var concealed = fog["units"]!.AsArray().OfType<JsonObject>().First(unit => !visible.Contains(unit["id"]!.GetValue<int>()));
        var id = concealed["id"]!.GetValue<int>();
        var hit = TacticalHit.Unit(new(concealed["x"]!.GetValue<int>(), concealed["y"]!.GetValue<int>()), id);
        var projection = hidden.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Unit,
            UnitId = id
        }, hit, id, false);
        Require(projection.Sections.All(section => section.Key != "unit-details")
            && projection.Sections.SelectMany(section => section.Rows).All(row => !row.Key.StartsWith("morale", StringComparison.Ordinal)),
            "An explicit fog-only boundary cannot reveal hidden morale through selected, inspected or cursor identity.");
    }

    private void CheckDepartureVisibility(GrayFlagSession session, SessionPresentationText presentation,
        BattlePresentationTransition receipt, int index, string expected)
    {
        var id = receipt.Events[index].GetProperty("unit").GetInt32();
        var names = new BattleLogNames(session.UnitName, presentation.Content.UnitName, presentation.Content.CareerName,
            presentation.Content.ResourceName, player => presentation.Battle.PlayerName("c01-06", player));
        var visible = new HashSet<int> { id };
        var hidden = new HashSet<int>();
        IReadOnlySet<int>?[] boundaries = { null, visible, hidden };
        foreach (var before in boundaries)
            foreach (var after in boundaries)
            {
                var context = new BattleLogContext(names, receipt.Before, receipt.After, before, after);
                var descriptions = presentation.Events.DescribeAll(context, receipt.Events);
                Require(descriptions.Count == receipt.Events.Count && descriptions[index] == (before == hidden ? "" : expected),
                    "A departure uses only prior visibility and preserves its event slot even when the actor is absent afterward.");
                Require(context.CanObserveUnit(id) == (before != hidden && after != hidden),
                    "Continuing-unit feedback retains its stricter both-boundaries visibility contract.");
            }
        var mutableBefore = new HashSet<int> { id };
        var mutableAfter = new HashSet<int>();
        var frozen = new BattleLogContext(names, receipt.Before, receipt.After, mutableBefore, mutableAfter);
        mutableBefore.Clear();
        mutableAfter.Add(id);
        Require(presentation.Events.DescribeAll(frozen, receipt.Events)[index] == expected,
            "Caller mutation cannot change the receipt's frozen departure visibility.");
        var absent = EcmaJson.ToElement(new JsonObject { ["type"] = "unitRouted", ["unit"] = int.MaxValue });
        Require(presentation.Events.Describe(new(names, receipt.Before, receipt.After, hidden), absent) == "",
            "Hidden departures are filtered before unavailable private identities can be resolved.");
        var rejected = false;
        try
        {
            presentation.Events.Describe(new(names, receipt.Before, receipt.After), absent);
        }
        catch (LocalizationException) { rejected = true; }
        Require(rejected, "An observable departure with missing identity remains an explicit invariant error, not invented prose.");
    }

    private void Press(Key key, Window? window = null)
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
            if (window is not null)
                window.PushInput(input, true);
            else
                GetViewport().PushInput(input, true);
        }
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
