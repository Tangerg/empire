#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class MiniatureBoardChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(l => l["id"]!.GetValue<string>() == "c01-01");
            var state = engine.CreateState(level);
            var before = EcmaJson.Stringify(state);
            var map = state["map"]!.AsObject();
            var width = map["width"]!.GetValue<int>();
            var playable = map["tiles"]!.AsArray().Select((t, i) => (t, i)).Where(pair =>
                !engine.Catalog.Definition("terrains", pair.t!.GetValue<string>())!.Value.GetProperty("tags").EnumerateArray().Any(t => t.GetString() == "outside"))
                .Select(pair => new Coord(pair.i % width, pair.i / width)).ToHashSet();
            var projection = new BoardProjection(engine.Grids.Get("square4"), BoardPlane.Dimetric, map, playable);
            var host = new Control { Size = new(960, 640) };
            AddChild(host);
            var view = new MiniatureBoardView(engine.Catalog);
            host.AddChild(view);
            view.LoadAuthoredScene(AuthoredScene.Load("res://data/scenes/c01-01.json").MiniatureScenePath!);
            view.Render(projection, state, null);
            var focus = projection.CellCenter(new(18, 13));
            var camera = new BattlefieldCamera(new(projection.Size.X, projection.Size.Y), new(74, 62, 74, 74), new(focus.X, focus.Y));
            camera.Resize(new(960, 640), new(16, 110, 16, 134));
            view.SyncCamera(camera, new(960, 640));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var tested = 0;
            foreach (var triangle in view.Geometry.Surface)
            {
                var center = (triangle.A + triangle.B + triangle.C) / 3;
                var screen = view.Camera.UnprojectPosition(center);
                var origin = view.Camera.ProjectRayOrigin(screen);
                var direction = view.Camera.ProjectRayNormal(screen);
                if (MiniatureBoardGeometry.Intersect(origin, direction, triangle.A, triangle.B, triangle.C) is null)
                    throw new InvalidOperationException($"Camera ray failed exact canonical triangle at {triangle.Cell}.");
                tested++;
            }
            var visibleUnits = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var node in state["units"]!.AsArray())
            {
                var unit = node!.AsObject();
                var cell = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
                var screen = view.Camera.UnprojectPosition(view.Geometry.Contact(cell) + Vector3.Up * .55f);
                var hit = view.Pick(screen);
                if (hit is { Kind: TacticalHitKind.Unit } && hit.UnitId == unit["id"]!.GetValue<int>())
                    visibleUnits++;
            }
            GD.Print($"Miniature 19 alpha/body picks: {clock.Elapsed.TotalMilliseconds:0.0}ms total.");
            if (visibleUnits < 3)
                throw new InvalidOperationException($"Insufficient true sprite ray witnesses: {visibleUnits}.");
            var viewport = view.GetNode<SubViewport>("MiniatureViewport");
            void Invalidates(Action action, string label)
            {
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                action();
                if (viewport.RenderTargetUpdateMode != SubViewport.UpdateMode.Once)
                    throw new InvalidOperationException($"Miniature {label} did not request a new frame.");
            }
            foreach (var preset in Enum.GetValues<MiniatureRenderPreset>())
                Invalidates(() => view.ApplyRenderPreset(preset), $"preset {preset}");
            view.ApplyRenderPreset(MiniatureRenderPreset.Reference);
            Invalidates(() => view.SyncCamera(camera, new(960, 640)), "camera");
            var firstUnit = state["units"]!.AsArray()[0]!.AsObject();
            var firstCell = new BoardCell(firstUnit["x"]!.GetValue<int>(), firstUnit["y"]!.GetValue<int>());
            Invalidates(() => view.PresentContact(firstUnit["id"]!.GetValue<int>(), projection.LogicalCellContact(new(firstCell.X, firstCell.Y))), "receipt contact");
            var none = Array.Empty<BoardCell>();
            Invalidates(() => view.ShowOverlay(new(firstCell, firstCell, none, none, none, none, none, null, none), none), "selection/roof/range");
            Invalidates(() => view.Render(projection, state, null), "snapshot");
            Invalidates(() => view.PresentEvent(EcmaJson.ToElement(new JsonObject { ["type"] = "heal", ["target"] = firstUnit["id"]!.GetValue<int>(), ["amount"] = 1 })), "receipt feedback");
            Invalidates(() => view._Process(1.0 / 60), "active transient feedback");
            GD.Print("Miniature local quality presets and all current mutation paths request redraw; no gameplay state owner added.");
            if (before != EcmaJson.Stringify(state))
                throw new InvalidOperationException("Miniature presentation mutated canonical state.");
            GD.Print($"MINIATURE_BOARD_OK {tested} exact triangles; {projection.Faces.Count} canonical faces; {visibleUnits} alpha-picked units; state unchanged.");
            view.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
