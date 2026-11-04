#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private async Task CheckTacticKeyboardContext()
    {
        var fixture = PresentationJson.Load<JsonArray>("res://tests/native/recorded-status-feedback.json").OfType<JsonObject>()
            .Single(value => value["levelId"]!.GetValue<string>() == "c01-01");
        var checkpoint = fixture["checkpoints"]!.AsArray()[0]!.AsObject();
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-01");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Keyboard tactic witness starts from the shipped campaign opening.");
        foreach (var action in fixture["actions"]!.AsArray().Take(checkpoint["index"]!.GetValue<int>()).OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), "Original legal orders reach the recorded rally checkpoint.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        var original = EcmaJson.Stringify(session.Snapshot());
        var originalSize = GetWindow().Size;
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            GetWindow().Size = new Vector2I(1180, 812);
            screen.Board.AnimationsEnabled = false;
            var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
            var rendering = BoardSceneRendering.Load(scene, level, session.Snapshot()["map"]!.AsObject());
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
            screen.ConfigureGround(rendering.Ground);
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(1180, 812);
            var map = (Control)screen.FindChild("MapArea", true, false);
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var viewport = GetViewport();
            viewport.NotifyMouseExited();
            viewport.NotifyMouseEntered();
            async Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            void Motion(Vector2 at) => viewport.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
            void Click(Vector2 at)
            {
                foreach (var down in new[] { true, false })
                    viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at }, true);
            }
            void KeyPress(Key key)
            {
                foreach (var down in new[] { true, false })
                    viewport.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down }, true);
            }
            Button Rally() => hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "tactics/laiya-command:c01.gray-rally");
            await Layout();
            var foundGround = false;
            foreach (var cell in session.GetTacticalQueries(0).InteractiveCells)
            {
                var point = screen.Board.ToGlobal(screen.Board.Projection.CellCenter(new(cell.X, cell.Y)));
                if (!map.GetGlobalRect().HasPoint(point) || screen.Board.Pick(screen.Board.ToLocal(point)) is not { Kind: TacticalHitKind.Ground })
                    continue;
                Motion(point);
                if (viewport.GuiGetHoveredControl() != map)
                    continue;
                foundGround = true;
                break;
            }
            await Layout();
            Require(foundGround && hud.Projection!.Sections.Any(section => section.Key == "tile"), "A real native map motion establishes a ground cursor before entering the tactic tray.");
            var buttonPoint = Rally().GetGlobalRect().GetCenter();
            Motion(buttonPoint);
            await Layout();
            Require(viewport.GuiGetHoveredControl() is { } hovered && hud.IsAncestorOf(hovered)
                && hud.Projection!.Sections.All(section => section.Key != "tile") && screen.Selection?.Kind == TacticalSelectionKind.Idle,
                "Ordinary map-to-HUD transfer clears the ground cursor without selecting an actor.");
            Click(buttonPoint);
            await Layout();
            Require(screen.Selection is { Kind: TacticalSelectionKind.Tactic, Commander: "laiya-command", Ability: "c01.gray-rally", UnitId: null }
                && session.Revision == 0 && session.Events.Count == 0,
                "Actual native tactic click arms rally and does not spend its command points.");
            var subjects = screen.Selection!.TargetSubjects.ToArray();
            KeyPress(Key.D);
            await Layout();
            GD.Print($"TACTIC_KEYBOARD_CONTEXT after native HUD click + D: selection={screen.Selection?.Kind}; revision={session.Revision}; tile={hud.Projection!.Sections.FirstOrDefault(section => section.Key == "tile")?.Rows.FirstOrDefault(row => row.Key == "coordinate")?.Label}");
            Require(screen.Selection is { Kind: TacticalSelectionKind.Tactic, Commander: "laiya-command", Ability: "c01.gray-rally" }
                && screen.Selection.TargetSubjects.SequenceEqual(subjects) && session.Revision == 0
                && EcmaJson.Stringify(session.Snapshot()) == original,
                "The first keyboard direction must retain the armed tactic and establish target focus, never cycle to an unrelated actor.");
            var queries = session.GetTacticalQueries(0);
            var initialTarget = subjects.Select(subject => subject.Cell).First(queries.InteractiveCells.Contains);
            string Coordinate() => hud.Projection!.Sections.Single(section => section.Key == "tile").Rows.Single(row => row.Key == "coordinate").Label;
            Require(Coordinate() == $"{initialTarget.X}, {initialTarget.Y}", "Target-only keyboard focus starts on the first canonical interactive target, not an invented coordinate.");
            KeyPress(Key.Escape);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && session.Revision == 0,
                "Canceling a keyboard-armed rally remains a reversible local selection.");
            await Layout();
            buttonPoint = Rally().GetGlobalRect().GetCenter();
            Motion(buttonPoint);
            await Layout();
            Click(buttonPoint);
            await Layout();
            Require(screen.Selection?.Kind == TacticalSelectionKind.Tactic && session.Revision == 0,
                "A second native button click rearms the same uncommitted rally after cancellation.");
            GetWindow().EmitSignal(Window.SignalName.MouseExited);
            await Layout();
            Require(screen.Selection?.Kind == TacticalSelectionKind.Tactic && hud.Projection!.Sections.All(section => section.Key != "tile"),
                "Window leave clears only the cursor, not the armed tactic or its canonical targets.");
            KeyPress(Key.Right);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Tactic && Coordinate() == $"{initialTarget.X}, {initialTarget.Y}",
                "The arrow-key path reestablishes the same target after cancel/re-arm and actual window leave.");
            var action = fixture["actions"]!.AsArray()[checkpoint["index"]!.GetValue<int>()]!;
            var target = new BoardCell(action["target"]!["x"]!.GetValue<int>(), action["target"]!["y"]!.GetValue<int>());
            var previous = new Dictionary<BoardCell, BoardCell>();
            var pending = new Queue<BoardCell>();
            var seen = new HashSet<BoardCell> { initialTarget };
            pending.Enqueue(initialTarget);
            while (pending.TryDequeue(out var current) && !seen.Contains(target))
                foreach (var next in queries.DirectionSteps(current).Where(queries.InteractiveCells.Contains))
                    if (seen.Add(next))
                    {
                        previous[next] = current;
                        pending.Enqueue(next);
                    }
            Require(seen.Contains(target), "Canonical interactive grid steps reach the original rally target.");
            var route = new List<BoardCell>();
            for (var at = target; at != initialTarget; at = previous[at])
                route.Add(at);
            route.Reverse();
            var cursor = initialTarget;
            foreach (var next in route)
            {
                var key = next.X > cursor.X ? Key.Right : next.X < cursor.X ? Key.Left : next.Y > cursor.Y ? Key.Down : Key.Up;
                KeyPress(key);
                Require(screen.Selection?.Kind == TacticalSelectionKind.Tactic && Coordinate() == $"{next.X}, {next.Y}",
                    "Every native directional input keeps the selected tactic and follows the configured grid.");
                cursor = next;
            }
            Require(session.Revision == 0 && EcmaJson.Stringify(session.Snapshot()) == original,
                "Keyboard reveal, cancel, re-arm and navigation never spend resources or advance the battle.");
            KeyPress(Key.Enter);
            Require(session.Revision == 1 && EcmaJson.DeepEquals(session.ActionLog.Single(), action)
                && StateDigest.Hash(session.Snapshot()) == checkpoint["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == checkpoint["eventsSha256"]!.GetValue<string>(),
                "Only Enter confirms the exact original rally, its cost, state and ordered events.");
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && !screen.IsPresenting,
                "The confirmed tactic settles through the normal presentation and selection lifecycle.");
        }
        finally
        {
            GetViewport().NotifyMouseExited();
            RemoveChild(screen);
            screen.Free();
            GetWindow().Size = originalSize;
        }
    }
}
