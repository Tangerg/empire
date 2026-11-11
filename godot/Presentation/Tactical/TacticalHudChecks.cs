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
            Require(hud.DossierSubject == TacticalHit.Object(at), "The projection owns the inspected structure identity independently of rendered text.");
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
        Require(recruit.DossierSubject == TacticalHit.Object(site), "Recruitment retains its pinned site identity.");
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

    private async System.Threading.Tasks.Task CheckSiteInspection()
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
        var siteStep = control.Key(control.Stamp, "d");
        Require(control.Selection.Kind == TacticalSelectionKind.Site && control.Selection.Destination == home
            && control.Cursor == new BoardCell(39, 23) && siteStep.Reveal == control.Cursor && siteStep.CursorFocused
            && siteStep.Request is null,
            "A keyboard step resumes from the existing site coordinate, not an unrelated inspected actor or a fabricated origin.");
        control.Leave(control.Stamp);
        var hudWidget = new TacticalHud();
        AddChild(hudWidget);
        hudWidget.IntentRequested += intent => control.Back(intent.Stamp);
        hudWidget.Apply(Hud());
        var close = ((VBoxContainer)hudWidget.FindChild("site", true, false)).GetChildren().OfType<Button>().Single();
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        await CheckShortHudBoundary(screen, session, map, hud);
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
        foreach (var pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventKey { Keycode = Key.D, PhysicalKeycode = Key.D, Pressed = pressed }, true);
        GD.Print($"SITE_KEYBOARD_CONTEXT C01-03: selection={screen.Selection?.Kind}; selected={screen.Selection?.Destination}; revision={session.Revision}");
        Require(screen.Selection is { Kind: TacticalSelectionKind.Site, Destination: { X: 38, Y: 23 } }
            && session.Revision == 0 && session.Events.Count == 0,
            "Keyboard navigation after leaving a pinned formal site must preserve its inspection instead of selecting an unrelated actor.");
        Motion(outside);
        ((VBoxContainer)hud.FindChild("site", true, false)).GetChildren().OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

    private async System.Threading.Tasks.Task CheckStructurePointerInspection()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-09");
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var snapshot = session.Snapshot();
        var original = EcmaJson.Stringify(snapshot);
        Require(StateDigest.Hash(snapshot) == "6fefe68e", "Structure inspection uses the unchanged formal C01-09 opening.");
        var west = new BoardCell(5, 6);
        var north = new BoardCell(7, 4);
        var queries = session.GetTacticalQueries(0);
        Require(snapshot["structures"]!.AsArray().OfType<JsonObject>().Any(value => value["id"]!.GetValue<string>() == "chain-west"
                && value["x"]!.GetValue<int>() == west.X && value["y"]!.GetValue<int>() == west.Y)
            && !queries.IsSite(west) && queries.IsSite(north),
            "The native regression contrasts the real standalone chain-west with capturable chain-north, without changing either terrain.");
        var windowSize = GetWindow().Size;
        var screen = new BattleScreen();
        AddChild(screen);
        async System.Threading.Tasks.Task Settle()
        {
            for (var frame = 0; frame < 3; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void Motion(Vector2 at)
        {
            using var input = new InputEventMouseMotion { Position = at, GlobalPosition = at };
            Input.ParseInputEvent(input);
        }
        void Click(Vector2 at)
        {
            foreach (var down in new[] { true, false })
            {
                using var input = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at };
                Input.ParseInputEvent(input);
            }
        }
        void KeyPress(Key key)
        {
            foreach (var down in new[] { true, false })
            {
                using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down };
                Input.ParseInputEvent(input);
            }
        }
        void Unchanged(string step) => Require(session.Revision == 0 && session.Events.Count == 0 && session.ActionLog.Count == 0
            && EcmaJson.Stringify(session.Snapshot()) == original,
            "Native structure inspection cannot change state, revision, events or the action log: " + step);
        try
        {
            GetWindow().Size = new Vector2I(1280, 812);
            var scene = AuthoredScene.Load("res://data/scenes/c01-09.json");
            var rendering = BoardSceneRendering.Load(scene, level, snapshot["map"]!.AsObject());
            screen.BindSession(new(presentation.Messages, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
            screen.ConfigureGround(rendering.Ground);
            screen.ConfigureDynamicState();
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(1280, 812);
            await Settle();
            var map = (Control)screen.FindChild("MapArea", true, false);
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var viewport = GetViewport();
            viewport.NotifyMouseExited();
            viewport.NotifyMouseEntered();
            viewport.GuiReleaseFocus();
            Button Dossier() => (Button)hud.FindChild("DossierButton", true, false);
            TacticalHudSection Structure() => hud.Projection!.Sections.Single(section => section.Key == "structure");
            async System.Threading.Tasks.Task<Vector2?> FindPixel(TacticalHit hit)
            {
                var origin = screen.Board.Projection.CellContact(new(hit.Cell.X, hit.Cell.Y));
                for (var radius = 0; radius <= 120; radius += 3)
                    for (var x = -radius; x <= radius; x += 3)
                        foreach (var y in radius == 0 ? new[] { 0 } : new[] { -radius, radius })
                        {
                            var point = origin + new Vector2(x, y);
                            if (screen.Board.Pick(point) != hit)
                                continue;
                            var global = screen.Board.GetGlobalTransformWithCanvas() * point;
                            if (!map.GetGlobalRect().HasPoint(global))
                                continue;
                            Motion(global);
                            await Settle();
                            if (viewport.GuiGetHoveredControl() == map)
                                return global;
                        }
                return null;
            }
            async System.Threading.Tasks.Task ClickMap(Vector2 at)
            {
                Motion(at);
                await Settle();
                Require(viewport.GuiGetHoveredControl() == map, "A native structure-flow click must reach the actual map, not overlapping HUD chrome.");
                Click(at);
                await Settle();
            }
            async System.Threading.Tasks.Task ClickControl(Button button)
            {
                var at = button.GetGlobalRect().GetCenter();
                if (button.GetWindow() != GetWindow())
                    at += new Vector2(button.GetWindow().Position.X, button.GetWindow().Position.Y);
                Motion(at);
                await Settle();
                Require(button.GetViewport().GuiGetHoveredControl() == button,
                    "Native structure-flow pointer must reach the actual control: " + button.Name);
                Click(at);
                await Settle();
            }
            async System.Threading.Tasks.Task TabTo(Button button)
            {
                var route = new System.Collections.Generic.List<string>();
                var limit = screen.FindChildren("*", "Button", true, false).Count + 2;
                for (var index = 0; index < limit && !button.HasFocus(); index++)
                {
                    KeyPress(Key.Tab);
                    await Settle();
                    route.Add(button.GetViewport().GuiGetFocusOwner()?.Name.ToString() ?? "none");
                }
                Require(button.HasFocus(), "Real native Tab must reach " + button.Name + "; focus route: " + string.Join(", ", route));
            }

            var westPixel = await FindPixel(TacticalHit.Object(west));
            Require(westPixel is not null, "The unchanged chain-west has an opaque, native-reachable Object pixel.");
            await ClickMap(westPixel!.Value);
            var westSection = Structure();
            void WestPinned(string step)
            {
                Require(screen.Selection is { Kind: TacticalSelectionKind.Structure, Destination: { X: 5, Y: 6 } }
                    && hud.Projection!.Sections.SingleOrDefault(section => section.Key == "structure") is { } section
                    && section.Title == westSection.Title && section.Rows.SequenceEqual(westSection.Rows)
                    && hud.Projection.Sections.All(section => section.Key is not ("site" or "unit"))
                    && hud.Projection.Sections.Single(section => section.Key == "tile").Rows.Single(row => row.Key == "coordinate").Label == "5, 6",
                    "Standalone structure selection and current facts must stay pinned: " + step);
                Unchanged(step);
            }
            WestPinned("opaque chain-west click");
            Require(Dossier().Text == westSection.Title, "The selected structure owns its dossier entry label instead of the general roster.");
            Motion(Dossier().GetGlobalRect().GetCenter());
            await Settle();
            Require(viewport.GuiGetHoveredControl() == Dossier(), "The real pointer reaches the structure dossier entry.");
            WestPinned("map-to-dossier pointer handoff");
            await ClickControl(Dossier());
            Require(hud.DossierVisible && hud.FindChild("structure", true, false) is Control mounted && mounted.IsVisibleInTree(),
                "Actual native dossier activation exposes the pinned standalone structure inside the window.");
            WestPinned("opened dossier");
            await ClickControl((Button)hud.FindChild("DossierClose", true, false));
            Require(!hud.DossierVisible, "The actual dossier Close button dismisses its window.");
            WestPinned("dossier dismissal preserves selection");
            TacticalUnit? actor = null;
            Vector2? actorPixel = null;
            foreach (var id in queries.Actors())
            {
                var candidate = queries.Unit(id)!;
                actorPixel = await FindPixel(TacticalHit.Unit(candidate.At, candidate.Id));
                if (actorPixel is null)
                    continue;
                actor = candidate;
                break;
            }
            Require(actor is not null && actorPixel is not null, "The unchanged C01-09 has a native-reachable friendly actor for incidental hover and explicit switching.");
            WestPinned("real friendly-unit hover");
            Vector2? groundPixel = null;
            foreach (var cell in queries.InteractiveCells.OrderBy(cell => Math.Abs(cell.X - west.X) + Math.Abs(cell.Y - west.Y)))
            {
                var point = screen.Board.Projection.CellCenter(new(cell.X, cell.Y));
                var global = screen.Board.GetGlobalTransformWithCanvas() * point;
                if (cell == west || !map.GetGlobalRect().HasPoint(global) || screen.Board.Pick(point) != TacticalHit.Ground(cell))
                    continue;
                Motion(global);
                await Settle();
                if (viewport.GuiGetHoveredControl() != map)
                    continue;
                groundPixel = global;
                break;
            }
            Require(groundPixel is not null, "The native structure flow includes a genuine painted Ground hover.");
            WestPinned("real ground hover");
            GetWindow().EmitSignal(Window.SignalName.MouseExited);
            await Settle();
            WestPinned("native window leave");
            var northPixel = await FindPixel(TacticalHit.Object(north));
            Require(northPixel is not null, "The unchanged chain-north has an opaque, native-reachable Object pixel.");
            await ClickMap(northPixel!.Value);
            var northSection = Structure();
            await ClickControl(Dossier());
            Require(screen.Selection is { Kind: TacticalSelectionKind.Site, Destination: { X: 7, Y: 4 } }
                && hud.DossierVisible && ((Control)hud.FindChild("structure", true, false)).IsVisibleInTree()
                && Structure().Title == northSection.Title && Structure().Rows.SequenceEqual(northSection.Rows)
                && hud.Projection!.Sections.Any(section => section.Key == "site"),
                "The real capturable chain-north remains a Site and retains its structure information through the same native dossier route.");
            Unchanged("capturable Site positive control");
            await ClickControl((Button)hud.FindChild("DossierClose", true, false));
            await ClickMap(westPixel.Value);
            WestPinned("switching from Site to standalone Structure");
            GD.Print("STRUCTURE_POINTER_INSPECTION_OK C01-09 chain-west / chain-north; actual mouse handoff, popup Close, hover and window leave; no committed actions");
            Require(viewport.GuiGetFocusOwner() is null, "A real map click naturally releases HUD focus before keyboard inspection.");
            await TabTo(Dossier());
            WestPinned("native Tab focus");
            KeyPress(Key.Enter);
            await Settle();
            Require(hud.DossierVisible && ((Control)hud.FindChild("structure", true, false)).IsVisibleInTree(),
                "Native Tab and Enter reopen the selected structure without selecting an actor or issuing an order.");
            var closeSelection = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "structure/close");
            ((ScrollContainer)hud.FindChild("DetailScroll", true, false)).EnsureControlVisible(closeSelection);
            await Settle();
            await ClickControl(closeSelection);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && hud.Projection!.Sections.All(section => section.Key != "structure"),
                "The actual structure Close selection button clears its pinned subject after pointer leave.");
            Unchanged("structure close selection");
            if (hud.DossierVisible)
                await ClickControl((Button)hud.FindChild("DossierClose", true, false));
            await ClickMap(westPixel.Value);
            Motion(Dossier().GetGlobalRect().GetCenter());
            await Settle();
            viewport.GuiReleaseFocus();
            KeyPress(Key.Escape);
            await Settle();
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && hud.Projection!.Sections.All(section => section.Key != "structure"),
                "Native Escape clears standalone structure selection after HUD handoff.");
            Unchanged("Escape cancellation");

            await ClickMap(westPixel.Value);
            WestPinned("reopening after Escape");
            await ClickMap(actorPixel!.Value);
            Motion(Dossier().GetGlobalRect().GetCenter());
            await Settle();
            Require(screen.Selection?.Kind == TacticalSelectionKind.Unit && screen.Selection.UnitId == actor!.Id
                && hud.Projection!.Sections.Any(section => section.Key == "unit")
                && hud.Projection.Sections.All(section => section.Key != "structure"),
                "An explicit native friendly-unit click replaces structure inspection without retaining stale structure details.");
            Unchanged("switching from Structure to Unit");
            GD.Print("STRUCTURE_INSPECTION_NATIVE_OK C01-09 chain-west / chain-north; state=6fefe68e; actual mouse handoff, Tab, Enter, Close and Escape; no committed actions");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
            GetWindow().Size = windowSize;
            await Settle();
        }
    }

    private async System.Threading.Tasks.Task CheckShortHudBoundary(BattleScreen screen, GrayFlagSession session, Control map, TacticalHud hud)
    {
        var windowSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1180, 812);
        var viewport = GetViewport();
        viewport.NotifyMouseEntered();
        void Motion(Vector2 at) => viewport.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        void Button(Vector2 at, bool down) => viewport.PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = down,
            Position = at,
            GlobalPosition = at
        }, true);
        async System.Threading.Tasks.Task PinSite()
        {
            screen.SelectHit(TacticalHit.Object(new(38, 23)));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        (Vector2 Map, Vector2 Hud) Crossing()
        {
            var bounds = map.GetGlobalRect();
            var boardTransform = screen.Board.GetGlobalTransformWithCanvas().AffineInverse();
            foreach (var panel in hud.GetChildren().OfType<Control>().Where(value => value.IsVisibleInTree()
                && value.MouseFilter == Control.MouseFilterEnum.Stop))
            {
                var rect = panel.GetGlobalRect();
                foreach (var vertical in new[] { false, true })
                    foreach (var far in new[] { false, true })
                    {
                        var length = vertical ? rect.Size.Y : rect.Size.X;
                        for (var offset = 8f; offset < length - 8; offset += 8)
                        {
                            var edge = vertical
                                ? new Vector2(far ? rect.End.X : rect.Position.X, rect.Position.Y + offset)
                                : new Vector2(rect.Position.X + offset, far ? rect.End.Y : rect.Position.Y);
                            var inward = (vertical ? Vector2.Right : Vector2.Down) * (far ? -1 : 1);
                            var from = edge - inward * 2;
                            var to = edge + inward * 2;
                            if (!bounds.HasPoint(from) || !bounds.HasPoint(to)
                                || screen.Board.Pick(boardTransform * from) is not { Kind: TacticalHitKind.Ground } start
                                || screen.Board.Pick(boardTransform * to) is not { Kind: TacticalHitKind.Ground } end
                                || !session.GetTacticalQueries(0).InteractiveCells.Contains(start.Cell)
                                || !session.GetTacticalQueries(0).InteractiveCells.Contains(end.Cell))
                                continue;
                            Motion(to);
                            var hovered = viewport.GuiGetHoveredControl();
                            if (hovered is null || !hud.IsAncestorOf(hovered))
                                continue;
                            Motion(from);
                            if (viewport.GuiGetHoveredControl() == map)
                                return (from, to);
                        }
                    }
            }
            throw new InvalidOperationException("No real painted HUD edge overlaps interactive Redstone ground for the short-crossing fixture.");
        }

        var before = EcmaJson.Stringify(session.Snapshot());
        await PinSite();
        var crossing = Crossing();
        Button(crossing.Map, true);
        Button(crossing.Map, false);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle,
            "A normal native map click still closes the pinned site through actual battlefield input.");

        await PinSite();
        crossing = Crossing();
        var world = map.GetNode<Node2D>("World");
        var cameraBefore = world.Position;
        Button(crossing.Map, true);
        Motion(crossing.Hud + (crossing.Hud - crossing.Map).Normalized() * 20);
        Motion(crossing.Hud);
        Button(crossing.Hud, false);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Site && world.Position != cameraBefore,
            "A real drag can pan across an overlapping HUD surface and release without activating the hidden battlefield.");

        await PinSite();
        crossing = Crossing();
        var selected = screen.Selection;
        Button(crossing.Map, true);
        Motion(crossing.Hud);
        Require(viewport.GuiGetHoveredControl() is { } capturedOwner && hud.IsAncestorOf(capturedOwner),
            "Native hover ownership reaches the HUD even while the board captures pointer motion.");
        Button(crossing.Hud, false);
        Require(screen.Selection == selected && session.Revision == 0 && session.Events.Count == 0
            && EcmaJson.Stringify(session.Snapshot()) == before,
            $"A four-pixel native map-to-painted-HUD release must preserve the pinned site, not click the ground behind the HUD; got {screen.Selection?.Kind}.");
        Motion(crossing.Map);
        Button(crossing.Map, true);
        Button(crossing.Hud, false);
        Require(screen.Selection == selected && session.Revision == 0,
            "A release delivered without an intervening motion also respects current native HUD ownership.");
        Motion(crossing.Map);
        Button(crossing.Map, true);
        Button(crossing.Map, false);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle,
            "A canceled HUD-boundary click releases capture so the next ordinary map click still works.");
        GetWindow().Size = windowSize;
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

    private async System.Threading.Tasks.Task CheckPrimaryOrders()
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
        Require(request is null, "Native primary activation waits until input dispatch completes.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        var actorHit = TacticalHit.Unit(new(actor["x"]!.GetValue<int>(), actor["y"]!.GetValue<int>()), selected);
        Require(hud.DossierSubject == actorHit && session.ProjectHud(new(0, 0), selection, null, selected, false).DossierSubject == actorHit,
            "Dossier identity remains the visible inspected actor when unrelated hover enters or leaves.");
        Require(session.ProjectHud(new(0, 0), new(), null, int.MaxValue, false).DossierSubject is null,
            "An unavailable inspection identifier cannot create a dossier subject without visible facts.");
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
            new TacticalHudSection("objectives", "作战目标", new[]
            {
                new TacticalHudRow("long", objective),
                new("locate", "定位任务区域", Intent: new(new(1, 1), TacticalHudIntentKind.FocusObjective, "locate"),
                    TargetCells: new[] { new BoardCell(18, 23) })
            }),
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
            var objectiveGroup = (VBoxContainer)hud.FindChild("objectives", true, false);
            var objectivePlate = (Control)hud.FindChild("objectivesPlate", true, false);
            var objectiveToggle = objectiveGroup.GetChildren().OfType<Button>().Single();
            var expandedBottom = objectivePlate.GetGlobalRect().End.Y;
            var objectiveLocator = objectiveGroup.GetNode<VBoxContainer>("Content").GetChildren().OfType<Button>().Single();
            foreach (var button in new[] { objectiveToggle, objectiveLocator })
                foreach (var colorName in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
                {
                    var expected = new Color(colorName == "font_disabled_color" ? "#67583f" : "#453921");
                    Require(button.GetThemeColor(colorName) == expected,
                        $"Objective paper button '{button.Text}' must retain readable dark ink for {colorName} at width {width}.");
                }
            foreach (var button in new[] { objectiveToggle, objectiveLocator })
                foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
                {
                    var expected = new Color(state == "hover" ? "#a58c4422"
                        : state is "pressed" or "hover_pressed" ? "#a58c4444" : "#00000000");
                    Require(button.GetThemeStylebox(state) is StyleBoxFlat background && background.BgColor == expected,
                        $"Objective paper button '{button.Text}' must preserve the light paper through its {state} background at width {width}.");
                }
            objectiveToggle.GrabFocus();
            objectiveToggle.ButtonPressed = false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(objectivePlate.Size.Y < 60 && objectivePlate.GetGlobalRect().End.Y < expandedBottom - 30,
                "Collapsed objectives must release their paper and pointer-blocking rectangle, not just hide text.");
            Require(!objectiveGroup.GetNode<VBoxContainer>("Content").Visible && objectiveToggle.HasFocus(),
                "Objective collapse lost the accessible disclosure focus or left content visible.");
            objectiveToggle.ButtonPressed = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(objectiveGroup.GetNode<VBoxContainer>("Content").Visible && objectivePlate.GetGlobalRect().End.Y >= expandedBottom
                && objectiveToggle.HasFocus(), "Reopening objectives failed to restore full text and focus.");
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
            Require(hint.GetVisibleLineCount() == hint.GetLineCount() && hint.MaxLinesVisible == -1
                && hint.Text.Contains("方向键", StringComparison.Ordinal) && hint.Text.Contains("Enter", StringComparison.Ordinal),
                "Selected-unit guidance keeps the precise keyboard path fully visible at both native window sizes.");
            Require(hint.TooltipText == hint.Text, "The instruction remains accessible through its complete tooltip.");
            foreach (var hintKey in new[] { "unit", "destination", "idle", "idle_recruit", "deploying", "disembark", "tactic" })
            {
                var fullInstruction = GodotTextCatalog.LoadSimplifiedChinese().Format("ui.hud.selection." + hintKey);
                hud.Apply(new(new(1, 1), fullInstruction, new[]
                {
                    new TacticalHudSection("tactics", "指挥战术", Enumerable.Range(0, 3).Select(i =>
                        new TacticalHudRow("tactic-" + i, "战术 " + i, Enabled: i != 2,
                            Intent: new(new(1, 1), TacticalHudIntentKind.Back))).ToArray())
                }));
                for (var frame = 0; frame < 4; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                hint = (Label)hud.FindChild("SelectionHint", true, false);
                tray = (Control)hud.FindChild("OrderTray", true, false);
                var actions = (Control)hud.FindChild("tactics", false, false);
                Require(hint.GetVisibleLineCount() == hint.GetLineCount() && hint.MaxLinesVisible == -1,
                    $"The complete {hintKey} instruction is visible without pointer-only tooltip access at {width}px: lines={hint.GetLineCount()}, visible={hint.GetVisibleLineCount()}, size={hint.Size}, minimum={hint.GetMinimumSize()}, tray={tray.Size}.");
                Require(tray.GetGlobalRect().Encloses(hint.GetGlobalRect())
                    && hint.GetGlobalRect().Position.Y >= actions.GetGlobalRect().End.Y
                    && tray.GetGlobalRect().Position.Y >= host.Size.Y - 180,
                    "Wrapped guidance stays inside its material tray, below commands and within a bounded bottom area.");
                var buttons = actions.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
                Require(buttons.Length == 3 && buttons[2].Disabled && buttons.All(button => button.Size.Y >= 54),
                    "Full instructions do not shrink command targets or enable unavailable commands.");
            }
            hud.ShowHint = false;
        }
        RemoveChild(host);
        host.Free();
    }

    private async System.Threading.Tasks.Task CheckRecruitmentLayout()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var site = new BoardCell(0, 0);
        state["map"]!["tiles"]![0] = "castle";
        state["map"]!["owners"]![0] = state["currentPlayer"]!.DeepClone();
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        var before = EcmaJson.Stringify(session.Snapshot());
        var projection = session.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Recruit,
            Destination = site
        }, TacticalHit.Object(site), null, false);
        var cards = engine.Recruitment.Cards(state, new(site.X, site.Y));
        Require(cards.Count > 8, "The unchanged recruitment catalog exercises at least three native order rows.");
        var host = new Control { Theme = TacticalHudStyle.Create() };
        AddChild(host);
        var assets = AssetCatalog.Load();
        var art = TacticalHudArt.Load(assets, PortraitCatalogResources.Load());
        var hud = new TacticalHud();
        hud.ConfigureArt(art);
        host.AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var width in new[] { 960, 1180 })
        {
            host.Size = new Vector2(width, 640);
            hud.Apply(projection);
            for (var frame = 0; frame < 6; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var orders = (GridContainer)hud.FindChild("Orders", true, false);
            var tray = (Control)hud.FindChild("OrderTray", true, false);
            var hint = (Label)hud.FindChild("SelectionHint", true, false);
            var actions = (Control)hud.FindChild("recruit", false, false);
            var buttons = orders.GetChildren().OfType<Button>().ToArray();
            Require(buttons.Select(button => button.GetMeta("hud_key", "").AsString()).SequenceEqual(cards.Select(card => "recruit/" + card.Unit))
                && buttons.Select(button => !button.Disabled).SequenceEqual(cards.Select(card => card.Affordable)),
                "Recruitment preserves every canonical card, its order and its affordability.");
            Require(buttons.Zip(cards).All(pair => pair.First.Icon is AtlasTexture icon
                && icon.Atlas.ResourcePath == assets.ResolveUnit(pair.Second.Unit).ResourcePath
                && icon.Region == assets.ResolveUnit(pair.Second.Unit).RegionAt(AssetCatalog.FrameIndex(assets.ResolveUnit(pair.Second.Unit), "idle"))),
                "Every recruit card displays its own formally bound native idle frame, never a placeholder or another unit.");
            Require(tray.GetGlobalRect().Encloses(actions.GetGlobalRect()) && actions.GetGlobalRect().End.Y <= hint.GetGlobalRect().Position.Y,
                "The complete recruitment grid and account row stay inside the tray and above the readable instruction.");
            Require(host.GetGlobalRect().Encloses(tray.GetGlobalRect()) && buttons.All(button => tray.GetGlobalRect().Encloses(button.GetGlobalRect()) && button.Size.Y >= 30)
                && hint.GetVisibleLineCount() == hint.GetLineCount(),
                "Multi-row recruitment stays on screen without truncating guidance or shrinking its command targets.");
            var initialTray = tray.GetGlobalRect();
            var focused = buttons.First(button => !button.Disabled);
            focused.GrabFocus();
            for (var refresh = 0; refresh < 3; refresh++)
            {
                hud.ShowHint = refresh % 2 == 0;
                hud.Apply(projection with
                {
                    Hint = projection.Hint + new string(' ', refresh + 1)
                });
                for (var frame = 0; frame < 4; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(ReferenceEquals(orders, hud.FindChild("Orders", true, false)) && focused.HasFocus()
                    && tray.GetGlobalRect() == initialTray,
                    "Hint and projection refresh retain recruitment buttons, keyboard focus and fitted geometry.");
            }
            var originalMinimum = buttons[0].CustomMinimumSize;
            buttons[0].CustomMinimumSize = originalMinimum + new Vector2(0, 80);
            for (var frame = 0; frame < 4; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(tray.Size.Y > initialTray.Size.Y && tray.GetGlobalRect().Encloses(actions.GetGlobalRect())
                && actions.GetGlobalRect().End.Y <= ((Label)hud.FindChild("SelectionHint", true, false)).GlobalPosition.Y,
                "The current hint subscription refits retained actions after a native minimum-height change.");
            buttons[0].CustomMinimumSize = originalMinimum;
            for (var frame = 0; frame < 4; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(tray.GetGlobalRect() == initialTray, "Native minimum-height contraction releases unused tray space.");
            hud.ShowHint = true;
            hud.Apply(new(projection.Stamp, "选择指令", new[]
            {
                new TacticalHudSection("commands", "指令", new[] { new TacticalHudRow("wait", "待机", Intent: new(projection.Stamp, TacticalHudIntentKind.Command, "wait")) })
            }));
            for (var frame = 0; frame < 4; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var compactTray = (Control)hud.FindChild("OrderTray", false, false);
            var compactActions = (Control)hud.FindChild("commands", false, false);
            Require(compactTray.Size.Y < initialTray.Size.Y && compactTray.GetGlobalRect().Encloses(compactActions.GetGlobalRect())
                && compactActions.GetGlobalRect().End.Y <= ((Label)hud.FindChild("SelectionHint", true, false)).GlobalPosition.Y,
                "Leaving a multi-row menu restores a compact single-order tray without stale extra height.");
        }
        Require(before == EcmaJson.Stringify(session.Snapshot()) && session.Revision == 0 && session.Events.Count == 0,
            "Recruitment layout does not change the battle or execute a purchase.");
        RemoveChild(host);
        host.Free();
    }

    private async System.Threading.Tasks.Task CheckRecruitedUnitArt()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var site = new BoardCell(0, 0);
        state["map"]!["tiles"]![0] = "castle";
        state["map"]!["owners"]![0] = state["currentPlayer"]!.DeepClone();
        var player = state["players"]!.AsArray().OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<int>() == state["currentPlayer"]!.GetValue<int>());
        player["resources"]!["funds"]!["current"] = 1000;
        var assets = AssetCatalog.Load();
        var hud = new TacticalHud { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
        hud.ConfigureArt(TacticalHudArt.Load(assets, PortraitCatalogResources.Load()));
        AddChild(hud);
        foreach (var card in engine.Recruitment.Cards(state, new(site.X, site.Y)))
        {
            var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
            var control = new TacticalSelectionController(session.GetTacticalQueries(0));
            control.Click(control.Stamp, TacticalHit.Object(site));
            var request = control.Recruit(control.Stamp, card.Unit).Request;
            Require(request?.Action is not null && session.Dispatch(request.Action.Value, request.Stamp.Revision).Accepted
                && session.Revision == 1, $"The real recruitment path commits {card.Unit} in the isolated funded fixture.");
            var snapshot = session.Snapshot();
            var expected = EcmaJson.Stringify(snapshot);
            var unit = snapshot["units"]!.AsArray().OfType<JsonObject>()
                .Single(value => value["type"]!.GetValue<string>() == card.Unit && value["x"]!.GetValue<int>() == site.X && value["y"]!.GetValue<int>() == site.Y);
            var id = unit["id"]!.GetValue<int>();
            var asset = assets.ResolveUnit(card.Unit);
            foreach (var currentSession in new[] { session, GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), session.Save()) })
            {
                var inspection = new TacticalSelectionController(currentSession.GetTacticalQueries(0));
                inspection.Click(inspection.Stamp, TacticalHit.Unit(site, id));
                hud.Apply(currentSession.ProjectHud(inspection.Stamp, inspection.Selection, inspection.CursorHit, inspection.InspectId, false));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var portrait = (TextureRect)hud.FindChild("InspectedPortrait", true, false);
                Require(hud.Projection!.Portrait?.UnitType == card.Unit && portrait.Texture is AtlasTexture frame
                    && frame.Atlas.ResourcePath == asset.ResourcePath && frame.Region == asset.RegionAt(AssetCatalog.FrameIndex(asset, "idle")),
                    $"Fresh and restored recruits show their own bound portrait: {card.Unit}.");
                Require(EcmaJson.Stringify(currentSession.Snapshot()) == expected,
                    "Inspection and portrait rendering cannot change a fresh or restored recruited unit.");
            }
            foreach (var role in asset.FrameOrder)
            {
                var sprite = assets.CreateSprite(asset, role);
                Require(sprite.Texture is AtlasTexture frame && frame.Region == asset.RegionAt(AssetCatalog.FrameIndex(asset, role))
                    && Mathf.IsEqualApprox(sprite.Scale.X, sprite.Scale.Y) && SpriteGroundContact.Offset(sprite).IsZeroApprox(),
                    $"Recruited unit animation keeps its exact frame and registered ground contact: {card.Unit}/{role}.");
                sprite.Free();
            }
        }
        RemoveChild(hud);
        hud.Free();
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
        var notices = new[] {
            new TacticalHudRow("forecast-scope", "HP 仅计主击/反击；移动、援护与附效另计"),
            new TacticalHudRow("hit-effect-attack", "另 15 项，详解 · 主击附效：" + string.Concat(Enumerable.Repeat("很长的状态名称", 12)), Hint: "完整附效不会因摘要长度而丢失"),
            new TacticalHudRow("hit-effect-counter", "反击附效：尝试击退 1 格 · 受阻伤害 10"),
            new TacticalHudRow("hit-effect-support", "另 8 项，详解 · 援护附效：尝试施加迟缓 2 回合") };
        var effectDetails = Enumerable.Range(0, 24).Select(index => new TacticalHudRow("hit-effect-detail:attack:" + index,
            $"北丘边境弓手 → 军团皇家盾卫：主击附效 {index}，命中且受击单位仍在场时尝试生效")).ToArray();
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
                    new TacticalHudRow("strike", "主击 36 · HP 80 → 44", Gauge: new(44, 80)) }.Concat(warningRows).Concat(notices).ToArray()),
                new TacticalHudSection("forecast-details", "计算详情", new[] { new TacticalHudRow("damage-type", "穿刺 → 重甲"), new TacticalHudRow("mitigation", "减伤 20%") }.Concat(effectDetails).ToArray()),
                new TacticalHudSection("target", "选择目标", new[] { new TacticalHudRow("cancel", "取消 Esc", Intent: new(stamp, TacticalHudIntentKind.Back)) })
            }));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var plate = (Control)hud.FindChild("forecastPlate", true, false);
            var effects = (GridContainer)hud.FindChild("ForecastEffects", true, false);
            Require(effects.GetChildCount() == warningRows.Length && effects.GetChildren().OfType<Label>().All(label =>
                label.IsVisibleInTree() && plate.GetGlobalRect().Encloses(label.GetGlobalRect())),
                "Interception, reaction, support, collateral and death facts are actually visible inside the forecast plate, not tooltip-only.");
            foreach (var notice in notices)
            {
                var label = (Label)hud.FindChild(notice.Key, true, false);
                Require(label.IsVisibleInTree() && plate.GetGlobalRect().Encloses(label.GetGlobalRect()) && label.GetVisibleLineCount() == 1,
                    "Scope and each phase's effect summary stay visible and bounded even with repeated effects and long names.");
            }
            var counterNotice = (Label)hud.FindChild("hit-effect-counter", true, false);
            Require(counterNotice.GetThemeFont("font").GetStringSize(counterNotice.Text, fontSize: counterNotice.GetThemeFontSize("font_size")).X <= counterNotice.Size.X,
                "The real counter's conditional 10 collision damage fits without ellipsis before confirmation.");
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
            Require(calculation.GetNode<VBoxContainer>("Content").GetChildCount() == effectDetails.Length + 2
                && calculation.Size.Y > popup.Size.Y,
                "All ordered effect details remain in the scrollable dossier even when the compact plate is bounded.");
            ((Button)hud.FindChild("DossierClose", true, false)).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!popup.Visible && ReferenceEquals(GetViewport().GuiGetFocusOwner(), detail),
                "Closing effect details returns focus to the unchanged forecast entry.");
        }
        foreach (var width in new[] { 960, 1180 })
        {
            host.Size = new Vector2(width, 640);
            var currentStamp = stamp with
            {
                Revision = width + 1
            };
            hud.Apply(new(currentStamp, "再点目标 / Enter 执行 · Esc 返回", new[]
            {
                new TacticalHudSection("forecast", "战斗预测", new[]
                {
                    new TacticalHudRow("exchange", "工兵 → 指挥节点"),
                    new("strike", "主击 57 · HP 57 → 0", Gauge: new(0, 100)),
                    new("collateral", "范围攻击还将波及 2 个单位、0 个结构"),
                    new("structure-destroyed", "目标建筑或障碍将被摧毁。"),
                    notices[1]
                }),
                new TacticalHudSection("forecast-details", "计算详情", effectDetails),
                new TacticalHudSection("target", "选择目标", new[]
                {
                    new TacticalHudRow("cancel", "取消 Esc", Intent: new(currentStamp, TacticalHudIntentKind.Back))
                })
            }));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var plate = (Control)hud.FindChild("forecastPlate", true, false);
            var notice = hud.FindChildren("*", "Label", true, false).OfType<Label>().Single(label => label.Text == notices[1].Label);
            var cancel = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "target/cancel");
            var detail = (Button)hud.FindChild("ForecastDetailsButton", true, false);
            GD.Print($"SYNTHETIC_STRUCTURE_FORECAST width={width}: effect lines={notice.GetVisibleLineCount()}, plate={plate.GetGlobalRect()}");
            Require(notice.IsVisibleInTree() && notice.GetVisibleLineCount() == 1
                && plate.GetGlobalRect().Encloses(notice.GetGlobalRect())
                && plate.GlobalPosition.Y >= 320 && plate.GetGlobalRect().End.Y <= host.Size.Y
                && cancel.IsVisibleInTree() && !cancel.Disabled && plate.GetGlobalRect().Encloses(cancel.GetGlobalRect())
                && detail.IsVisibleInTree() && plate.GetGlobalRect().Encloses(detail.GetGlobalRect()),
                "Synthetic long structure-effect summaries stay bounded, with current Cancel and details controls reachable at both widths.");
            Require(notice.TooltipText == notices[1].Hint,
                "A bounded structure effect summary retains its complete conditional tooltip.");
            detail.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            var calculation = (VBoxContainer)hud.FindChild("forecast-details", true, false);
            Require(popup.Visible && calculation.GetNode<VBoxContainer>("Content").Visible
                && calculation.GetNode<VBoxContainer>("Content").GetChildCount() == effectDetails.Length
                && calculation.Size.Y > popup.Size.Y,
                "All structure collateral effect details remain available in the existing scrollable dossier.");
            ((Button)hud.FindChild("DossierClose", true, false)).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!popup.Visible && ReferenceEquals(GetViewport().GuiGetFocusOwner(), detail),
                "Structure forecast details restore their current opener's keyboard focus.");
            var canceled = 0;
            void Cancel(TacticalHudIntent intent)
            {
                if (intent.Kind == TacticalHudIntentKind.Back && intent.Stamp == currentStamp)
                    canceled++;
            }
            hud.IntentRequested += Cancel;
            cancel.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            hud.IntentRequested -= Cancel;
            Require(canceled == 1, "The visible structure forecast Cancel control submits its current stamped intent once.");
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
        Require(fired == 0, "Native release does not publish while the viewport is dispatching input.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(fired == 1, "Native pointer press/release across hover refresh submits the first click exactly once.");
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        TacticalHudProjection Frame(string terrain, int unit = 1) => new(stamp, "查看单位", new[]
        {
            new TacticalHudSection("unit", "米蕾尔", new[] { new TacticalHudRow("hp", "生命 78 / 78") }),
            new TacticalHudSection("unit-details", "兵种与能力", new[] { new TacticalHudRow("ability", "治疗") }),
            new TacticalHudSection("tile", "地形", new[] { new TacticalHudRow("terrain", terrain) })
        }, DossierSubject: TacticalHit.Unit(new(4, 17), unit));
        hud.Apply(Frame("道路"));
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        var scroll = hud.FindChild("DetailScroll", true, false);
        var staleEntry = (Button)hud.FindChild("DossierButton", true, false);
        staleEntry.EmitSignal(Button.SignalName.Pressed);
        hud.Apply(Frame("道路", unit: 2));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!popup.Visible, "Deferred dossier opening cannot switch to a visually identical different subject at the same battle stamp.");
        var retainedEntry = (Button)hud.FindChild("DossierButton", true, false);
        retainedEntry.EmitSignal(Button.SignalName.Pressed);
        hud.Apply(Frame("无关悬停地形", unit: 2));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(popup.Visible && ReferenceEquals(retainedEntry, hud.FindChild("DossierButton", true, false)),
            "Deferred dossier reading survives unrelated terrain changes for the same subject.");
        popup.Hide();
        retainedEntry.EmitSignal(Button.SignalName.Pressed);
        hud.Apply(Frame("无关悬停地形", unit: 2) with
        {
            Stamp = new(80, 2)
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!popup.Visible, "A pending dossier cannot cross a canonical revision even for the same subject.");
        foreach (var subjects in new[]
        {
            (TacticalHit.Object(new BoardCell(1, 1)), TacticalHit.Object(new BoardCell(2, 1))),
            (TacticalHit.Unit(new BoardCell(4, 17), 2), TacticalHit.Object(new BoardCell(4, 17)))
        })
        {
            hud.Apply(Frame("同样的资料") with
            {
                DossierSubject = subjects.Item1
            });
            ((Button)hud.FindChild("DossierButton", true, false)).EmitSignal(Button.SignalName.Pressed);
            hud.Apply(Frame("同样的资料") with
            {
                DossierSubject = subjects.Item2
            });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!popup.Visible, "Different site cells or subject kinds invalidate a pending read even when every displayed row matches.");
        }
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
            hud.Apply(Frame("Terrain changes while the dossier pointer is held " + index));
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

    private async System.Threading.Tasks.Task CheckRetainedSectionLifetime()
    {
        var hud = new TacticalHud { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
        AddChild(hud);
        try
        {
            var stamp = new TacticalStamp(29, 7);
            TacticalHudProjection Frame(int tile, bool formation = false, bool enabled = true)
            {
                var sections = new List<TacticalHudSection> { new("unit-details", "Abilities", new[] { new TacticalHudRow("ability", "Guard") }) };
                if (formation)
                    sections.Add(new("formations", "Formation", new[] { new TacticalHudRow("line", "Line") }));
                sections.Add(new("stances", "Stance", new[] { new TacticalHudRow("guard", "Guard", Enabled: enabled, Intent: new(stamp, TacticalHudIntentKind.Command, "guard")) }));
                sections.Add(new("tile", "Terrain", new[] { new TacticalHudRow("at", "Tile " + tile) }));
                return new(stamp, "Inspect", sections);
            }
            VBoxContainer Group(string key) => (VBoxContainer)hud.FindChild(key, true, false);
            Button Toggle(string key) => Group(key).GetChildren().OfType<Button>().Single();
            Button Guard() => Group("stances").FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "stances/guard");
            var fired = 0;
            hud.IntentRequested += _ => fired++;
            hud.Apply(Frame(1));
            var abilities = Group("unit-details");
            var disclosure = Toggle("unit-details");
            var stance = Group("stances");
            var action = Guard();
            hud.Apply(Frame(2, formation: true));
            Require(ReferenceEquals(abilities, Group("unit-details")) && ReferenceEquals(stance, Group("stances")) && ReferenceEquals(action, Guard()),
                "Inserting a detail section keeps unchanged native subtrees mounted.");
            Require(hud.FindChild("Details", true, false).GetChildren().Select(child => child.Name.ToString())
                .SequenceEqual(new[] { "unit-details", "formations", "stances", "tile" }),
                "Retained and inserted dossier sections follow the current projection order.");
            disclosure.ButtonPressed = true;
            Require(abilities.GetNode<Control>("Content").Visible, "Retained disclosure remains live after a different section changes.");
            hud.Apply(Frame(3, formation: true, enabled: false));
            action.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 0 && Guard().Disabled && !ReferenceEquals(action, Guard()),
                "Changed hidden action eligibility replaces its controls and rejects detached stale input.");
            hud.Apply(Frame(4, formation: true) with
            {
                Busy = true
            });
            Require(Guard().Disabled, "Busy state invalidates retained detail actions as well as the primary strip.");
            hud.Apply(Frame(5));
            Require(hud.FindChild("formations", true, false) is null, "Removed sections do not linger in the persistent dossier body.");
            abilities = Group("unit-details");
            disclosure = Toggle("unit-details");
            Require(disclosure.ButtonPressed && abilities.GetNode<Control>("Content").Visible,
                "Changed facts preserve the user's disclosure state.");
            RemoveChild(hud);
            AddChild(hud);
            disclosure.ButtonPressed = false;
            Require(hud.Projection is null && abilities.GetNode<Control>("Content").Visible,
                "Remount without a fresh Apply cannot reactivate an old disclosure callback.");
            hud.Apply(Frame(6));
            Require(!ReferenceEquals(abilities, Group("unit-details")) && Toggle("unit-details").ButtonPressed,
                "A fresh projection rebuilds invalidated nodes without accepting stale disclosure changes.");
            disclosure.EmitSignal(BaseButton.SignalName.Toggled, false);
            hud.Apply(Frame(7));
            Require(Toggle("unit-details").ButtonPressed, "Detached disclosures cannot alter the live expansion state.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            RemoveChild(hud);
            hud.Free();
        }
    }

    private async System.Threading.Tasks.Task CheckTileTextRefresh()
    {
        var hud = new TacticalHud { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
        var assets = AssetCatalog.Load();
        var portraits = PortraitCatalogResources.Load();
        hud.ConfigureArt(TacticalHudArt.Load(assets, portraits));
        AddChild(hud);
        try
        {
            var stamp = new TacticalStamp(92, 3);
            TacticalHudProjection Frame(string title, params TacticalHudRow[] rows) => new(stamp, "Inspect", new[]
            {
                new TacticalHudSection("unit-details", "Abilities", new[] { new TacticalHudRow("ability", "Guard") }),
                new TacticalHudSection("tile", title, rows)
            });
            VBoxContainer Group() => (VBoxContainer)hud.FindChild("tile", true, false);
            Button Toggle() => Group().GetChildren().OfType<Button>().Single();
            Label[] Labels() => Group().GetNode<VBoxContainer>("Content").GetChildren().OfType<HBoxContainer>()
                .Select(line => line.GetChildren().OfType<Label>().Single()).ToArray();
            async System.Threading.Tasks.Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var coordinate = new TacticalHudRow("coordinate", "1, 2", "First cell");
            var defense = new TacticalHudRow("defense", "Defense 10");
            hud.Apply(Frame("Road", coordinate, defense));
            var group = Group();
            var labels = Labels();
            var toggle = Toggle();
            ((Button)hud.FindChild("DossierButton", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Layout();
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            toggle.ButtonPressed = true;
            toggle.GrabFocus();
            await Layout();
            coordinate = coordinate with
            {
                Label = "3, 4",
                Hint = "Current cell"
            };
            defense = defense with
            {
                Label = "Defense 20",
                Hint = "Current cover"
            };
            hud.Apply(Frame("Forest", coordinate, defense));
            await Layout();
            Require(ReferenceEquals(group, Group()) && labels.SequenceEqual(Labels()) && toggle.HasFocus(),
                "Terrain text refresh keeps native row shaping, disclosure and keyboard focus alive.");
            Require(Labels().Select(label => (label.Text, label.TooltipText)).SequenceEqual(new[]
                { (coordinate.Label, coordinate.Hint), (defense.Label, defense.Hint) })
                && group.GetNode<HBoxContainer>("Heading").GetChildren().OfType<Label>().Single().Text == "Forest",
                "Retained terrain labels, tooltips and hidden heading use the current projection.");
            foreach (var open in new[] { false, true })
            {
                toggle.ButtonPressed = open;
                Require(toggle.Text == (open ? "−  " : "+  ") + "Forest" && group.GetNode<Control>("Content").Visible == open,
                    "A retained disclosure cannot restore the terrain title captured at creation.");
            }
            var longDefense = defense with
            {
                Label = string.Join(" ", Enumerable.Repeat("Current terrain movement cost", 30))
            };
            hud.Apply(Frame("Forest", coordinate, longDefense));
            await Layout();
            var lineCount = labels[1].GetLineCount();
            var textHeight = Enumerable.Range(0, lineCount).Sum(labels[1].GetLineHeight)
                + Math.Max(0, lineCount - 1) * labels[1].GetThemeConstant("line_spacing");
            Require(ReferenceEquals(group, Group()) && lineCount > 1 && labels[1].Size.Y >= textHeight,
                "Long replacement terrain text reflows through native container sizing without replacing its row.");
            var scroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
            toggle.ReleaseFocus();
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            await Layout();
            var manualScroll = scroll.ScrollVertical;
            hud.Apply(Frame("Forest", coordinate with
            {
                Label = "4, 4"
            }, longDefense));
            await Layout();
            Require(manualScroll > 0 && scroll.ScrollVertical == manualScroll,
                "Updating a coordinate preserves the player's deliberate dossier reading offset.");
            popup.Hide();
            await Layout();
            var variants = new[]
            {
                new[] { coordinate, defense, new TacticalHudRow("heal", "Recover 15") },
                new[] { coordinate, defense with { Key = "cover" } },
                new[] { defense, coordinate },
                new[] { coordinate, defense with { Selected = true } },
                new[] { coordinate, defense with { Enabled = false } },
                new[] { coordinate, defense with { Icon = "weapon:c01.border-blade" } },
                new[] { coordinate, defense with { UnitIcon = new("c01.mirelle", null, "#224466") } },
                new[] { coordinate, defense with { Gauge = new(50, 100) } },
                new[] { coordinate, defense with { Gauge = new(10, 100) } },
                new[] { coordinate, defense with { Intent = new(stamp, TacticalHudIntentKind.Command, "wait") } }
            };
            var fired = 0;
            hud.IntentRequested += _ => fired++;
            foreach (var rows in variants)
            {
                var baseline = rows.Last().Gauge?.Value == 10 ? defense with
                {
                    Gauge = new(50, 100)
                } : defense;
                hud.Apply(Frame("Baseline shape", coordinate, baseline));
                var prior = Group();
                hud.Apply(Frame("Changed shape", rows));
                Require(!ReferenceEquals(prior, Group()), "Changed terrain row order, key, style, media, gauge or intent rebuilds its native shape.");
                var row = rows.Last();
                if (row.Intent is not null)
                {
                    var action = Group().FindChildren("*", "Button", true, false).OfType<Button>()
                        .Single(button => button.GetMeta("hud_key", "").AsString() == "tile/defense");
                    action.EmitSignal(Button.SignalName.Pressed);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Require(fired == 1, "A newly interactive terrain row uses the canonical guarded intent path.");
                }
                else
                {
                    Require(Labels().Select(label => label.Text).SequenceEqual(rows.Select(value => value.Label)),
                        "Shape changes show every current row in projection order without obsolete labels.");
                    if (row.Selected)
                        Require(Labels().Last().GetThemeColor("font_color") == TacticalHudStyle.Gold, "Changed selection restyles the native row.");
                    if (row.Icon is not null || row.UnitIcon is not null)
                        Require(Group().FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Single().Texture is not null,
                            "New terrain media resolves through the configured art owner.");
                    if (row.Gauge is { } gauge)
                    {
                        var bar = Group().FindChildren("*", "ProgressBar", true, false).OfType<ProgressBar>().Single();
                        Require(bar.Value == gauge.Value && bar.MaxValue == gauge.Maximum, "Changed gauge values cannot be stranded by text reuse.");
                    }
                }
            }
            hud.Apply(Frame("Road", coordinate, defense));
            var removedToggle = Toggle();
            hud.Apply(new(stamp, "Inspect", new[] { new TacticalHudSection("unit-details", "Abilities", Array.Empty<TacticalHudRow>()) }));
            Require(hud.FindChild("tile", true, false) is null, "An absent terrain projection removes the old section.");
            removedToggle.EmitSignal(Button.SignalName.Toggled, false);
            hud.Apply(Frame("Reappeared", coordinate, defense));
            Require(Toggle().ButtonPressed, "A detached terrain callback cannot change the live disclosure state.");
            foreach (var frame in new[]
            {
                Frame("Revision", coordinate, defense) with { Stamp = new(92, 4) },
                Frame("Binding", coordinate, defense) with { Stamp = new(93, 3) },
                Frame("Busy", coordinate, defense) with { Busy = true }
            })
            {
                hud.Apply(Frame("Baseline context", coordinate, defense));
                var prior = Group();
                hud.Apply(frame);
                Require(!ReferenceEquals(prior, Group()) && Labels()[0].Text == coordinate.Label,
                    "Revision, binding and busy changes retain their existing full invalidation boundary.");
            }
            var mediaFrame = Frame("Media", defense with
            {
                Icon = "weapon:c01.border-blade"
            });
            hud.Apply(mediaFrame);
            var oldGroup = Group();
            var oldTexture = Group().FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Single();
            hud.ConfigureArt(TacticalHudArt.Load(assets, portraits));
            hud.Apply(mediaFrame);
            Require(!ReferenceEquals(oldGroup, Group()) && oldTexture.Texture is null
                && Group().FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Single().Texture is not null,
                "Art replacement rebuilds the terrain section after releasing old texture references.");
            var titledEntry = (Button)hud.FindChild("DossierButton", true, false);
            var titledFrame = Frame("Renamed terrain", coordinate, defense) with
            {
                Portrait = new("c01.mirelle", null, "#224466"),
                Sections = new[] { new TacticalHudSection("unit-details", "Current abilities", Array.Empty<TacticalHudRow>()),
                    new TacticalHudSection("tile", "Renamed terrain", new[] { coordinate, defense }) }
            };
            hud.Apply(titledFrame);
            Require(ReferenceEquals(titledEntry, hud.FindChild("DossierButton", true, false))
                && titledEntry.Text == "Current abilities" && titledEntry.OffsetRight == 264,
                "A retained dossier opener updates its current title and portrait-dependent width.");
            var staleToggle = Toggle();
            var staleContent = Group().GetNode<Control>("Content");
            var staleEntry = (Button)hud.FindChild("DossierButton", true, false);
            RemoveChild(hud);
            AddChild(hud);
            staleToggle.ButtonPressed = false;
            staleEntry.EmitSignal(Button.SignalName.Pressed);
            await Layout();
            Require(hud.Projection is null && staleContent.Visible && !popup.Visible,
                "Remount without a fresh projection cannot reactivate retained terrain or dossier callbacks.");
            hud.Apply(Frame("Remounted", coordinate, defense));
            Require(!ReferenceEquals(staleToggle, Toggle()) && Toggle().ButtonPressed,
                "Fresh Apply rebuilds remounted terrain controls without accepting stale expansion changes.");
            var entry = (Button)hud.FindChild("DossierButton", true, false);
            var onlyTile = new TacticalHudProjection(stamp, "Inspect", new[] { new TacticalHudSection("tile", "Ground", new[] { coordinate }) });
            hud.Apply(onlyTile);
            entry.EmitSignal(Button.SignalName.Pressed);
            await Layout();
            Require(hud.FindChild("DossierButton", true, false) is null && !popup.Visible,
                "Removing inspectable details removes its opener and rejects its stale activation.");
        }
        finally
        {
            RemoveChild(hud);
            hud.Free();
        }
    }

    private async System.Threading.Tasks.Task CheckDeferredOrderLifetime()
    {
        var hud = new TacticalHud();
        AddChild(hud);
        var stamp = new TacticalStamp(81, 2);
        TacticalHudProjection Frame(string label = "Wait") => new(stamp, "", new[]
        {
            new TacticalHudSection("commands", "Orders", new[]
            {
                new TacticalHudRow("wait", label, Intent: new(stamp, TacticalHudIntentKind.Command, "wait")),
                new TacticalHudRow("back", "Back", Intent: new(stamp, TacticalHudIntentKind.Back))
            })
        });
        Button Order(string key = "wait") => hud.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.GetMeta("hud_key", "").AsString() == "commands/" + key);
        var fired = 0;
        hud.IntentRequested += _ => fired++;
        try
        {
            hud.Apply(Frame());
            var retained = Order();
            retained.EmitSignal(Button.SignalName.Pressed);
            Order("back").EmitSignal(Button.SignalName.Pressed);
            hud.Apply(Frame() with
            {
                Hint = "Updated instruction"
            });
            Require(fired == 0 && ReferenceEquals(retained, Order()), "A pending order survives an unrelated retained-row refresh without synchronous publication.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "Only the first pending HUD order publishes after a retained-row refresh.");

            hud.Apply(Frame("Retry"));
            Order().EmitSignal(Button.SignalName.Pressed);
            hud.Apply(Frame("Changed before dispatch"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "Replacing a queued row rejects its old activation.");
            Order().EmitSignal(Button.SignalName.Pressed);
            hud.Apply(Frame("Busy") with
            {
                Busy = true
            });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "A pending order cannot cross a busy projection.");

            hud.Apply(Frame());
            Order().EmitSignal(Button.SignalName.Pressed);
            stamp = new TacticalStamp(82, 2);
            hud.Apply(Frame());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "A pending order cannot cross a binding replacement at the same revision.");
            Order().EmitSignal(Button.SignalName.Pressed);
            RemoveChild(hud);
            AddChild(hud);
            hud.Apply(Frame());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "Unmount and remount reject a previously queued order.");
            Order().EmitSignal(Button.SignalName.Pressed);
            Order().Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 1, "A freed native button cannot publish its pending intent.");

            hud.Apply(Frame("Ready again"));
            Order().EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired == 2, "Rejected pending work does not leave current orders blocked.");
        }
        finally
        {
            RemoveChild(hud);
            hud.Free();
        }
    }

    private async System.Threading.Tasks.Task CheckDeferredSelectionContext()
    {
        var hud = new TacticalHud();
        AddChild(hud);
        var stamp = new TacticalStamp(83, 2);
        var unit = new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = 1 };
        var destination = unit with
        {
            Kind = TacticalSelectionKind.Destination,
            Destination = new(4, 5)
        };
        var target = destination with
        {
            Kind = TacticalSelectionKind.Target,
            Ability = "attack",
            Weapon = "sword"
        };
        var tactic = new TacticalSelection { Kind = TacticalSelectionKind.Tactic, Commander = "laiya", Ability = "rally" };
        var recruit = new TacticalSelection { Kind = TacticalSelectionKind.Recruit, Destination = new(4, 5) };
        var disembark = unit with
        {
            Kind = TacticalSelectionKind.Disembark,
            Passenger = 2
        };
        TacticalHudProjection Frame(TacticalSelection selection, TacticalHudIntentKind kind) => new(stamp, "", new[]
        {
            new TacticalHudSection("commands", "Orders", new[]
            {
                new TacticalHudRow("order", "Order", Intent: new(stamp, kind, "same-key", Context: selection.Context))
            })
        });
        Button Order() => hud.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.GetMeta("hud_key", "").AsString() == "commands/order");
        var fired = new List<TacticalHudIntent>();
        hud.IntentRequested += fired.Add;
        try
        {
            foreach (var (name, kind, previous, next) in new[]
            {
                ("actor", TacticalHudIntentKind.Command, unit, unit with { UnitId = 2 }),
                ("destination", TacticalHudIntentKind.Command, destination, destination with { Destination = new(5, 5) }),
                ("selection mode", TacticalHudIntentKind.Command, unit, unit with { Kind = TacticalSelectionKind.Destination }),
                ("recruitment site", TacticalHudIntentKind.Recruit, recruit, recruit with { Destination = new(5, 5) }),
                ("carrier", TacticalHudIntentKind.Disembark, unit, unit with { UnitId = 2 }),
                ("target ability", TacticalHudIntentKind.Back, target, target with { Ability = "heal" }),
                ("target weapon", TacticalHudIntentKind.Back, target, target with { Weapon = "bow" }),
                ("tactic commander", TacticalHudIntentKind.Back, tactic, tactic with { Commander = "torren" }),
                ("tactic identity", TacticalHudIntentKind.Back, tactic, tactic with { Ability = "charge" }),
                ("passenger", TacticalHudIntentKind.Back, disembark, disembark with { Passenger = 3 })
            })
            {
                hud.Apply(Frame(previous, kind));
                var old = Order();
                var count = fired.Count;
                old.EmitSignal(Button.SignalName.Pressed);
                hud.Apply(Frame(next, kind));
                Require(!ReferenceEquals(old, Order()) && !old.IsInsideTree(),
                    "An identical row with changed " + name + " must retire its previous native activation target.");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(fired.Count == count, "A deferred order cannot cross changed " + name + " context.");
                Order().EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(fired.Count == count + 1 && fired[^1].Context == next.Context,
                    "A deliberate current order remains available with its exact " + name + " context.");
            }
            var composed = target with
            {
                Path = new[] { new BoardCell(3, 5), new BoardCell(4, 5) },
                TargetSubjects = new[] { TacticalTarget.AtCell(new(6, 5)) }
            };
            hud.Apply(Frame(composed, TacticalHudIntentKind.Back));
            var retained = Order();
            var before = fired.Count;
            retained.EmitSignal(Button.SignalName.Pressed);
            var hovered = composed with
            {
                PreviewTarget = TacticalHit.Ground(new(6, 5)),
                Path = composed.Path.ToArray(),
                TargetSubjects = composed.TargetSubjects.ToArray()
            };
            hud.Apply(Frame(hovered, TacticalHudIntentKind.Back) with
            {
                Hint = "Pointer moved"
            });
            Require(ReferenceEquals(retained, Order()), "Transient preview and equivalent derived collections retain the same command context.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(fired.Count == before + 1, "Hover-only refresh permits its original queued order exactly once.");
        }
        finally
        {
            RemoveChild(hud);
            hud.Free();
        }
    }

    private async System.Threading.Tasks.Task CheckHudFlow()
    {
        CheckHudSnapshotLifetime();
        await CheckSitePointerBoundary();
        await CheckStructurePointerInspection();
        await CheckTacticKeyboardContext();
        await CheckHealingTarget();
        CheckInspectionOrder();
        CheckObjectiveChecklist();
        CheckPinnedUnit();
        await CheckPrimaryOrders();
        await CheckSiteInspection();
        await CheckResponsiveLayout();
        await CheckRecruitmentLayout();
        await CheckRecruitedUnitArt();
        await CheckForecastFacts();
        await CheckPointerCapture();
        await CheckDossierLifetime();
        await CheckRetainedSectionLifetime();
        await CheckTileTextRefresh();
        await CheckUnitDetailsRowRetention();
        await CheckOrderBadgeStates();
        await CheckDeferredOrderLifetime();
        await CheckDeferredSelectionContext();
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
        Require(fired == 0, "Native activation is deferred beyond the signal stack.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(fired == 1, "One-shot duplicate signal suppression");
        hud.Apply(Projection());
        old.EmitSignal(BaseButton.SignalName.Pressed);
        Require(fired == 1, "Equivalent refresh does not rearm a submitted native row");
        foreach (var visible in new[] { false, true, false, true })
        {
            hud.ShowHint = visible;
            Require(ReferenceEquals(old, Button()), "Hint visibility preserves existing command controls.");
            Button().EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Hint visibility must not rearm an already submitted order.");
        }
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
        Require(!GodotObject.IsInstanceValid(hud), "The final HUD must be destroyed before its resource-owning test scope completes.");
        resourceOwners = new[] { new WeakReference(assets), new WeakReference(art), new WeakReference(replacement),
            new WeakReference(shiftedArt), new WeakReference(explicitArt) };
    }
}
