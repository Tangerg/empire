#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Localization;
using Empire.Core.Engine;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.State;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>Native end-to-end UI binding checks using the real application session, never a demo session.</summary>
public partial class BattleScreenChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Check();
            CheckPresentationTransitions();
            CheckInputContracts();
            CheckCanonicalCellProjection();
            CheckRegisteredReactions();
            CheckLocalizedInteraction();
            CheckInjectedEventPresenter();
            GD.Print("Native BattleScreen real-session integration passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckCanonicalCellProjection()
    {
        foreach (var canonicalOutside in new[] { false, true })
        {
            var level = BattleTextChecks.OpeningLevel();
            level["rules"]!["fog"] = true;
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            var source = new BattleEngine(packs).CreateState(level);
            var sourceActor = source["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == 2);
            var actorIndex = sourceActor["y"]!.GetValue<int>() * level["width"]!.GetValue<int>() + sourceActor["x"]!.GetValue<int>();
            var actorTerrain = source["map"]!["tiles"]![actorIndex]!.GetValue<string>();
            var terrain = packs.OfType<JsonObject>().SelectMany(pack => pack["terrains"]?.AsArray().OfType<JsonObject>() ?? [])
                .Single(definition => EcmaJson.ReadString(definition["id"]) == actorTerrain);
            if (canonicalOutside)
                terrain["tags"]!.AsArray().Add("outside");
            var engine = new BattleEngine(packs);
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation), level);
            var stateBefore = EcmaJson.Stringify(session.Snapshot());
            var queries = session.GetTacticalQueries(0);
            var actor = queries.Unit(2)!;
            var stops = queries.MoveStops(2).ToArray();
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            var scene = new AuthoredScene
            {
                LevelId = level["id"]!.GetValue<string>(),
                MapSize = [level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>()],
                FramePlacements = []
            };
            screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, scene);
            var materials = WorldMaterialsManifest.Load();
            materials.TerrainTags[actorTerrain] = canonicalOutside ? [] : ["outside"];
            screen.ConfigureGround(TerrainView.Factory(materials));

            screen.SelectCell(actor.At);
            Require((screen.SelectedUnitId == actor.Id) == !canonicalOutside,
                "Canonical terrain must govern selection when material tags disagree.");
            screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Space });
            Require(screen.SelectedUnitId is not { } selected || queries.InteractiveCells.Contains(queries.Unit(selected)!.At),
                "Native actor cycling cannot select a canonical outside cell.");

            var overlay = (BoardOverlay)screen.FindChild("LegalTargets", true, false);
            var visible = session.GetSnapshot().VisibleCells ?? throw new InvalidOperationException("Fog fixture has no visibility projection.");
            var fog = overlay.RangeOrder().Where(entry => entry.Role == "fog").Select(entry => entry.Cell).ToHashSet();
            Require(fog.SetEquals(queries.InteractiveCells.Where(cell => !visible.Contains(cell))),
                "Rendered fog must consume the exact canonical interactive-cell projection.");
            var state = session.Snapshot();
            var width = state["map"]!["width"]!.GetValue<int>();
            var unseenTerrain = state["map"]!["tiles"]!.AsArray().Select((tile, index) => (tile, at: new BoardCell(index % width, index / width)))
                .First(entry => EcmaJson.ReadString(entry.tile) == actorTerrain && !visible.Contains(entry.at)).at;
            Require(fog.Contains(unseenTerrain) == !canonicalOutside,
                "Material outside metadata cannot add or remove fog from a canonical cell.");
            Require(queries.MoveStops(2).SequenceEqual(stops) && EcmaJson.Stringify(session.Snapshot()) == stateBefore,
                "Changing presentation metadata cannot change legality or battle state.");
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckInjectedEventPresenter()
    {
        var level = BattleTextChecks.OpeningLevel();
        level["rules"]!["baseResourceGrants"] = new JsonArray(new JsonObject
        {
            ["resource"] = "funds",
            ["amount"] = 3,
        });
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var defaults = BattleTextChecks.Session(engine, text);
        const string customLine = "custom resource presenter reached the battle log";
        var presentation = defaults with
        {
            Events = defaults.Events.Clone().Replace("resourceChanged", (_, _) => customLine).Seal(),
        };
        var fresh = new GrayFlagSession(new(engine, presentation), level);
        var restored = GrayFlagSession.FromSave(new(engine, presentation), fresh.Save());
        foreach (var session in new[] { fresh, restored })
        {
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            Buttons(screen).Single(button => button.Name == "EndTurn").EmitSignal(Button.SignalName.Pressed);
            Require(session.GetSnapshot().Transition!.Events.Any(value => value.GetProperty("type").GetString() == "resourceChanged"),
                "The real end-turn action must emit a resource event for the custom presenter.");
            Require(screen.RecentMessages.Contains(customLine),
                "Fresh and loaded sessions must render their injected event presenter, not rebuilt defaults.");
            RemoveChild(screen);
            screen.Free();
        }
        Require(EcmaJson.DeepEquals(fresh.Snapshot(), restored.Snapshot()),
            "Injecting event wording cannot change gameplay across fresh and loaded sessions.");
    }

    private void CheckPresentationTransitions()
    {
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        void Bind(GrayFlagSession value) => screen.BindSession(new(text, battleText, value, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        Bind(session);
        Require(screen.RecentMessages.SequenceEqual(new[] { session.GetSnapshot().InitialLogMessage }),
            "New battle log must contain only the original opening line.");
        var destination = TacticalSceneInputChecks.Move(screen, session);
        Require(screen.RecentMessages.Count == 1, "Event text must wait until its animation completes.");
        Require(screen.IsPresenting && screen.Board.IsPresenting, "Accepted move must hold the presentation gate.");
        var revision = session.GetSnapshot().Revision;
        screen.SelectCell(destination);
        screen.SelectCell(session.GetTacticalQueries(0).Unit(2)!.At);
        screen.Refresh();
        Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null,
            "Repeated clicks and refresh must not dispatch or change selection during playback.");
        for (var step = 0; step < 100 && screen.IsPresenting; step++)
            screen.Board.AdvancePresentation(0.1);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == revision,
            "Timeline completion must settle the committed revision and release input.");
        var expectedLog = new Feedback.BattleLog();
        expectedLog.Add(session.GetSnapshot().InitialLogMessage!);
        foreach (var line in session.GetSnapshot().Transition!.EventDescriptions)
            expectedLog.Add(line);
        Require(screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Descriptions must append once in receipt order after presentation.");
        screen.Refresh();
        Require(!screen.IsPresenting && screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Refreshing a settled revision must not replay it.");
        var superseded = new GrayFlagSession(new(engine, presentation), level);
        var replaceable = new ReplaceableSession(superseded);
        screen.BindSession(new(text, battleText, replaceable, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        TacticalSceneInputChecks.Move(screen, replaceable);
        var replacement = superseded.GetSnapshot() with
        {
            Revision = 9,
            Transition = null
        };
        replaceable.Replacement = replacement;
        screen.Refresh();
        screen.Board.AdvancePresentation(100);
        Require(!screen.IsPresenting && screen.DisplayedRevision == 9,
            "A newer transition-free snapshot must invalidate older async completion.");
        screen.Refresh();
        Require(!screen.IsPresenting, "A transition-free revision must remain consumed.");
        var canceled = new GrayFlagSession(new(engine, presentation), level);
        Bind(canceled);
        TacticalSceneInputChecks.Move(screen, canceled);
        screen.Board.CancelPresentation();
        Require(!screen.IsPresenting && screen.DisplayedRevision == canceled.GetSnapshot().Revision,
            "Same-session cancellation must settle both board and controls to committed state.");
        var fresh = new GrayFlagSession(new(engine, presentation), level);
        Bind(fresh);
        TacticalSceneInputChecks.Move(screen, fresh);
        Require(screen.IsPresenting, "Second timeline did not start.");
        var loaded = GrayFlagSession.FromSave(new(engine, presentation), fresh.Save());
        Bind(loaded);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == 0,
            "Rebinding a loaded session must cancel old playback without replaying its history.");
        screen.BindSession(new(text, battleText, loaded, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"), initialLogMessage: "已读取第 1 回合的存档");
        Require(screen.RecentMessages.SequenceEqual(new[] { "已读取第 1 回合的存档" }), "Load must clear history and keep one load line.");
        screen.Board.AdvancePresentation(100);
        Require(screen.DisplayedRevision == 0, "Stale timeline completion overwrote the rebound view.");
        var last = new GrayFlagSession(new(engine, presentation), level);
        Bind(last);
        TacticalSceneInputChecks.Move(screen, last);
        var saves = 0;
        screen.BindApplicationActions(() => saves++);
        var staleSave = Buttons(screen).Single(button => button.Text == "保存");
        var lastRevision = last.GetSnapshot().Revision;
        RemoveChild(screen);
        staleSave.EmitSignal(Button.SignalName.Pressed);
        Require(saves == 0, "Unmounted application callbacks must not save or load.");
        screen.SelectCell(session.GetTacticalQueries(0).Unit(2)!.At);
        screen.SelectCell(destination);
        Require(last.GetSnapshot().Revision == lastRevision, "Unmounted callbacks must not dispatch.");
        Require(!screen.IsPresenting && !screen.Board.IsPresenting, "Unmount must release all presentation waiters.");
        screen.QueueFree();
    }

    private sealed class ReplaceableSession(ITacticalBattleSession inner) : ITacticalBattleSession
    {
        public BattleSnapshot? Replacement
        {
            get; set;
        }
        public ITacticalSelectionQueries GetTacticalQueries(long binding) => new StampedQueries(inner.GetTacticalQueries(binding), new(binding, GetSnapshot().Revision));
        public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy)
        {
            var source = inner.ProjectHud(stamp with
            {
                Revision = inner.GetSnapshot().Revision
            }, selection, cursor, inspectId, busy);
            return source with
            {
                Stamp = stamp,
                Sections = source.Sections.Select(section => section with
                {
                    Rows = section.Rows.Select(row => row with { Intent = row.Intent is { } intent ? intent with { Stamp = stamp } : null }).ToArray()
                }).ToArray()
            };
        }
        public BattleDispatchResult Undo(long expectedRevision) => inner.Undo(expectedRevision);

        public BattleSnapshot GetSnapshot() => Replacement ?? inner.GetSnapshot();
        public BattleDispatchResult Dispatch(System.Text.Json.JsonElement action, long expectedRevision) => inner.Dispatch(action, expectedRevision);
    }

    private void Check()
    {
        Require(typeof(BattleScreen).GetMethod(nameof(BattleScreen.BindSession))!.GetParameters()[0].ParameterType == typeof(BattleScreenRules) && typeof(BattleScreenRules).GetProperty(nameof(BattleScreenRules.Session))!.PropertyType == typeof(ITacticalBattleSession),
            "BattleScreen must declare its complete consumer port rather than discover a hidden capability at runtime.");
        var level = BattleTextChecks.OpeningLevel();
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation), level);
        var screen = new BattleScreen();
        screen.Board.AnimationsEnabled = false; // This suite isolates synchronous binding, not timeline behavior.
        AddChild(screen);
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
        screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(),
            engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
        screen.ConfigureGround(rendering.Ground);
        Require(screen.GetNode<ColorRect>("Backdrop").ZIndex < -300,
            "The screen backdrop must be below all negative-depth terrain layers.");
        var before = session.GetSnapshot();
        var beforeState = before.State.GetRawText();
        screen.SelectCell(session.GetTacticalQueries(0).Unit(2)!.At);
        Require(screen.SelectedUnitId == 2 && session.GetSnapshot().Revision == before.Revision,
            "Selecting Torren must not advance canonical state.");
        var queries = session.GetTacticalQueries(0);
        var destination = queries.MoveStops(2).First(cell => cell != session.GetTacticalQueries(0).Unit(2)!.At
            && queries.CommandsAt(2, cell) is { Count: 1 } commands && commands[0].SelfTargeted);
        var staleTurnButton = Buttons(screen).Single(button => button.Text == "结束回合");
        screen.SelectCell(destination);
        var after = session.GetSnapshot();
        Require(after.Revision == before.Revision + 1 && screen.DisplayedRevision == after.Revision,
            "A sole self-targeted destination command dispatches without a confirmation menu.");
        var actor = after.State.GetProperty("units").EnumerateArray()
            .Single(unit => unit.GetProperty("id").GetInt32() == 2);
        Require(actor.GetProperty("x").GetInt32() == destination.X
            && actor.GetProperty("y").GetInt32() == destination.Y
            && actor.GetProperty("done").GetBoolean(),
            "The real destination selection must commit the complete wait-at-destination command.");
        staleTurnButton.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == after.Revision, "A callback rendered before dispatch cannot end the newer turn.");
        var stale = session.Dispatch(after.Transition!.Action, before.Revision);
        Require(!stale.Accepted && session.GetSnapshot().Revision == after.Revision,
            "An obsolete UI revision must not replay a command.");
        Require(before.State.GetRawText() == beforeState,
            "Earlier detached snapshots must remain immutable.");
        var turnButton = Buttons(screen).Single(button => button.Name == "EndTurn");
        turnButton.EmitSignal(Button.SignalName.Pressed);
        var nextTurn = session.GetSnapshot();
        Require(nextTurn.Revision == after.Revision + 1
            && nextTurn.State.GetProperty("currentPlayer").GetInt32() != after.State.GetProperty("currentPlayer").GetInt32()
            && screen.DisplayedRevision == nextTurn.Revision,
            "The sole turn button must commit through the real session and refresh current player.");
        screen.QueueFree();
    }
    private void CheckInputContracts()
    {
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        void Keypress(Key key) => screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
        var snapshot = session.GetSnapshot();
        var staleSameRevision = Buttons(screen).Single(button => button.Text == "结束回合");
        Keypress(Key.Space);
        Require(screen.SelectedUnitId == session.GetTacticalQueries(0).Actors()[0], "Space selects first source-ordered actor through native keyboard input.");
        staleSameRevision.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == snapshot.Revision, "Removed global button cannot dispatch after same-revision selection refresh.");
        Keypress(Key.Escape);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle, "Native Escape follows the source back operation.");
        screen.SelectCell(session.GetTacticalQueries(0).Unit(2)!.At);
        var selected = screen.SelectedUnitId;
        var queries = session.GetTacticalQueries(0);
        var destination = queries.MoveStops(2).First(at => at != session.GetTacticalQueries(0).Unit(2)!.At && queries.CommandsAt(2, at) is { Count: 1 } commands && commands[0].SelfTargeted);
        screen.SetApplicationBusy(true);
        var busyReaction = Buttons(screen).First(button => button.GetMeta("hud_key", "").AsString().StartsWith("stances/reaction:", StringComparison.Ordinal));
        screen.SelectCell(destination);
        busyReaction.EmitSignal(Button.SignalName.Pressed);
        Keypress(Key.E);
        Keypress(Key.Escape);
        Require(session.GetSnapshot().Revision == snapshot.Revision && screen.SelectedUnitId == selected, "Application busy blocks board, HUD actions, end-turn and back input.");
        screen.SetApplicationBusy(false);
        Require(screen.SelectedUnitId == selected, "Clearing application busy preserves the valid selection.");
        screen.SelectCell(destination);
        Require(session.GetSnapshot().Revision == snapshot.Revision + 1, "Busy release re-enables source automatic destination dispatch.");
        var undo = Buttons(screen).Single(button => button.Text == "撤销");
        Require(!undo.Disabled, "Real undo button becomes enabled after a reversible move.");
        undo.EmitSignal(Button.SignalName.Pressed);
        var undone = session.GetSnapshot();
        Require(undone.Revision == snapshot.Revision + 2 && EcmaJson.DeepEquals(EcmaJson.Parse(undone.State.GetRawText()), EcmaJson.Parse(snapshot.State.GetRawText())), "Mounted undo restores exact canonical state and advances revision.");
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && screen.RecentMessages.Contains("已撤销上一步"), "Undo resets selection and appends the source cosmetic message.");
        var turn = Buttons(screen).Single(button => button.Text == "结束回合");
        turn.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == undone.Revision + 1, "Mounted global end-turn commits once after undo.");

        // Fixture: original campaign map and units with an explicit deployment zone, not naturally reached campaign state.
        var deploymentLevel = EcmaJson.Parse(EcmaJson.Stringify(level))!.AsObject();
        var own = deploymentLevel["units"]!.AsArray().OfType<JsonObject>().Where(unit => unit["owner"]!.GetValue<int>() == 1).ToArray();
        var cells = new JsonArray(own.Select(unit => (JsonNode)new JsonObject { ["x"] = unit["x"]!.GetValue<int>(), ["y"] = unit["y"]!.GetValue<int>() }).ToArray());
        deploymentLevel["scenario"]!.AsObject()["zones"] = new JsonArray(new JsonObject { ["id"] = "selection-check-zone", ["cells"] = cells });
        deploymentLevel["deployment"] = new JsonObject { ["order"] = new JsonArray(1), ["zones"] = new JsonArray(new JsonObject { ["player"] = 1, ["zone"] = "selection-check-zone" }) };
        var deployment = new GrayFlagSession(new(engine, presentation), deploymentLevel);
        screen.BindSession(new(text, battleText, deployment, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), deploymentLevel, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        var roster = deployment.GetTacticalQueries(0).DeploymentUnits();
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        Button RosterButton(int id)
        {
            var row = hud.Projection!.Sections.SelectMany(section => section.Rows.Select(row => (section, row)))
                .Single(pair => pair.row.Intent is { Kind: TacticalHudIntentKind.DeployPick, Unit: var unit } && unit == id);
            return Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == row.section.Key + "/" + row.row.Key);
        }
        RosterButton(roster[0]).EmitSignal(Button.SignalName.Pressed);
        RosterButton(roster[1]).EmitSignal(Button.SignalName.Pressed);
        Require(screen.SelectedUnitId == roster[1] && screen.Selection?.Kind == TacticalSelectionKind.DeploymentUnit && deployment.GetSnapshot().Revision == 0,
            "Two actual deployment roster clicks change the picked unit without dispatching a swap.");
        var confirm = Buttons(screen).Single(button => button.Text == "确认部署");
        confirm.EmitSignal(Button.SignalName.Pressed);
        Require(deployment.GetSnapshot().Revision == 1 && deployment.GetSnapshot().State.GetProperty("phase").GetString() == "playing", "Mounted deployment confirmation opens battle through session dispatch.");
        screen.QueueFree();
    }

    private void CheckRegisteredReactions()
    {
        var level = BattleTextChecks.OpeningLevel();
        var reactions = ReactionRegistry.Core();
        reactions.Register(new("plugin-dodge", false, 0.4, false, false));
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")),
            [new EnginePlugin("test.native-reactions", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]);
        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var messages = translation.GetMessageList().ToDictionary(key => key, key => translation.GetMessage(key).ToString(), StringComparer.Ordinal);
        messages["test.reactions.guard.name"] = "替换防御菜单";
        messages["test.reactions.guard.hint"] = "来自显式表现元数据";
        messages["test.reactions.plugin-dodge.name"] = "自定义闪避菜单";
        messages["test.reactions.plugin-dodge.hint"] = "自定义提示";
        var text = new GameText(key => messages.GetValueOrDefault(key), System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        var metadata = PresentationJson.Load<JsonObject>("res://data/grayflag-content-text.json");
        metadata["reactions"]!["guard"] = new JsonObject
        {
            ["name"] = "test.reactions.guard.name",
            ["hint"] = "test.reactions.guard.hint"
        };
        metadata["reactions"]!["plugin-dodge"] = new JsonObject
        {
            ["name"] = "test.reactions.plugin-dodge.name",
            ["hint"] = "test.reactions.plugin-dodge.hint"
        };
        var presentation = SessionPresentationText.Compose(new(engine, text),
            PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"), EcmaJson.Stringify(metadata));
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        var origin = session.GetTacticalQueries(0).Unit(2)!.At;
        screen.SelectCell(origin);
        Button Reaction(string id) => Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "stances/reaction:" + id);
        var buttons = Buttons(screen).Where(button => button.GetMeta("hud_key", "").AsString().StartsWith("stances/reaction:", StringComparison.Ordinal)).ToArray();
        Require(buttons.Select(button => button.Text).SequenceEqual(reactions.All().Select(stance => presentation.Content.ReactionName(stance.Id)))
            && buttons.Select(button => button.TooltipText).SequenceEqual(reactions.All().Select(stance => presentation.Content.ReactionHint(stance.Id)))
            && Reaction("guard").Text == messages["test.reactions.guard.name"]
            && Reaction("plugin-dodge").TooltipText == messages["test.reactions.plugin-dodge.hint"],
            "Mounted reaction controls retain resolved registry order and explicitly supplied names and hints.");
        var staleGuard = Reaction("guard");
        Reaction("plugin-dodge").EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 1 && session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["id"]!.GetValue<int>() == 2)["reaction"]!.GetValue<string>() == "plugin-dodge",
            "The actual HUD button must commit a registered reaction through the tactical session port.");
        staleGuard.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 1, "Removed reaction controls cannot replay after a HUD dispatch.");
        screen.SelectCell(origin);
        var staleCustom = Reaction("plugin-dodge");
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        var guard = hud.Projection!.Sections.Single(section => section.Key == "stances").Rows.Single(row => row.Key == "reaction:guard").Intent!;
        Require(session.Dispatch(guard.Action!.Value, guard.Stamp.Revision).Accepted, "External revision fixture did not commit.");
        staleCustom.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 2 && screen.DisplayedRevision == 1 && screen.Selection?.Kind == TacticalSelectionKind.Unit
            && session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["id"]!.GetValue<int>() == 2)["reaction"]!.GetValue<string>() == "guard",
            "Still-mounted HUD controls cannot use stale choices after the live session revision changes.");
        screen.Refresh();
        screen.SelectCell(origin);
        Reaction("plugin-dodge").EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 3, "Refreshing the stale view must restore the actual HUD action path.");
        screen.QueueFree();
    }

    private void CheckLocalizedInteraction()
    {
        var sourceLevel = BattleTextChecks.OpeningLevel();
        var level = sourceLevel.DeepClone().AsObject();
        // Bring one authored enemy into preview range without dispatching a setup action.
        var defender = level["units"]!.AsArray().OfType<JsonObject>().First(unit => unit["owner"]!.GetValue<int>() == 2);
        var attacker = level["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]?.GetValue<string>() == "campaign-torren");
        var defenderAt = new BoardCell(attacker["x"]!.GetValue<int>() + 1, attacker["y"]!.GetValue<int>());
        defender["x"] = defenderAt.X;
        defender["y"] = defenderAt.Y;
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var registeredMap = engine.CreateState(sourceLevel)["map"]!.AsObject();
        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var keys = translation.GetMessageList().ToHashSet(StringComparer.Ordinal);
        string? baselineState = null, baselineEvents = null, baselineSave = null;
        foreach (var variant in new[] { "A", "B" })
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ui.hud.selection.unit"] = variant + " select destination",
                ["ui.hud.health"] = variant + " health {0}/{1}",
                ["ui.battle.undo"] = variant + " undo",
                ["feedback.session.undone"] = variant + " restored",
                ["ui.battle.artwork_issues"] = variant + " artwork unavailable: {0}",
                ["content.units.c01.swordsman.name"] = variant + " swordsman",
                ["content.units.c01.swordsman.blurb"] = variant + " swordsman description",
                ["content.reactions.guard.name"] = variant + " guard",
                ["content.reactions.guard.hint"] = variant + " guard hint",
                ["content.abilities.attack.name"] = variant + " attack",
                ["content.abilities.attack.hint"] = variant + " attack hint",
                ["content.weapons.c01.border-blade.name"] = variant + " border blade",
                ["combat.modifier.row"] = variant + " modifier {0} {1}",
                ["combat.modifier.matchup"] = variant + " matchup",
            };
            var text = new GameText(key => replacements.TryGetValue(key, out var replacement) ? replacement
                : keys.Contains(key) ? translation.GetMessage(key).ToString() : null,
                System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation), level);
            var initial = EcmaJson.Stringify(session.Snapshot());
            var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
            bindings.Actors.Clear();
            bindings.Units.Remove("c01.archer");
            var assets = new AssetCatalog(PresentationJson.Load<AssetManifest>("res://data/selected-assets.json"), bindings);
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
            var rendering = BoardSceneRendering.Load(scene, sourceLevel, registeredMap);
            screen.BindSession(new(text, battleText, session, new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
            screen.ConfigureGround(rendering.Ground);
            Require(screen.Board.RenderingIssueCount > 0 && screen.Board.Diagnostics.Any(value => value.Contains("c01.archer", StringComparison.Ordinal)),
                "Unavailable actor art remains explicitly diagnosed without changing canonical units.");
            Require(Descendants(screen).OfType<Label>().Any(label => label.Text == text.Format("ui.battle.artwork_issues", screen.Board.RenderingIssueCount))
                && !Descendants(screen).OfType<Control>().Any(control => control.TooltipText.Contains("c01.archer", StringComparison.Ordinal)),
                "The actual board notice uses injected wording and never exposes raw resource diagnostics in a tooltip.");
            var origin = session.GetTacticalQueries(0).Unit(2)!.At;
            screen.SelectCell(origin);
            var nativeHud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var hud = nativeHud.Projection!;
            var unitRows = hud.Sections.Single(section => section.Key == "unit").Rows;
            Require(hud.Hint == replacements["ui.hud.selection.unit"] && unitRows.Single(row => row.Key == "hp").Label.StartsWith(variant + " health", StringComparison.Ordinal),
                "Mounted selection help and numeric unit facts use the injected catalog.");
            var guard = Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "stances/reaction:guard");
            Require(hud.Sections.Single(section => section.Key == "unit").Title == replacements["content.units.c01.swordsman.name"]
                && unitRows.Single(row => row.Key == "blurb").Label == replacements["content.units.c01.swordsman.blurb"]
                && guard.Text == replacements["content.reactions.guard.name"] && guard.TooltipText == replacements["content.reactions.guard.hint"],
                "Mounted unit content and reaction labels and hints use supplied presentation text, not canonical definitions.");
            screen.SelectCell(origin);
            var attack = Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "commands/attack:c01.border-blade");
            Require(attack.Text.Contains(replacements["content.abilities.attack.name"], StringComparison.Ordinal)
                && attack.Text.Contains(replacements["content.weapons.c01.border-blade.name"], StringComparison.Ordinal)
                && attack.TooltipText.Contains(replacements["content.abilities.attack.hint"], StringComparison.Ordinal),
                "Mounted weapon command names and range hints compose supplied ability and weapon text.");
            screen.SelectCell(defenderAt);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Target,
                "Selecting the adjacent authored enemy must arm a real attack preview without dispatching.");
            using var hoverTarget = new InputEventKey { Keycode = Key.Right, Pressed = true };
            screen._UnhandledKeyInput(hoverTarget);
            var modifierRows = nativeHud.Projection!.Sections.Single(section => section.Key == "forecast").Rows
                .Where(row => row.Key.StartsWith("strike-mod:", StringComparison.Ordinal)).ToArray();
            Require(modifierRows.Length > 0 && modifierRows.All(row => row.Label.StartsWith(variant + " modifier ", StringComparison.Ordinal))
                && modifierRows.Any(row => row.Label.Contains(replacements["combat.modifier.matchup"], StringComparison.Ordinal))
                && modifierRows.All(row => Descendants(nativeHud).OfType<Label>().Any(label => label.Text == row.Label)),
                "Real attack-preview modifiers render through supplied formatter text in the mounted native HUD.");
            using var cancel = new InputEventKey { Keycode = Key.Escape, Pressed = true };
            screen._UnhandledKeyInput(cancel);
            screen._UnhandledKeyInput(cancel);
            screen._UnhandledKeyInput(cancel);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && session.GetSnapshot().Revision == 0
                && EcmaJson.Stringify(session.Snapshot()) == initial && session.Events.Count == 0,
                "Content inspection and attack preview cancel without changing domain state or emitting events.");
            TacticalSceneInputChecks.Move(screen, session);
            var state = EcmaJson.Stringify(session.Snapshot());
            var events = EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode?)value).ToArray()));
            var actualSave = session.Save();
            Require(EcmaJson.DeepEquals(actualSave, engine.SaveBattle(session.Snapshot(), actualSave["savedAt"]!.GetValue<string>())),
                "The real session save uses the canonical complete document contract.");
            var save = EcmaJson.Stringify(engine.SaveBattle(session.Snapshot(), "2026-10-21T00:00:00.000Z"));
            Require(session.GetSnapshot().Revision == 1, "Localized controls still commit the real movement command.");
            if (baselineState is null)
            {
                baselineState = state;
                baselineEvents = events;
                baselineSave = save;
            }
            else
                Require(state == baselineState && events == baselineEvents && save == baselineSave,
                "Changing only catalog wording cannot change the battle state, event stream or saved document.");
            var undo = Buttons(screen).Single(button => button.Name == "Undo");
            Require(undo.Text == replacements["ui.battle.undo"], "The mounted undo button uses injected wording.");
            undo.EmitSignal(Button.SignalName.Pressed);
            Require(EcmaJson.Stringify(session.Snapshot()) == initial && session.GetSnapshot().Revision == 2
                && screen.RecentMessages.Contains(replacements["feedback.session.undone"]),
                "The real localized undo restores the canonical state and formats its feedback independently.");
            RemoveChild(screen);
            screen.Free();
        }

        level = BattleTextChecks.OpeningLevel();
        foreach (var expectedArtFailure in new[] { true, false })
        {
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation), level);
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            Exception fault = expectedArtFailure ? new MissingArtResourceException("res://test-missing-art", "test missing art diagnostic")
                : new InvalidOperationException("test presentation programming defect");
            screen.Board.EventPresented += (_, _) => throw fault;
            var previous = System.Threading.SynchronizationContext.Current;
            var capture = new PresentationFaultContext();
            System.Threading.SynchronizationContext.SetSynchronizationContext(capture);
            try
            {
                TacticalSceneInputChecks.Move(screen, session);
            }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previous); }
            var playerNotice = text.Format("error.presentation.playback_failed");
            Require(expectedArtFailure ? capture.Fault is null : ReferenceEquals(capture.Fault, fault),
                "Only expected missing-art failures are recovered; programming faults escape the async UI boundary unchanged.");
            Require(Descendants(screen).OfType<Label>().Any(label => label.Text == playerNotice) == expectedArtFailure,
                "A programming defect must not be converted into a recoverable player notice.");
            Require(!screen.IsPresenting && !screen.Board.IsPresenting && session.GetSnapshot().Revision == 1,
                "Failure cleanup releases playback gates without undoing the already committed domain action.");
            RemoveChild(screen);
            screen.Free();
        }
    }

    private sealed class PresentationFaultContext : System.Threading.SynchronizationContext
    {
        public Exception? Fault
        {
            get; private set;
        }
        public override void Post(System.Threading.SendOrPostCallback callback, object? state)
        {
            try
            {
                callback(state);
            }
            catch (Exception error) { Fault = error; }
        }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static System.Collections.Generic.IEnumerable<Button> Buttons(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button)
                yield return button;
            foreach (var descendant in Buttons(child))
                yield return descendant;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    // Only this synthetic replacement fixture rebases a revision. Production must never repair a mismatched stamp.
    private sealed class StampedQueries(ITacticalSelectionQueries inner, TacticalStamp stamp) : ITacticalSelectionQueries
    {
        public TacticalStamp Stamp => stamp;
        public string Phase => inner.Phase;
        public bool HumanInput => inner.HumanInput;
        public bool HumanTurn => inner.HumanTurn;
        public bool CanUndo => inner.CanUndo;
        public IReadOnlySet<BoardCell> InteractiveCells => inner.InteractiveCells;
        public TacticalUnit? Unit(int id) => inner.Unit(id);
        public TacticalUnit? UnitAt(BoardCell at) => inner.UnitAt(at);
        public bool AreEnemies(int firstUnit, int secondUnit) => inner.AreEnemies(firstUnit, secondUnit);
        public bool CanOpenRecruitment(BoardCell at) => inner.CanOpenRecruitment(at);
        public bool IsEnemyOfViewer(int unit) => inner.IsEnemyOfViewer(unit);
        public IReadOnlyList<int> Actors() => inner.Actors();
        public IReadOnlyList<BoardCell> MoveStops(int unit) => inner.MoveStops(unit);
        public IReadOnlyList<BoardCell>? PathTo(int unit, BoardCell at) => inner.PathTo(unit, at);
        public IReadOnlyList<BoardCell> Threat(int unit) => inner.Threat(unit);
        public IReadOnlyList<BoardCell> ControlZone(int unit) => inner.ControlZone(unit);
        public IReadOnlyList<TacticalCommand> CommandsAt(int unit, BoardCell at) => inner.CommandsAt(unit, at);
        public TacticalAttack Attack(int unit, BoardCell target, BoardCell from, string weapon) => inner.Attack(unit, target, from, weapon);
        public IReadOnlyList<int> DeploymentUnits() => inner.DeploymentUnits();
        public IReadOnlyList<BoardCell> DeploymentZone() => inner.DeploymentZone();
        public IReadOnlyList<BoardCell> DeploymentSpots(int unit) => inner.DeploymentSpots(unit);
        public IReadOnlyList<TacticalTactic> Tactics() => inner.Tactics();
        public IReadOnlyList<TacticalPassenger> Passengers(int carrier) => inner.Passengers(carrier);
        public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from) => inner.DirectionSteps(from);
    }

}
