#nullable enable
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class MiniatureInputChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            foreach (var width in new[] { 960, 1180 })
                await CheckPointer(width);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CheckPointer(int width)
    {
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(engine.CreateState(level)));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            GetWindow().Size = new Vector2I(width, 640);
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(width, 640);
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            var map = (Control)screen.FindChild("MapArea", true, false);
            var view = (MiniatureBoardView)screen.FindChild("MiniaturePresentation", true, false);
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            await Layout();
            var original = EcmaJson.Stringify(session.Snapshot());

            var start = new BoardCell(9, 18);
            const int shieldId = 8;
            var expected = TacticalHit.Unit(start, shieldId);
            var center = view.Camera.UnprojectPosition(view.Geometry.Contact(start) + Vector3.Up * .55f);
            var actorPoint = Enumerable.Range(-5, 11).SelectMany(y => Enumerable.Range(-8, 17)
                    .Select(x => center + new Vector2(x, y)))
                .OrderBy(point => point.DistanceSquaredTo(center))
                .First(point => view.Pick(point) == expected);
            await Click(map.GetGlobalTransformWithCanvas() * actorPoint);
            Require(screen.SelectedUnitId == shieldId, "The actor must be selectable with the mounted world pointer.");

            var zoom = (Button)screen.FindChild("ZoomOut", true, false);
            await Click(zoom.GetGlobalRect().GetCenter());
            Require(screen.SelectedUnitId == shieldId && session.Revision == 0 && original == EcmaJson.Stringify(session.Snapshot()),
                "The HUD button must not change battle selection or commit a world command.");

            var goal = new BoardCell(10, 17);
            var groundPoint = view.Camera.UnprojectPosition(view.Geometry.Contact(goal) + Vector3.Up * .025f);
            Require(view.Pick(groundPoint) == TacticalHit.Ground(goal), "The lawful preview point must hit actual terrain after viewport re-layout.");
            await Click(map.GetGlobalTransformWithCanvas() * groundPoint);
            Require(session.Revision == 0 && original == EcmaJson.Stringify(session.Snapshot()),
                "World preview must not dispatch before an action is chosen.");
            var wait = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.IsVisibleInTree() && button.GetMeta("hud_key", "").AsString() == "commands/wait");
            await Click(wait.GetGlobalRect().GetCenter());
            var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<int>() == shieldId);
            Require(session.Revision == 1 && session.ActionLog.Count == 1
                && unit["x"]!.GetValue<int>() == goal.X && unit["y"]!.GetValue<int>() == goal.Y,
                "One confirmed pointer command must move the real actor and advance exactly one revision.");
            GD.Print($"MINIATURE_INPUT_OK size={width}x640 from={start} to={goal} revision={session.Revision}");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
            await Layout();
        }
    }

    private async Task Click(Vector2 position)
    {
        var viewport = GetViewport();
        using var motion = new InputEventMouseMotion { Position = position, GlobalPosition = position };
        viewport.PushInput(motion, true);
        foreach (var pressed in new[] { true, false })
        {
            using var button = new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = pressed
            };
            viewport.PushInput(button, true);
        }
        await Layout();
    }

    private async Task Layout()
    {
        for (var index = 0; index < 3; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
