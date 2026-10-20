#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

/// <summary>Real native resources plus canonical campaign states; no fabricated authored scenery.</summary>
public partial class DynamicStateChecks : Node
{
    private int assertions;
    public override void _Ready()
    {
        try { Run(); GD.Print($"Dynamic state presentation checks passed: {assertions} assertions."); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Run()
    {
        var assets = AssetCatalog.Load();
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var environment = EnvironmentCatalog.Load();
        var total = 0;
        foreach (var level in levels.OfType<JsonObject>())
        {
            var id = level["id"]!.GetValue<string>();
            var path = $"res://data/scenes/{id}.json";
            var scene = Godot.FileAccess.FileExists(path) ? AuthoredScene.Load(path) : new AuthoredScene
            {
                LevelId = id, MapSize = [level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>()], RuntimeReady = false,
            };
            var board = new BoardView();
            AddChild(board);
            board.Configure(assets, level, scene, environment, facing => facing == "west", facing =>
            {
                if (facing is null) return Vector2.Zero;
                var step = engine.Grids.Get("square4").Step(new Empire.Core.Geometry.Coord(0, 0), facing);
                return new Vector2(step.X, step.Y);
            });
            board.ConfigureDynamicState(packs);
            var state = engine.CreateState(level);
            var before = state.ToJsonString();
            board.RenderState(state);
            var count = state["structures"]!.AsArray().Count;
            Require(board.RenderedStructureCount == count, $"{id}: every canonical structure must be visible");
            total += count;
            Require(board.RenderedAuthoredPlacementCount == scene.Placements.Count + scene.FramePlacements.Count,
                $"{id}: dynamic state must not replace authored dressing");
            Require(!board.Diagnostics.Any(message => message.Contains("Structure ", StringComparison.Ordinal)),
                $"{id}: shipped structures must have a usable image or intentional source fallback");
            foreach (var node in DynamicNodes(board).Where(node => node.HasMeta("structure_id")))
            {
                var texture = node.GetChildren().OfType<Sprite2D>().First().Texture;
                using var image = texture.GetImage();
                Require(image.GetUsedRect().HasArea(), $"{id}: structure texture cannot be transparent");
            }
            if (id == "c01-03") CheckStructureTransitions(board, state, scene);
            if (id == "c01-09")
            {
                Require(count == 4 && DynamicNodes(board).Count(node => node.HasMeta("structure_id")) == 4,
                    "Composite must display its four canonical parts, not an invented fifth body");
                state["structures"]!.AsArray().RemoveAt(0);
                board.RenderState(state);
                Require(board.RenderedStructureCount == 3, "Removing a composite part must remove its picture");
            }
            if (id == "c01-15")
            {
                var mother = DynamicNodes(board).Single(node => node.HasMeta("structure_id"));
                Require(mother.GetMeta("rule_derived").AsBool() && mother.GetMeta("max_hp").AsDouble() == 500,
                    "Mother-root must use its source rule-derived drawing and canonical 500 HP");
            }
            if (id == "c01-01")
            {
                CheckMarkers(board, state);
                CheckBadges(board, state);
                CheckMapOwnership(board, state, assets);
                Require(board.RenderedAuthoredPlacementCount == scene.Placements.Count + scene.FramePlacements.Count,
                    "Marker updates must not duplicate or delete authored shelter and civilian decoration");
                var beforeVisibility = state.ToJsonString();
                var unitId = state["units"]![0]!["id"]!.GetValue<int>();
                board.RenderState(state, new System.Collections.Generic.HashSet<int> { unitId });
                Require(board.ProjectionUnitCount == state["units"]!.AsArray().Count && board.RenderedUnitCount == 1,
                    "Explicit visibility filters actors without changing canonical projection count");
                Require(state.ToJsonString() == beforeVisibility && !board.Diagnostics.Any(message => message.StartsWith("Unit ", StringComparison.Ordinal)),
                    "Hidden actors are not missing resources and visibility never mutates state");
            }
            if (id is not ("c01-01" or "c01-03" or "c01-09"))
                Require(state.ToJsonString() == before, $"{id}: projection must not mutate its supplied state");
            board.Free();
        }
        Require(total == 9, "Coverage must include all nine shipped GrayFlag live structures");
        var gauge = DynamicStateView.CreateGauge(0, 0);
        Require(gauge.GetMeta("ratio").AsDouble() == 1, "Zero maximum must match the source's whole gauge");
        gauge.Free();
    }

    private void CheckStructureTransitions(BoardView board, JsonObject state, AuthoredScene scene)
    {
        var structure = state["structures"]![0]!.AsObject();
        Node2D Current() => DynamicNodes(board).Single(node => node.HasMeta("structure_id"));
        Require(Current().GetMeta("visual_state").AsString() == "captured", "Owned healthy structure uses captured frame");
        var old = Current().Position;
        structure["hp"] = 50;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "damaged", "Exactly half HP selects damaged frame");
        Require(Math.Abs(Current().GetNode<Node2D>("HealthGauge").GetMeta("ratio").AsDouble() - 0.5) < 0.0001,
            "HP gauge must refresh to canonical damage");
        structure["hp"] = 100; structure["disabled"] = true;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "damaged", "Disabled healthy structure remains damaged");
        structure["disabled"] = false; structure["owner"] = 0; structure["x"] = structure["x"]!.GetValue<int>() + 1;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "normal" && Current().Position == old + new Vector2(32, 0),
            "Ownership and location must refresh without stale objects");
        var before = state.ToJsonString();
        board.RenderState(state); board.RenderState(state);
        Require(state.ToJsonString() == before && DynamicNodes(board).Count(node => node.HasMeta("structure_id")) == 1,
            "Repeated snapshots must neither mutate state nor duplicate a structure");
        Require(board.GetNode<Node2D>("AuthoredScene/AuthoredStanding").GetChildCount() == scene.Placements.Count,
            "Existing authored decoration must remain one independent layer");
        structure["hp"] = 0; structure["disabled"] = true;
        board.RenderState(state);
        Require(board.RenderedStructureCount == 1 && Current().GetMeta("visual_state").AsString() == "damaged" &&
            Current().GetNode<Node2D>("HealthGauge").GetMeta("ratio").AsDouble() == 0,
            "A destroyed-but-retained source structure must remain as a disabled damaged object");
        state["structures"]!.AsArray().Clear();
        board.RenderState(state);
        Require(board.RenderedStructureCount == 0 && !DynamicNodes(board).Any(node => node.HasMeta("structure_id")),
            "Removed structures must disappear, not remain as authored decoration");
    }

    private void CheckMarkers(BoardView board, JsonObject state)
    {
        foreach (var (kind, topic, id) in new[] { ("fallen_corpse", "C01-BPROP-AFTERMATH-2", 1),
            ("transport_wreck", "C01-BPROP-AFTERMATH-3", 2), ("routed", "C01-BPROP-EVIDENCE-1", 3) })
        {
            state["markers"]!.AsArray().Add(new JsonObject { ["id"] = id, ["kind"] = kind, ["owner"] = 1,
                ["at"] = new JsonObject { ["x"] = id + 3, ["y"] = 8 } });
            board.RenderState(state);
            var marker = DynamicNodes(board).Single(node => node.HasMeta("marker_id") && node.GetMeta("marker_id").AsInt32() == id);
            Require(marker.GetNode<Sprite2D>("AuthoredMarker").GetMeta("topic_id").AsString() == topic,
                $"Marker {kind} must use its actual source topic");
        }
        Require(board.RenderedMarkerCount == 3, "All canonical markers must render");
        var before = state.ToJsonString(); board.RenderState(state);
        Require(state.ToJsonString() == before && board.RenderedMarkerCount == 3, "Repeated markers must be detached and nonduplicated");
        state["markers"]!.AsArray().Clear(); board.RenderState(state);
        Require(board.RenderedMarkerCount == 0 && !DynamicNodes(board).Any(node => node.HasMeta("marker_id")),
            "Marker removal must remove its node");
    }

    private void CheckBadges(BoardView board, JsonObject state)
    {
        var unit = state["units"]![0]!.AsObject();
        var id = unit["id"]!.GetValue<int>();
        var x = unit["x"]!.GetValue<int>(); var y = unit["y"]!.GetValue<int>();
        unit["hp"] = 20; unit["done"] = true;
        unit["facing"] = "south";
        unit["capture"] = 0;
        var threshold = double.Parse(state["rules"]!["captureThreshold"]!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
        state["map"]!["captureProgress"]![y * state["map"]!["width"]!.GetValue<int>() + x] = threshold / 2;
        var before = state.ToJsonString();
        board.RenderState(state);
        var badges = board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}/Badges");
        Require(badges.HasNode("HealthGauge") && badges.HasNode("DoneShade"), "Damaged/done units must have visible source badges");
        Require(Math.Abs(badges.GetNode<Node2D>("CaptureGauge").GetMeta("ratio").AsDouble() - 0.5) < 0.0001,
            "Capture gauge must read canonical map progress, not unit capture");
        Require(state.ToJsonString() == before, "Badge projection must not advance unit or capture state");
        Require(badges.GetNode<Node2D>("FacingPip").GetMeta("direction").AsVector2() == Vector2.Down,
            "Facing pip must use the injected canonical grid vector");
    }

    private void CheckMapOwnership(BoardView board, JsonObject state, AssetCatalog assets)
    {
        var tiles = state["map"]!["tiles"]!.AsArray();
        var index = Enumerable.Range(0, tiles.Count).First(at => assets.HasMapStructureBinding(tiles[at]!.GetValue<string>()));
        Node2D Structure() => board.GetNode<Node2D>($"CanonicalProjection/MapStructure_{index}");
        state["map"]!["owners"]![index] = 1;
        board.RenderState(state);
        var first = Structure().GetNode<Sprite2D>("OwnerBanner");
        using var firstImage = first.Texture.GetImage();
        var firstPixels = firstImage.GetData();
        state["map"]!["owners"]![index] = 2;
        board.RenderState(state);
        var second = Structure().GetNode<Sprite2D>("OwnerBanner");
        using var secondImage = second.Texture.GetImage();
        Require(!firstPixels.SequenceEqual(secondImage.GetData()), "Changing site owner 1 to 2 must change actual rendered banner pixels");
        Require(second.GetMeta("owner_color").AsString() == "#" + Color.FromHtml(state["players"]![1]!["color"]!.GetValue<string>()).ToHtml(false),
            "Site banner must use the new canonical player's color");
        state["map"]!["owners"]![index] = 0;
        board.RenderState(state);
        Require(!Structure().HasNode("OwnerBanner"), "Neutral sites must remove the prior owner's banner");
    }

    private static Node2D[] DynamicNodes(BoardView board) =>
        board.GetNode<Node2D>("CanonicalProjection/DynamicState").GetChildren().OfType<Node2D>().ToArray();
    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
}
