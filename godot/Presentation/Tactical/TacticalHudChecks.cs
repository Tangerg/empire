#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    private void CheckInspectionOrder()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var structure = state["structures"]![0]!.AsObject();
        var at = new BoardCell(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
        foreach (var disabled in new[] { false, true })
        {
            structure["disabled"] = disabled;
            var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
            var before = EcmaJson.Stringify(session.Snapshot());
            var hud = session.ProjectHud(new(0, 0), new(), TacticalHit.Object(at), null, false);
            var sections = hud.Sections.Select(section => section.Key).ToArray();
            Require(Array.IndexOf(sections, "structure") < Array.IndexOf(sections, "tile"), "Structure inspection precedes passive ground details.");
            var rows = hud.Sections.Single(section => section.Key == "structure").Rows;
            Require(rows.Take(2).Select(row => row.Key).SequenceEqual(new[] { "hp", "owner" }), "Health and owner precede structure capabilities.");
            Require(rows.Any(row => row.Key == "disabled") == disabled && (!disabled || rows[2].Key == "disabled"), "Current disabled state precedes structure capabilities.");
            Require(before == EcmaJson.Stringify(session.Snapshot()), "Inspection ordering cannot mutate canonical state.");
        }
        // A detached recruitment fixture: campaign sites need not offer recruits.
        var site = new BoardCell(0, 0);
        state["map"]!["tiles"]![0] = "castle";
        state["map"]!["owners"]![0] = state["currentPlayer"]!.DeepClone();
        var initial = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        Require(initial.GetTacticalQueries(0).CanOpenRecruitment(site), "Recruitment fixture is reachable through the existing query.");
        var recruit = initial.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Recruit,
            Destination = site
        }, TacticalHit.Object(site), null, false);
        var keys = recruit.Sections.Select(section => section.Key).ToArray();
        Require(Array.IndexOf(keys, "recruit") < Array.IndexOf(keys, "tile"), "Recruitment actions precede passive site terrain details.");
        var cards = engine.Recruitment.Cards(initial.Snapshot(), new(site.X, site.Y));
        var choices = recruit.Sections.Single(section => section.Key == "recruit").Rows.Where(row => row.Intent?.Kind == TacticalHudIntentKind.Recruit).ToArray();
        Require(choices.Select(row => row.Intent!.Key).SequenceEqual(cards.Select(card => card.Unit))
            && choices.Select(row => row.Enabled).SequenceEqual(cards.Select(card => card.Affordable)), "Reordering preserves the authoritative recruitment cards and affordability.");
        state["map"]!["owners"]![0] = 0;
        var neutral = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        var unavailable = neutral.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Site,
            Destination = site
        }, null, null, false);
        Require(engine.Recruitment.Cards(neutral.Snapshot(), new(0, 0)).Count == 0
            && !neutral.GetTacticalQueries(0).CanOpenRecruitment(site)
            && unavailable.Sections.Single(section => section.Key == "site").Rows.Single(row => row.Key == "recruit-use").Label
                == GodotTextCatalog.LoadSimplifiedChinese().Format("ui.hud.site.recruit_unavailable"),
            "A producing but unowned site cannot promise a recruitment panel that the canonical query will not open.");
    }

    private void CheckSiteInspection()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var control = new TacticalSelectionController(session.GetTacticalQueries(41));
        var home = new BoardCell(38, 23);
        var before = EcmaJson.Stringify(session.Snapshot());
        TacticalHudProjection Hud() => session.ProjectHud(control.Stamp, control.Selection, control.CursorHit, control.InspectId, control.Busy);
        Require(control.Click(control.Stamp, TacticalHit.Object(home)).Request is null && control.Selection.Kind == TacticalSelectionKind.Site,
            "The real non-recruiting Redstone home opens inspection, not a recruit panel or a domain command.");
        control.Leave(control.Stamp);
        var homeHud = Hud();
        var site = homeHud.Sections.Single(section => section.Key == "site");
        Require(control.Cursor is null && control.Overlay().Selected == home && site.Rows.Any(row => row.Key == "owner"),
            "Leaving the board for the HUD preserves the selected site, its highlight and owner.");
        Require(site.Rows.Single(row => row.Key == "heal").Label == text.Format("ui.hud.terrain_heal", 15)
            && site.Rows.Single(row => row.Key == "heal-use").Label == text.Format("ui.hud.site.heal_use", text.Format("ui.hud.site.heal_timing_side")),
            "The unchanged initial home explains the canonical 15 HP and owned-unit start-of-turn timing.");
        Require(site.Rows.Single(row => row.Key == "recruit-use").Label == text.Format("ui.hud.site.no_recruitment")
            && homeHud.Sections.All(section => section.Key != "recruit")
            && !session.GetTacticalQueries(41).CanOpenRecruitment(home),
            "Home stays non-recruiting in both data and UI.");
        var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(value => value["owner"]!.GetValue<int>() == 1);
        var unitAt = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
        control.Hover(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
        Require(control.Selection.Destination == home && control.InspectId is null
            && Hud().Sections.Single(section => section.Key == "site").Title == site.Title,
            "Moving across units toward the HUD cannot replace a pinned site with incidental unit inspection.");
        control.Leave(control.Stamp);
        var hudWidget = new TacticalHud();
        AddChild(hudWidget);
        hudWidget.IntentRequested += intent => control.Back(intent.Stamp);
        hudWidget.Apply(Hud());
        var close = ((VBoxContainer)hudWidget.FindChild("site", true, false)).GetChildren().OfType<Button>().Single();
        close.EmitSignal(BaseButton.SignalName.Pressed);
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.Overlay().Selected is null
            && Hud().Sections.All(section => section.Key != "site"),
            "The mounted close button clears selected site inspection after pointer leave.");
        RemoveChild(hudWidget);
        hudWidget.Free();
        var sites = new[] { home, new BoardCell(16, 17), new(23, 36), new(55, 40), new(85, 14), new(88, 21), new(78, 30), new(88, 39) };
        foreach (var at in sites)
        {
            Require(control.Click(control.Stamp, TacticalHit.Object(at)).Request is null && control.Selection.Kind == TacticalSelectionKind.Site
                && control.Selection.Destination == at && control.Overlay().Selected == at,
                $"Redstone site {at} must replace the previous inspection without advancing the battle; got {control.Selection.Kind}.");
            if (session.GetTacticalQueries(41).UnitAt(at) is { Visible: true } occupant)
            {
                control.Hover(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
                control.Leave(control.Stamp);
                Require(control.InspectId is null && Hud().Sections.All(section => section.Key != "unit"),
                    "An explicit site hit must not inspect a same-cell garrison that the pointer did not hit.");
            }
            control.Leave(control.Stamp);
            var rows = Hud().Sections.Single(section => section.Key == "site").Rows;
            var index = at.Y * level["width"]!.GetValue<int>() + at.X;
            var owner = session.Snapshot()["map"]!["owners"]![index]!.GetValue<int>();
            Require(rows.Any(row => row.Key == "capture-use") == (owner != 1),
                "Only a site not owned by the viewer explains capture; no capture command is invented.");
        }
        control.Key(control.Stamp, "escape");
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.Overlay().Selected is null,
            "Escape closes the pinned site through the existing back transition.");
        control.Click(control.Stamp, TacticalHit.Object(home));
        control.Click(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
        Require(control.Selection.Kind == TacticalSelectionKind.Unit && control.Selection.UnitId == unit["id"]!.GetValue<int>(),
            "A friendly actor remains directly selectable from site inspection.");
        Require(before == EcmaJson.Stringify(session.Snapshot()) && session.Events.Count == 0,
            "Site selection, hover, switching and closing do not mutate battle facts or emit events.");
    }

    private async System.Threading.Tasks.Task CheckSitePointerBoundary()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var rendering = BoardSceneRendering.Load(scene, level, session.Snapshot()["map"]!.AsObject());
        var screen = new BattleScreen();
        AddChild(screen);
        screen.BindSession(new(presentation.Messages, presentation.Battle, PortraitCatalogResources.Load(), session,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
        screen.ConfigureGround(rendering.Ground);
        screen.ConfigureDynamicState();
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        screen.Size = new Vector2(1180, 812);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var map = (Control)screen.FindChild("MapArea", true, false);
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var boardToMap = map.GetGlobalTransformWithCanvas().AffineInverse() * screen.Board.GetGlobalTransformWithCanvas();
        var inside = session.GetTacticalQueries(0).InteractiveCells
            .Select(cell => boardToMap * screen.Board.Projection.CellCenter(new(cell.X, cell.Y)))
            .First(point => new Rect2(Vector2.Zero, map.Size).HasPoint(point)
                && screen.Board.Pick(boardToMap.AffineInverse() * point) is not null);
        var outside = new Vector2(map.Size.X + 1, inside.Y);
        void Motion(Vector2 at) => map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = at });
        void Down(Vector2 at, MouseButton button = MouseButton.Left) => map.EmitSignal(Control.SignalName.GuiInput,
            new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at });
        void Release(Vector2 at, MouseButton button = MouseButton.Left) => screen._Input(new InputEventMouseButton
        {
            ButtonIndex = button,
            Position = map.GetGlobalTransformWithCanvas() * at
        });
        bool HasTile() => hud.Projection!.Sections.Any(section => section.Key == "tile");

        screen.SelectHit(TacticalHit.Object(new(38, 23)));
        Motion(outside);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Site
            && hud.Projection!.Sections.Single(section => section.Key == "tile").Rows.First().Label == "38, 23",
            "A native outside-map motion preserves only the pinned site, not an offscreen map projection.");
        ((VBoxContainer)hud.FindChild("site", true, false)).GetChildren().OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && !HasTile(),
            "Closing from the HUD cannot expose a fictitious offscreen hover tile.");
        Motion(inside);
        Require(HasTile(), "A visible native battlefield cell remains inspectable.");
        screen.SetApplicationBusy(true);
        Motion(outside);
        Require(!HasTile(), "Busy read-only hover obeys the same viewport boundary.");
        screen.SetApplicationBusy(false);

        // A release just beyond the edge is below the six-pixel drag threshold.
        Motion(inside);
        Down(new(map.Size.X - 1, inside.Y));
        map.EmitSignal(Control.SignalName.MouseExited);
        Release(outside);
        Require(!HasTile() && screen.Selection?.Kind == TacticalSelectionKind.Idle,
            "A short boundary crossing clears hover without dispatching a click into the HUD.");
        Motion(inside);
        Require(HasTile(), "An outside release clears pointer capture for the next hover.");
        Down(new(map.Size.X - 8, inside.Y));
        screen._Input(new InputEventMouseMotion { Position = map.GetGlobalTransformWithCanvas() * outside });
        Release(outside);
        Motion(inside);
        Require(HasTile() && screen.Selection?.Kind == TacticalSelectionKind.Idle && screen.DisplayedRevision == 0,
            $"A drag released over the HUD clears capture and cannot advance battle state; tile={HasTile()}, selection={screen.Selection?.Kind}, revision={screen.DisplayedRevision}.");
        Down(new(map.Size.X - 1, inside.Y), MouseButton.Middle);
        map.EmitSignal(Control.SignalName.MouseExited);
        Release(outside, MouseButton.Middle);
        Require(!HasTile(), "Middle-button boundary release clears hover through the same captured gesture policy.");
        RemoveChild(screen);
        screen.Free();
    }

    private void CheckObjectiveChecklist()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-07");
        var state = engine.CreateState(level);
        var opening = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-01");
        var openingSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), opening);
        var openingBefore = EcmaJson.Stringify(openingSession.Snapshot());
        var openingSnapshot = openingSession.Snapshot();
        var viewer = openingSnapshot["players"]!.AsArray().OfType<JsonObject>()
            .Single(player => player["controller"]!.GetValue<string>() == "human")["id"]!.GetValue<int>();
        var openingObjectives = engine.Victory.Objectives(openingSnapshot, viewer);
        var singleTask = openingObjectives.Single().Children.Single();
        var openingRows = openingSession.ProjectHud(new(0, 0), new(), null, null, false)
            .Sections.Single(section => section.Key == "objectives").Rows;
        Require(openingRows.Count == 1 && openingRows[0].Key == singleTask.Id && openingRows[0].Hint.Length == 0
            && openingRows[0].Label.Contains(presentation.Battle.ObjectiveProgress(singleTask), StringComparison.Ordinal),
            "A single-task C01 objective shows its canonical progress once, on the named task.");
        Require(openingBefore == EcmaJson.Stringify(openingSession.Snapshot()), "Objective compaction cannot change battle facts.");
        var objectiveStates = state["players"]![0]!["objectiveStates"]!.AsObject();
        objectiveStates["granary"]!["status"] = "completed";
        foreach (var hidden in new[] { false, true })
        {
            objectiveStates["militia-barracks"]!["hidden"] = hidden;
            var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
            var before = EcmaJson.Stringify(session.Snapshot());
            var rows = session.ProjectHud(new(0, 0), new(), null, null, false).Sections.Single(section => section.Key == "objectives").Rows;
            Require(rows.Single(row => row.Key == "granary").Label.Contains(text.Format("objective.c01_07.p1.granary"), StringComparison.Ordinal)
                && rows.Single(row => row.Key == "granary").Selected, "The named granary renders its canonical completed state.");
            Require(rows.Any(row => row.Key == "militia-barracks") == !hidden, "A nested hidden objective is not disclosed by the HUD checklist.");
            Require(rows.Single(row => row.Key == "primary").Label.Contains("1/2", StringComparison.Ordinal), "Overall progress uses the existing objective assessment.");
            Require(before == EcmaJson.Stringify(session.Snapshot()), "Objective checklist cannot advance battle facts.");
        }
    }

    private void CheckPrimaryOrders()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-07");
        var state = engine.CreateState(level);
        state["phase"] = "playing";
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        var queries = session.GetTacticalQueries(23);
        var actor = queries.Unit(queries.Actors().First())!;
        var control = new TacticalSelectionController(queries);
        var selected = control.Click(control.Stamp, TacticalHit.Unit(actor.At, actor.Id));
        Require(selected.Request is null && control.Selection.Kind == TacticalSelectionKind.Unit,
            "Selecting a ready C07 actor does not dispatch a battle action.");
        TacticalHudProjection Projection() => session.ProjectHud(control.Stamp, control.Selection, control.CursorHit, control.InspectId, false);
        var projection = Projection();
        var commands = queries.CommandsAt(actor.Id, actor.At);
        Require(projection.Sections.Single(section => section.Key == "commands").Rows.Where(row => row.Key != "cancel")
            .Select(row => row.Intent!.Key).SequenceEqual(commands.Select(command => command.Key)),
            "Ready-unit primary buttons are exactly canonical commands at its current foot cell.");
        var hud = new TacticalHud { Size = new Vector2(960, 640) };
        AddChild(hud);
        hud.Apply(projection);
        Require(hud.FindChild("ContextTabs", true, false) is null
            && hud.FindChildren("DossierButton", "Button", true, false).Count == 1,
            "Low-frequency details have one entry, not a duplicate skill-like primary strip.");
        TacticalRequest? request = null;
        hud.IntentRequested += intent => request = control.ChooseCommand(intent.Stamp, intent.Key!).Request;
        var wait = commands.Single(command => command.Ability == "wait");
        var button = hud.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(value => value.GetMeta("hud_key", "").AsString() == "commands/" + wait.Key);
        button.EmitSignal(BaseButton.SignalName.Pressed);
        Require(request?.Action is not null && session.Revision == 0,
            "Only the explicit Wait button produces a request; projection does not mutate the session.");
        var dispatched = session.Dispatch(request!.Action!.Value, request.Stamp.Revision);
        Require(dispatched.Accepted && session.Revision == 1,
            "The explicit primary Wait request commits through the real session once.");
        control.CompleteDispatch(control.Stamp, session.GetTacticalQueries(23));
        control.Click(control.Stamp, TacticalHit.Unit(actor.At, actor.Id));
        var acted = Projection();
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && acted.Sections.All(section => section.Key is not ("commands" or "tactics"))
            && acted.Sections.Single(section => section.Key == "unit").Rows.Any(row => row.Key == "cancel"),
            "An acted unit exposes state, health, dossier and close, never a fake actionable primary strip.");
        Require(control.ChooseCommand(control.Stamp, wait.Key).Request is null && session.Revision == 1,
            "Inspecting an acted unit cannot dispatch a second Wait.");
        RemoveChild(hud);
        hud.Free();
    }

    private void CheckPinnedUnit()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().First();
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var units = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var actor = units.First();
        var target = units.Last();
        var selected = actor["id"]!.GetValue<int>();
        var inspected = target["id"]!.GetValue<int>();
        var selection = new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = selected };
        var before = EcmaJson.Stringify(session.Snapshot());
        var hud = session.ProjectHud(new(0, 0), selection,
            TacticalHit.Unit(new(target["x"]!.GetValue<int>(), target["y"]!.GetValue<int>()), inspected), inspected, false);
        Require(hud.Sections.Single(section => section.Key == "unit").Title == session.UnitName(EcmaJson.ToElement(actor))
            && hud.Portrait?.UnitType == actor["type"]!.GetValue<string>(),
            "Hovering another unit cannot displace the acting unit's name, portrait and HP from the order tray.");
        Require(before == EcmaJson.Stringify(session.Snapshot()), "Pinned identity is only a projection, never a battle mutation.");
    }

    private async System.Threading.Tasks.Task CheckResponsiveLayout()
    {
        var host = new Control
        {
            Size = new Vector2(960, 640),
            Theme = TacticalHudStyle.Create()
        };
        AddChild(host);
        var hud = new TacticalHud
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ShowHint = false
        };
        host.AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var objective = new string('界', 80);
        var projection = new TacticalHudProjection(new(1, 1), "选择高亮地格", new[]
        {
            new TacticalHudSection("unit", "莱娅", new[] { new TacticalHudRow("selection", "预览未执行"), new("hp", "生命 116 / 116", Gauge: new(116, 116)) }),
            new TacticalHudSection("objectives", "作战目标", new[] { new TacticalHudRow("long", objective) }),
            new TacticalHudSection("forecast", "战斗预测", new[]
            {
                new TacticalHudRow("attacker", "灰旗卫士"), new("recipient", "戍斗法师"),
                new("strike", "造成伤害 36 · 80 → 44", Gauge: new(44, 80)),
                new("counter", "遭到反击 20 · 28 → 8", Gauge: new(8, 80))
            }),
            new TacticalHudSection("target", "选择目标", new[]
            {
                new TacticalHudRow("cancel", "取消 Esc", Intent: new(new(1, 1), TacticalHudIntentKind.Back))
            }),
            new TacticalHudSection("site", "村庄", new[] { new TacticalHudRow("site", "当前悬停地形") }),
            new TacticalHudSection("unit-details", "兵种与能力", new[] { new TacticalHudRow("long", objective) })
        });
        foreach (var width in new[] { 960, 1180 })
        {
            host.Size = new Vector2(width, 640);
            hud.Apply(projection);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var goal = ((VBoxContainer)hud.FindChild("objectives", true, false)).GetNode<VBoxContainer>("Content").GetChildren().OfType<HBoxContainer>().Single().GetChildren().OfType<Label>().Single();
            Require(goal.GetLineCount() > 1 && goal.GetGlobalRect().End.X <= host.GetGlobalRect().End.X,
                "Long objectives wrap within the narrow and wide native panel, rather than clipping horizontally.");
            var unitCard = (Control)hud.FindChild("unit", true, false);
            var prediction = (Control)hud.FindChild("forecast", true, false);
            Require(unitCard.GetGlobalRect().Position.Y >= 500 && prediction.GetGlobalRect().Position.Y >= 514 && prediction.GetGlobalRect().End.Y <= 632,
                "The acting unit and forecast stay in the bottom safe area, never covering a hovered map target.");
            var hp = unitCard.FindChildren("*", "Label", true, false).OfType<Label>().Single(label => label.Text.StartsWith("生命", StringComparison.Ordinal));
            var dossierButton = (Button)hud.FindChild("DossierButton", true, false);
            Require(dossierButton.Text == "兵种与能力", "An acting unit owns its dossier entry label even while a village site is hovered.");
            Require(hp.GetGlobalRect().End.Y + 4 <= dossierButton.GlobalPosition.Y
                && hp.GetGlobalRect().End.X <= 274,
                "Current/max HP remains fully visible above the dossier button at both window sizes.");
            var pair = (HBoxContainer)hud.FindChild("Exchange", true, false);
            var left = pair.GetChild<Control>(0).GetGlobalRect();
            var right = pair.GetChild<Control>(1).GetGlobalRect();
            Require(left.End.X <= right.Position.X && right.End.X <= host.GetGlobalRect().End.X,
                "Both forecast recipients remain separate and readable without horizontal overflow.");
            var cancel = ((HBoxContainer)prediction.FindChild("Heading", true, false)).GetChildren().OfType<Button>().Single();
            Require(cancel.Size.Y < 40 && cancel.GetMeta("hud_key").AsString() == "target/cancel",
                "Target cancellation retains its semantic key in the compact forecast header.");
            var detailScroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
            var pinnedPosition = unitCard.GlobalPosition;
            detailScroll.ScrollVertical = 100;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(unitCard.GlobalPosition == pinnedPosition
                && !detailScroll.IsAncestorOf(prediction), "Scrolling goals and details never displaces acting-unit or forecast context.");
            var details = ((VBoxContainer)hud.FindChild("unit-details", true, false));
            var toggle = details.GetChildren().OfType<Button>().Single();
            Require(!details.GetChildren().OfType<VBoxContainer>().Single().Visible, "Secondary details start collapsed.");
            toggle.ButtonPressed = true;
            ((PopupPanel)hud.FindChild("UnitDossier", true, false)).Popup();
            toggle.GrabFocus();
            hud.Apply(projection);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            details = ((VBoxContainer)hud.FindChild("unit-details", true, false));
            Require(details.GetChildren().OfType<VBoxContainer>().Single().Visible
                && details.GetChildren().OfType<Button>().Single().HasFocus(), "Refresh preserves disclosure state and keyboard focus.");
            details.GetChildren().OfType<Button>().Single().ButtonPressed = false;
            ((PopupPanel)hud.FindChild("UnitDossier", true, false)).Hide();
            hud.ShowHint = true;
            hud.Apply(new(new(1, 1), GodotTextCatalog.LoadSimplifiedChinese().Format("ui.hud.selection.unit"), new[]
            {
                new TacticalHudSection("commands", "指令", new[]
                {
                    new TacticalHudRow("wait", "待机", Intent: new(new(1, 1), TacticalHudIntentKind.Command, "wait")),
                    new("cancel", "取消 Esc", Intent: new(new(1, 1), TacticalHudIntentKind.Back))
                })
            }));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var tray = (Control)hud.FindChild("OrderTray", true, false);
            var hint = (Label)hud.FindChild("SelectionHint", true, false);
            Require(hint.GetGlobalRect().Position.X >= tray.GetGlobalRect().Position.X + 10
                && hint.GetGlobalRect().End.X <= tray.GetGlobalRect().End.X - 10
                && hint.GetGlobalRect().End.Y <= tray.GetGlobalRect().End.Y,
                "A single command still reserves a complete material-backed instruction and cancel area.");
            Require(hint.GetThemeFont("font").GetStringSize(hint.Text, fontSize: hint.GetThemeFontSize("font_size")).X <= hint.Size.X,
                "The localized move/attack instruction fits fully without ellipsis at both native window sizes.");
            Require(hint.TooltipText == hint.Text, "The instruction remains accessible through its complete tooltip.");
            hud.ShowHint = false;
        }
        RemoveChild(host);
        host.Free();
    }

    private async System.Threading.Tasks.Task CheckForecastFacts()
    {
        var host = new Control { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
        AddChild(host);
        var hud = new TacticalHud();
        host.AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var stamp = new TacticalStamp(90, 1);
        var warningRows = new[] {
            new TacticalHudRow("interceptor", "皇家卫士将进行援护并承受伤害"),
            new TacticalHudRow("reaction", "目标将触发防守姿态"),
            new TacticalHudRow("support", "援护攻击预计追加 21 点伤害"),
            new TacticalHudRow("collateral", "范围攻击还将波及 2 个单位、1 个结构"),
            new TacticalHudRow("recipient-dies", "主击目标预计阵亡"),
            new TacticalHudRow("attacker-dies", "攻击者预计阵亡") };
        foreach (var width in new[] { 960, 1180 })
        {
            host.Size = new Vector2(width, 640);
            hud.Apply(new(stamp with
            {
                Revision = width
            }, "再点目标 / Enter 执行 · Esc 返回", new[] {
                new TacticalHudSection("forecast", "战斗预测", new[] {
                    new TacticalHudRow("attacker", "北丘边境弓手"), new TacticalHudRow("recipient", "军团皇家盾卫"),
                    new TacticalHudRow("counter", "反击 20 · 28 → 8", Gauge: new(8, 80)),
                    new TacticalHudRow("strike", "主击 36 · HP 80 → 44", Gauge: new(44, 80)) }.Concat(warningRows).ToArray()),
                new TacticalHudSection("forecast-details", "计算详情", new[] { new TacticalHudRow("damage-type", "穿刺 → 重甲"), new TacticalHudRow("mitigation", "减伤 20%") }),
                new TacticalHudSection("target", "选择目标", new[] { new TacticalHudRow("cancel", "取消 Esc", Intent: new(stamp, TacticalHudIntentKind.Back)) })
            }));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var plate = (Control)hud.FindChild("forecastPlate", true, false);
            var effects = (GridContainer)hud.FindChild("ForecastEffects", true, false);
            Require(effects.GetChildCount() == warningRows.Length && effects.GetChildren().OfType<Label>().All(label =>
                label.IsVisibleInTree() && plate.GetGlobalRect().Encloses(label.GetGlobalRect())),
                "Interception, reaction, support, collateral and death facts are actually visible inside the forecast plate, not tooltip-only.");
            Require(plate.GlobalPosition.Y >= 320 && plate.GetGlobalRect().End.Y <= host.Size.Y,
                "Even the fullest forecast remains in the lower safe half at both widths.");
            var hint = (Label)hud.FindChild("SelectionHint", true, false);
            Require(hint.IsVisibleInTree() && plate.GetGlobalRect().Encloses(hint.GetGlobalRect()), "Confirmation and escape guidance stays visible with the forecast.");
            var detail = (Button)hud.FindChild("ForecastDetailsButton", true, false);
            detail.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            var calculation = (VBoxContainer)hud.FindChild("forecast-details", true, false);
            Require(popup.Visible && calculation.GetNode<VBoxContainer>("Content").Visible,
                "Direct forecast details opens the current calculation already expanded.");
            popup.Hide();
        }
        RemoveChild(host);
        host.Free();
    }

    private async System.Threading.Tasks.Task CheckPointerCapture()
    {
        GetWindow().Size = new Vector2I(960, 640);
        var hud = new TacticalHud { Size = new Vector2(960, 640) };
        AddChild(hud);
        var stamp = new TacticalStamp(70, 2);
        TacticalHudProjection Frame(string detail) => new(stamp, "选择指令", new[]
        {
            new TacticalHudSection("commands", "指令", new[] { new TacticalHudRow("wait", "待机", Intent: new(stamp, TacticalHudIntentKind.Command, "wait")) }),
            new TacticalHudSection("tile", "地形", new[] { new TacticalHudRow("terrain", detail) })
        });
        hud.Apply(Frame("道路"));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var button = (Button)hud.FindChild("Orders", true, false).GetChild(0);
        var point = button.GetGlobalRect().GetCenter();
        var fired = 0;
        hud.IntentRequested += _ => fired++;
        GetViewport().NotifyMouseEntered();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        hud.Apply(Frame("道路"));
        Require(ReferenceEquals(button, hud.FindChild("Orders", true, false).GetChild(0)), "Equivalent projection retains the actual native button.");
        hud.Apply(Frame("草地"));
        Require(ReferenceEquals(button, hud.FindChild("Orders", true, false).GetChild(0)), "Hover detail changes retain the pressed command subtree without detaching it.");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        Require(fired == 1, "Native pointer press/release across hover refresh submits the first click exactly once.");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        Require(fired == 1, "A duplicate release cannot submit a second order.");
        RemoveChild(hud);
        hud.Free();
    }

    private async System.Threading.Tasks.Task CheckDossierLifetime()
    {
        GetWindow().Size = new Vector2I(960, 640);
        var hud = new TacticalHud { Size = new Vector2(960, 640) };
        AddChild(hud);
        var stamp = new TacticalStamp(80, 1);
        TacticalHudProjection Frame(string terrain) => new(stamp, "查看单位", new[]
        {
            new TacticalHudSection("unit", "米蕾尔", new[] { new TacticalHudRow("hp", "生命 78 / 78") }),
            new TacticalHudSection("unit-details", "兵种与能力", new[] { new TacticalHudRow("ability", "治疗") }),
            new TacticalHudSection("tile", "地形", new[] { new TacticalHudRow("terrain", terrain) })
        });
        hud.Apply(Frame("道路"));
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        var scroll = hud.FindChild("DetailScroll", true, false);
        var staleEntry = (Button)hud.FindChild("DossierButton", true, false);
        staleEntry.EmitSignal(Button.SignalName.Pressed);
        hud.Apply(Frame("同一战局版本的新检查对象"));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!popup.Visible, "Deferred dossier opening cannot cross a newer HUD projection with the same battle stamp.");
        var detachedEntry = (Button)hud.FindChild("DossierButton", true, false);
        detachedEntry.EmitSignal(Button.SignalName.Pressed);
        hud.RemoveChild(detachedEntry);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!popup.Visible, "Deferred dossier opening cannot act after its button is detached.");
        detachedEntry.Free();
        hud.Apply(Frame("重新挂载入口"));
        var showCount = 0;
        popup.VisibilityChanged += () =>
        {
            if (popup.Visible)
                hud.Apply(Frame("焦点变化 " + ++showCount));
        };
        for (var index = 0; index < 3; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var entry = (Button)hud.FindChild("DossierButton", true, false);
            var point = entry.GetGlobalRect().GetCenter();
            var viewport = GetViewport();
            viewport.NotifyMouseEntered();
            viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(popup.Visible && ReferenceEquals(popup, hud.FindChild("UnitDossier", true, false))
                && ReferenceEquals(scroll, hud.FindChild("DetailScroll", true, false)),
                "Opening a native dossier may synchronously refresh hover facts without replacing its registered subwindow or scroll owner.");
            var details = (VBoxContainer)hud.FindChild("unit-details", true, false);
            var toggle = details.GetChildren().OfType<Button>().Single();
            toggle.ButtonPressed = false;
            var togglePoint = toggle.GetGlobalRect().GetCenter() + (Vector2)popup.Position;
            viewport.NotifyMouseEntered();
            viewport.PushInput(new InputEventMouseMotion { Position = togglePoint, GlobalPosition = togglePoint }, true);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = togglePoint, GlobalPosition = togglePoint }, true);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = togglePoint, GlobalPosition = togglePoint }, true);
            Require(toggle.ButtonPressed, "Native dossier disclosure press/release expands the content.");
            hud.Apply(Frame("展开后地形 " + index));
            Require(popup.Visible && ((VBoxContainer)hud.FindChild("unit-details", true, false)).GetNode<VBoxContainer>("Content").Visible,
                "Expanded dossier remains visible across refreshed facts without reopening the subwindow.");
            popup.Hide();
            Require(!popup.Visible, "The persistent dossier still closes normally.");
        }
        RemoveChild(hud);
        hud.Free();
    }

    private async System.Threading.Tasks.Task CheckOrderBadgeStates()
    {
        GetWindow().Size = new Vector2I(960, 640);
        var button = new Button { Text = "待机", Position = new Vector2(100, 100), Size = new Vector2(98, 54), Theme = TacticalHudStyle.Create() };
        TacticalHudStyle.OrderBadge(button);
        AddChild(button);
        var badge = button.GetChildren().OfType<TacticalOrderBadge>().Single();
        var redraws = 0;
        badge.Draw += () => redraws++;
        async System.Threading.Tasks.Task DrawFrame()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var viewport = GetViewport();
        viewport.NotifyMouseEntered();
        void Move(Vector2 point) => viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        void Mouse(bool down, Vector2 point) => viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = point, GlobalPosition = point }, true);
        Move(new Vector2(20, 20));
        await DrawFrame();
        var normal = badge.SurfaceColor;
        var point = button.GetGlobalRect().GetCenter();
        Move(point);
        await DrawFrame();
        Require(badge.VisualMode == BaseButton.DrawMode.Hover && badge.SurfaceColor != normal, "Order badge renders native pointer hover distinctly from resting state.");
        var hover = badge.SurfaceColor;
        Mouse(true, point);
        await DrawFrame();
        Require(badge.VisualMode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed && badge.SurfaceColor != hover, "Non-toggle pointer down has a distinct native pressed surface before release.");
        Mouse(false, point);
        Move(new Vector2(20, 20));
        button.GrabFocus();
        await DrawFrame();
        Require(badge.FocusVisible && button.GetThemeStylebox("focus") is StyleBoxEmpty, "Keyboard focus uses the shield contour rather than a conflicting rectangular frame.");
        var actions = 0;
        button.Pressed += () => actions++;
        viewport.PushInput(new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = true }, true);
        await DrawFrame();
        Require(badge.VisualMode == BaseButton.DrawMode.Pressed && badge.SurfaceColor != normal, "Focused keyboard down uses the same pressed feedback as pointer activation.");
        viewport.PushInput(new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = false }, true);
        await DrawFrame();
        Require(actions == 1 && badge.FocusVisible && badge.VisualMode == BaseButton.DrawMode.Normal, "Keyboard release fires once and retains focus without leaving the badge pressed.");
        var beforeDisable = redraws;
        button.Disabled = true;
        await DrawFrame();
        Require(badge.VisualMode == BaseButton.DrawMode.Disabled && !badge.FocusVisible && redraws > beforeDisable, "Programmatic disable redraws the existing badge and suppresses focus emphasis without a pointer event.");
        Mouse(true, point);
        Mouse(false, point);
        Require(actions == 1, "A disabled badge cannot execute an order.");
        button.Disabled = false;
        Move(new Vector2(20, 20));
        button.ReleaseFocus();
        await DrawFrame();
        Require(badge.VisualMode == BaseButton.DrawMode.Normal && badge.SurfaceColor == normal && !badge.FocusVisible, "Re-enabling and clearing focus restores the original surface without stale highlight.");
        button.ToggleMode = true;
        button.ButtonPressed = true;
        await DrawFrame();
        Require(badge.VisualMode == BaseButton.DrawMode.Pressed, "Programmatic toggle selection uses the same native visual state owner.");
        RemoveChild(button);
        button.Free();
    }

    public override async void _Ready()
    {
        try
        {
            CheckInspectionOrder();
            CheckObjectiveChecklist();
            CheckPinnedUnit();
            CheckPrimaryOrders();
            CheckSiteInspection();
            await CheckSitePointerBoundary();
            await CheckResponsiveLayout();
            await CheckForecastFacts();
            await CheckPointerCapture();
            await CheckDossierLifetime();
            await CheckOrderBadgeStates();
            var stamp = new TacticalStamp(17, 9);
            var hud = new TacticalHud();
            AddChild(hud);
            var fired = 0;
            hud.IntentRequested += intent => { Require(intent.Stamp == stamp, "Exact stamp emitted"); fired++; };
            TacticalHudProjection Projection(bool enabled = true) => new(stamp, "下一步", new[] { new TacticalHudSection("commands", "指令", new[] { new TacticalHudRow("wait", "待命", "原始提示", enabled, Intent: new(stamp, TacticalHudIntentKind.Command, "wait")) }) });
            Button Button() => ((GridContainer)hud.FindChild("Orders", true, false)).GetChildren().OfType<Button>().Single();
            hud.Apply(Projection());
            var old = Button();
            Require(old.TooltipText == "原始提示", "Native reason tooltip");
            old.EmitSignal(BaseButton.SignalName.Pressed);
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "One-shot duplicate signal suppression");
            hud.Apply(Projection());
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Equivalent refresh does not rearm a submitted native row");
            var mounted = Button();
            RemoveChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached widget inert");
            AddChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Remount without Apply inert");
            hud.Apply(Projection(false));
            var disabled = Button();
            Require(disabled.Disabled, "Native disabled row");
            disabled.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Disabled emitted signal inert");
            hud.Apply(Projection() with
            {
                Busy = true
            });
            var busy = Button();
            busy.EmitSignal(BaseButton.SignalName.Pressed);
            Require(busy.Disabled && fired == 1, "Busy projection cannot emit tactical input");
            hud.Apply(Projection());
            var detached = Button();
            detached.GetParent().RemoveChild(detached);
            detached.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached child inert");
            detached.QueueFree();
            hud.Apply(Projection() with
            {
                Busy = true
            });
            hud.Apply(Projection());
            Button().GrabFocus();
            hud.Apply(Projection());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(Button().HasFocus(), "Stable row focus restored");
            var portraits = PortraitCatalogResources.Load();
            var metadata = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            foreach (var image in metadata.Icons.Values.Concat(metadata.Portraits.Values.Where(value => value.PortraitId is not null).Select(value => portraits.Resolve(value.PortraitId!))))
            {
                var bytes = Godot.FileAccess.GetFileAsBytes(image.ResourcePath!);
                Require(bytes.Length > 0 && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant() == image.Sha256, "Original HUD resource hash: " + image.ResourcePath);
            }
            var assets = AssetCatalog.Load();
            var explicitDocument = new TacticalHudArtDocument
            {
                Schema = 2,
                Portraits = new() { ["c01.mirelle"] = new() { UnitType = "c01.mirelle", PortraitId = "explicit-card", PortraitRegion = new[] { 330, 40, 500, 500 } } }
            };
            var explicitCatalog = new PortraitCatalog(new Dictionary<string, PortraitAsset> { ["explicit-card"] = portraits.Portraits["mirelle"] });
            var explicitArt = new TacticalHudArt(assets, explicitCatalog, explicitDocument);
            Require(((AtlasTexture)explicitArt.Portrait(new("c01.mirelle", null, "#224466"))).Atlas.ResourcePath == explicitCatalog.Resolve("explicit-card").ResourcePath,
                "HUD uses the injected shared catalog and explicit ID, not unit-name inference");
            try
            {
                _ = new TacticalHudArt(assets, new PortraitCatalog(new Dictionary<string, PortraitAsset> { ["different-card"] = portraits.Portraits["mirelle"] }), explicitDocument);
                throw new Exception("Unknown portrait silently fell back to a frame");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("Unknown portrait ID", StringComparison.Ordinal)) { assertions++; }
            try
            {
                System.Text.Json.JsonSerializer.Deserialize<TacticalPortraitBinding>("{\"unitType\":\"c01.mirelle\"}",
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                throw new Exception("Omitted portrait ID silently became frame semantics");
            }
            catch (System.Text.Json.JsonException) { assertions++; }
            foreach (var region in new int[]?[] { null, new[] { -1, 0, 100, 100 }, new[] { 0, 0, 100, 90 }, new[] { 0, 0, 10000, 10000 } })
            {
                var invalid = new TacticalHudArtDocument
                {
                    Schema = 2,
                    Portraits = new() { ["c01.mirelle"] = new() { UnitType = "c01.mirelle", PortraitId = "explicit-card", PortraitRegion = region } }
                };
                try
                {
                    _ = new TacticalHudArt(assets, explicitCatalog, invalid);
                    throw new Exception("Invalid named portrait crop was accepted.");
                }
                catch (InvalidOperationException error) when (error.Message.Contains("in-bounds square", StringComparison.Ordinal)) { assertions++; }
            }
            try
            {
                _ = new TacticalHudArt(assets, explicitCatalog, new TacticalHudArtDocument
                {
                    Schema = 2,
                    Portraits = new() { ["c01.swordsman"] = new() { UnitType = "c01.swordsman", PortraitId = null, PortraitRegion = new[] { 0, 0, 99999, 99999 } } }
                });
                throw new Exception("A generic frame accepted an out-of-bounds local crop.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("in-bounds square", StringComparison.Ordinal)) { assertions++; }
            var art = TacticalHudArt.Load(assets, portraits);
            hud.ConfigureArt(art);
            TextureRect MountedPortrait() => (TextureRect)hud.FindChild("InspectedPortrait", true, false);
            foreach (var binding in metadata.Portraits.Values.Where(value => value.PortraitId is not null))
            {
                var namedPortrait = new TacticalHudPortrait(binding.UnitType, binding.ActorKey, "#224466");
                var source = ResourceLoader.Load<Texture2D>(portraits.Resolve(binding.PortraitId!).ResourcePath!);
                var crop = (AtlasTexture)art.Portrait(namedPortrait);
                var region = binding.PortraitRegion!;
                Require(crop.Atlas == source && crop.Region == new Rect2(region[0], region[1], region[2], region[3])
                    && crop.GetSize() == new Vector2(region[2], region[3]) && art.Portrait(namedPortrait) == crop,
                    "Every named HUD portrait uses its validated, cached head-and-shoulders crop without changing the narrative source: " + binding.PortraitId);
                hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, namedPortrait));
                var mountedPortrait = MountedPortrait();
                Require(mountedPortrait.Texture == crop && mountedPortrait.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered,
                    "Every named HUD crop preserves square aspect in its medallion: " + binding.PortraitId);
            }
            var named = new TacticalHudPortrait("c01.swordsman", "campaign-torren", "#224466");
            var portrait = art.Portrait(named);
            var genericPortrait = new TacticalHudPortrait("c01.swordsman", null, "#224466");
            var generic = art.Portrait(genericPortrait) as AtlasTexture;
            var unit = assets.ResolveUnit(genericPortrait.UnitType, genericPortrait.ActorKey);
            foreach (var (key, binding) in metadata.Portraits.Where(pair => pair.Value.PortraitId is null && pair.Value.PortraitRegion is not null))
            {
                var frameAsset = assets.ResolveUnit(binding.UnitType, binding.ActorKey);
                var frame = assets.CreateFrameTexture(frameAsset, AssetCatalog.FrameIndex(frameAsset, "idle"));
                var actual = art.Portrait(new(binding.UnitType, binding.ActorKey, "#224466"));
                if (binding.PortraitRegion is not { } region)
                    Require(actual == frame, "Null generic crop preserves its full idle frame: " + key);
                else
                    Require(actual is AtlasTexture cropped && cropped.Atlas == frame.Atlas && cropped.Atlas is not AtlasTexture
                        && cropped.Region == new Rect2(frame.Region.Position + new Vector2(region[0], region[1]), new Vector2(region[2], region[3])),
                        "Generic head crops remain frame-local and flatten onto the original sheet: " + key);
            }
            var wholeFrameArt = new TacticalHudArt(assets, portraits, new TacticalHudArtDocument
            {
                Schema = 2,
                Portraits = new() { ["c01.swordsman"] = new() { UnitType = "c01.swordsman", PortraitId = null, PortraitRegion = null } }
            });
            Require(wholeFrameArt.Portrait(genericPortrait) == assets.CreateFrameTexture(unit, AssetCatalog.FrameIndex(unit, "idle")),
                "Explicit null generic crop preserves the original idle texture.");
            var shiftedManifest = PresentationJson.Load<JsonObject>("res://data/selected-assets.json");
            var archerRecord = shiftedManifest["assets"]!.AsArray().OfType<JsonObject>().Single(value =>
                value["topicId"]!.GetValue<string>() == "C01-UNIT-ARCHER" && value["id"] is null);
            archerRecord["frameOrder"] = new JsonArray("walk-a", "idle", "attack", "walk-b");
            var shiftedAssets = new AssetCatalog(System.Text.Json.JsonSerializer.Deserialize<AssetManifest>(shiftedManifest.ToJsonString(),
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!,
                PresentationJson.Load<ArtBindings>("res://data/art-bindings.json"));
            var shiftedArt = new TacticalHudArt(shiftedAssets, portraits, metadata);
            var shiftedCrop = (AtlasTexture)shiftedArt.Portrait(new("c01.archer", "elin", "#224466"));
            var shiftedFrame = shiftedAssets.CreateFrameTexture(shiftedAssets.ResolveUnit("c01.archer"), 1);
            Require(shiftedCrop.Region.Position == shiftedFrame.Region.Position + new Vector2(140, 140)
                && shiftedCrop.Atlas == shiftedFrame.Atlas && shiftedCrop.Atlas is not AtlasTexture,
                "Nonzero idle-region origin is applied once, without nested atlases.");
            Require(generic!.Atlas.ResourcePath != ((AtlasTexture)portrait).Atlas.ResourcePath && art.UnitIcon(named) is AtlasTexture,
                "Generic portraits and unit icons remain sprite frames, distinct from named head-and-shoulders portraits.");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, genericPortrait));
            var oldPortrait = MountedPortrait();
            for (var index = 0; index < 100; index++)
                hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            Require(oldPortrait.Texture is null && MountedPortrait().Texture == portrait && art.Portrait(named) == portrait,
                "Repeated projections release old control references and reuse the art owner’s cached portrait resource.");
            oldPortrait = MountedPortrait();
            var replacement = TacticalHudArt.Load(assets, portraits);
            hud.ConfigureArt(replacement);
            Require(oldPortrait.Texture is null && GodotObject.IsInstanceValid(portrait) && GodotObject.IsInstanceValid(generic),
                "Rebinding releases control references, not shared catalog textures or idle-frame atlases.");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            var lastPortrait = MountedPortrait();
            RemoveChild(hud);
            Require(lastPortrait.Texture is null && ((AtlasTexture)replacement.Portrait(named)).Atlas == ((AtlasTexture)portrait).Atlas && art.Portrait(genericPortrait) == generic,
                "HUD teardown leaves shared named and generic resources usable by their owners.");
            hud.QueueFree();
            var missing = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            var required = missing.SourceIconKeys[0];
            missing.Icons.Remove(required);
            var broken = new TacticalHudArt(assets, portraits, missing);
            try
            {
                broken.Icon(required);
                throw new Exception("Missing declared icon silently disappeared");
            }
            catch (MissingArtResourceException) { assertions++; }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"TACTICAL_HUD_CHECKS_OK {assertions} assertions (headless native controls, not visual acceptance)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
