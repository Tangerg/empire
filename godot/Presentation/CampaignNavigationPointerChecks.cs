#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire.Presentation.Tactical;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckPointerHudRetention()
    {
        ResetAutosave();
        var game = MountGame();
        try
        {
            Press(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
            while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                CompleteStory(game);
            Press(Buttons(View<StoryScreen>(game)).First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            var session = Campaign(game).Battle!;
            var screen = View<BattleScreen>(game);
            var map = (Control)screen.FindChild("MapArea", true, false);
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var viewport = GetViewport();
            viewport.NotifyMouseEntered();
            foreach (var pressed in new[] { true, false })
            {
                using var key = new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = pressed };
                viewport.PushInput(key, true);
            }
            await Layout();
            var selected = screen.SelectedUnitId ?? throw new InvalidOperationException("Native Space did not select a formal actor.");
            var queries = session.GetTacticalQueries(0);
            var points = new List<(BoardCell Cell, Vector2 Pixel)>();
            void Motion(Vector2 at)
            {
                using var input = new InputEventMouseMotion { Position = at, GlobalPosition = at };
                viewport.PushInput(input, true);
            }
            void CheckTile(BoardCell cell, int? inspect)
            {
                var projection = hud.Projection!;
                var expected = session.ProjectHud(projection.Stamp, screen.Selection!, TacticalHit.Ground(cell), inspect, false)
                    .Sections.Single(section => section.Key == "tile");
                var actual = projection.Sections.SingleOrDefault(section => section.Key == "tile");
                var labels = hud.FindChild("tile", true, false) is { } nativeTile
                    ? nativeTile.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text)
                    : Array.Empty<string>();
                Require(actual is not null && actual.Title == expected.Title && actual.Rows.SequenceEqual(expected.Rows)
                    && expected.Rows.All(row => labels.Contains(row.Label)),
                    "Every native pointer cell must refresh its canonical terrain facts and coordinate labels, including idle inspection.");
            }
            foreach (var cell in queries.MoveStops(selected))
            {
                var point = screen.Board.ToGlobal(screen.Board.Projection.CellCenter(new(cell.X, cell.Y)));
                if (!map.GetGlobalRect().HasPoint(point) || screen.Board.Pick(screen.Board.ToLocal(point)) is not { Kind: TacticalHitKind.Ground })
                    continue;
                Motion(point);
                if (viewport.GuiGetHoveredControl() == map)
                    points.Add((cell, point));
                if (points.Count == 16)
                    break;
            }
            Require(points.Count >= 2 && screen.Selection?.UnitId == selected && screen.Selection.Kind == TacticalSelectionKind.Unit,
                "The formal battle must offer distinct native-reachable ground cells while its actor stays selected.");
            await Layout();
            var before = Digest(session.Snapshot());
            var names = new[] { "crownPlate", "objectivesPlate", "unit", "PortraitTray", "PortraitMedallion", "Details", "unit-details", "stances", "formations", "roster", "commands", "DossierButton", "SelectionHint" };
            var selectedHint = hud.Projection!.Hint;
            var retained = names.Select(name => hud.FindChild(name, true, false)
                ?? throw new InvalidOperationException("Missing formal HUD subtree: " + name)).ToArray();
            for (var sample = 0; sample < 3; sample++)
            {
                long allocated = 0;
                double elapsed = 0;
                foreach (var point in points)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    Motion(point.Pixel);
                    allocated += GC.GetAllocatedBytesForCurrentThread() - start;
                    timer.Stop();
                    elapsed += timer.Elapsed.TotalMilliseconds;
                    Require(viewport.GuiGetHoveredControl() == map && screen.Selection?.UnitId == selected,
                        "Measured mouse motion must reach the actual map without losing the acting unit.");
                    Require(hud.Projection!.Hint == selectedHint, "Measured terrain movement leaves the selected-unit instruction unchanged.");
                    Require(retained.All(node => GodotObject.IsInstanceValid(node) && node.IsInsideTree()
                        && ReferenceEquals(node, hud.FindChild(node.Name, true, false))),
                        "Pointer-only changes must retain unchanged native HUD sections, portraits and dossier controls instead of reshaping their text.");
                    CheckTile(point.Cell, selected);
                    await Layout();
                }
                GD.Print($"POINTER_HUD sample={sample} cells={points.Count} allocated={allocated} ms={elapsed:F3}");
                Require(allocated < points.Count * 2L * 1024 * 1024,
                    "Repeated pointer previews must reuse the snapshot movement field instead of allocating a new traversal for every movement, path and threat query.");
            }
            var objectives = (VBoxContainer)hud.FindChild("objectives", true, false);
            var objectiveToggle = objectives.GetChildren().OfType<Button>().Single();
            objectiveToggle.ButtonPressed = false;
            Require(!objectives.GetNode<Control>("Content").Visible, "Retained objective disclosure must still collapse after pointer refreshes.");
            objectiveToggle.ButtonPressed = true;
            Require(objectives.GetNode<Control>("Content").Visible, "Retained objective disclosure must still expand after pointer refreshes.");
            var opener = (Button)hud.FindChild("DossierButton", true, false);
            Motion(opener.GetGlobalRect().GetCenter());
            await Layout();
            opener = (Button)hud.FindChild("DossierButton", true, false);
            var buttonPoint = opener.GetGlobalRect().GetCenter();
            Require(viewport.GuiGetHoveredControl() == opener,
                "A retained dossier opener stays above newly mounted portrait trays and receives native pointer input.");
            foreach (var pressed in new[] { true, false })
            {
                using var input = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = buttonPoint, GlobalPosition = buttonPoint };
                viewport.PushInput(input, true);
            }
            await Layout();
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            Require(popup.Visible, "A real pointer click must still open the dossier after repeated HUD retention.");
            var unitDetails = (VBoxContainer)hud.FindChild("unit-details", true, false);
            var toggle = unitDetails.GetChildren().OfType<Button>().Single();
            toggle.GrabFocus();
            foreach (var open in new[] { true, false })
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var key = new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = pressed };
                    popup.PushInput(key, true);
                }
                await Layout();
                Require(toggle.ButtonPressed == open && unitDetails.GetNode<Control>("Content").Visible == open,
                    "Native keyboard activation must continue toggling a retained dossier disclosure.");
            }
            Press((Button)hud.FindChild("DossierClose", true, false));
            await Layout();
            Require(!popup.Visible && ((Button)hud.FindChild("DossierButton", true, false)).HasFocus(),
                "Closing the refreshed dossier must return focus to its live opener.");
            var focusedOpener = (Button)hud.FindChild("DossierButton", true, false);
            Motion(points[0].Pixel);
            await Layout();
            Require(GodotObject.IsInstanceValid(focusedOpener) && focusedOpener.IsInsideTree() && focusedOpener.HasFocus(),
                "Moving the pointer over another terrain cell must preserve keyboard focus returned to the dossier opener.");
            foreach (var pressed in new[] { true, false })
            {
                using var key = new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = pressed };
                viewport.PushInput(key, true);
            }
            await Layout();
            Require(popup.Visible && screen.SelectedUnitId == selected,
                "Space after pointer-only terrain inspection must reopen the focused dossier, not cycle the acting unit.");
            Press((Button)hud.FindChild("DossierClose", true, false));
            await Layout();
            for (var press = 0; press < 2; press++)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var key = new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = pressed };
                    viewport.PushInput(key, true);
                }
                await Layout();
                if (press == 0)
                    Require(viewport.GuiGetFocusOwner() is null && screen.Selection?.UnitId == selected,
                        "The first Escape returns dossier-opener focus to the map without discarding the selected actor.");
            }
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle,
                "Native Escape must return the selected actor to idle map inspection.");
            var idleInstruction = hud.FindChild("SelectionHint", false, false);
            var idleBacking = hud.FindChild("InstructionTray", false, false);
            Require(idleInstruction is not null && idleBacking is not null, "Actual idle play mounts guidance with its own backing.");
            var recruitment = session.GetTacticalQueries(0);
            var canRecruit = recruitment.InteractiveCells.Any(recruitment.CanOpenRecruitment);
            var idleHint = GodotTextCatalog.LoadSimplifiedChinese().Format(canRecruit ? "ui.hud.selection.idle_recruit" : "ui.hud.selection.idle");
            for (var sample = 0; sample < 3; sample++)
            {
                long allocated = 0;
                double elapsed = 0;
                foreach (var point in points)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    Motion(point.Pixel);
                    allocated += GC.GetAllocatedBytesForCurrentThread() - start;
                    timer.Stop();
                    elapsed += timer.Elapsed.TotalMilliseconds;
                    Require(viewport.GuiGetHoveredControl() == map && screen.Selection?.Kind == TacticalSelectionKind.Idle
                        && hud.Projection!.Hint == idleHint,
                        "Idle pointer motion must reach the real map and preserve the canonical recruitment guidance.");
                    Require(ReferenceEquals(idleInstruction, hud.FindChild("SelectionHint", false, false))
                        && ReferenceEquals(idleBacking, hud.FindChild("InstructionTray", false, false)),
                        "Idle terrain motion must retain unchanged native instructions and their pointer backing.");
                    CheckTile(point.Cell, null);
                    await Layout();
                }
                GD.Print($"POINTER_IDLE sample={sample} cells={points.Count} allocated={allocated} ms={elapsed:F3}");
                Require(allocated < points.Count * 2L * 1024 * 1024,
                    "Idle pointer inspection must not rescan recruitment across the unchanged battle map on every hover.");
            }
            Require(Digest(session.Snapshot()) == before && session.Revision == 0,
                "Pointer motion and retained dossier interactions must not mutate canonical battle state.");
        }
        finally { Retire(game); }
    }
}
