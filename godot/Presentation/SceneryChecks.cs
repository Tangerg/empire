#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Godot;

namespace Empire.Presentation;

public partial class SceneryChecks : Node
{
    private int _checks;
    private static double Num(XElement element, string key) => double.Parse(element.Attribute(key)!.Value, CultureInfo.InvariantCulture);
    private static double[] Numbers(string value) => Regex.Matches(value, @"-?\d+(?:\.\d+)?").Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)).ToArray();
    private void Require(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private void Near(double actual, double expected, string message, double tolerance = .006) => Require(Math.Abs(actual - expected) < tolerance, $"{message}: {actual} != {expected}");

    public override async void _Ready()
    {
        try
        {
            var arg = OS.GetCmdlineUserArgs().Single(value => value.StartsWith("--scenery-oracle="));
            var path = arg["--scenery-oracle=".Length..];
            using var file = System.IO.File.OpenRead(ProjectSettings.GlobalizePath(path));
            using var input = path.EndsWith(".gz", StringComparison.Ordinal) ? new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress) : (System.IO.Stream)file;
            var oracle = JsonNode.Parse(input)!;
            var environment = EnvironmentCatalog.Load();
            var assets = AssetCatalog.Load();
            HistoricalFrames(oracle, environment);
            await CoversAndLifecycle(oracle, assets, environment);
            GD.Print($"PASS {_checks} historical source-art and current registered scenery/resource/lifecycle assertions; no GUI acceptance claim.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void HistoricalFrames(JsonNode oracle, EnvironmentCatalog environment)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        Require(!JsonSerializer.Deserialize<AuthoredScene>("{}", options)!.HasAuthoredFramePlacements, "Absent frames lost");
        Require(JsonSerializer.Deserialize<AuthoredScene>("{\"framePlacements\":[]}", options)!.HasAuthoredFramePlacements, "Explicit empty frames lost");
        foreach (var field in oracle["fields"]!.AsArray())
        {
            var id = field!["id"]!.GetValue<string>();
            // Source declarations and their historical SVG are one pinned fixture. They are not
            // the current native scene, whose map registration and artwork evolved independently.
            var declared = field["framePlacements"] as JsonArray;
            Require((declared is not null) == field["authored"]!.GetValue<bool>(), $"Historical frame declaration presence {id}");
            var source = XElement.Parse(field["foreground"]!.GetValue<string>()).Elements().ToArray();
            var native = declared is null
                ? BoardScenery.Perimeter(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>())
                : declared.Select(item => new BoardScenery.Placement(item!["id"]!.GetValue<string>(),
                    item["contact"]![0]!.GetValue<double>() * 32, item["contact"]![1]!.GetValue<double>() * 32,
                    item["scale"]?.GetValue<double>() ?? 1, item["flip"]?.GetValue<bool>() ?? false,
                    1, ShadowKind.Foreground)).ToArray();
            if (declared is null)
                Require(native.SequenceEqual(BoardScenery.Perimeter(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>())), "Generic frame determinism");
            Require(native.Count == source.Length, $"Historical frame count {id}");
            for (var i = 0; i < native.Count; i++)
            {
                var placement = native[i];
                var outer = source[i];
                var group = outer.Element("g")!;
                var raster = group.Element("svg")!;
                var image = raster.Element("image")!;
                var cell = environment.ResolveCell(placement.Id);
                var contact = Numbers(outer.Attribute("transform")!.Value);
                Near(placement.X + 74, contact[0], $"Frame X {id}/{i}");
                Near(placement.Y + 62, contact[1], $"Frame Y {id}/{i}");
                var transform = Numbers(group.Attribute("transform")!.Value);
                var scale = placement.Scale * cell.PresentationScale;
                Near(transform[2], placement.Flip ? -scale : scale, "Frame scale", .00051);
                Near(transform[0], (placement.Flip ? cell.CellWidth - cell.Anchor.X : -cell.Anchor.X) * scale, "Frame anchor X", .0006);
                Near(transform[1], -cell.Anchor.Y * scale, "Frame anchor Y", .0006);
                Near(placement.Opacity, Num(group, "opacity"), "Frame opacity");
                Require(image.Attribute("href")!.Value.EndsWith(cell.ResourcePath["res://assets/".Length..], StringComparison.Ordinal), "Original frame artwork");
                Near(Num(image, "x"), -(cell.Index % cell.Columns) * cell.CellWidth, "Atlas cell X");
                Near(Num(image, "y"), -(cell.Index / cell.Columns) * cell.CellHeight, "Atlas cell Y");
                var classes = group.Attribute("class")!.Value;
                Require(placement.Shadow == (Regex.IsMatch(classes, "is-frame-(left|right|bottom)") ? ShadowKind.FrameEdge : ShadowKind.Foreground), "Frame shadow selector");
            }
        }
        using var cache = new ShadowCache();
        var root = new Node2D();
        try
        {
            var diagnostics = new System.Collections.Generic.List<string>();
            var count = BoardScenery.AddPerimeter(root, environment, 20, 15, cache, diagnostics);
            Require(count == BoardScenery.Perimeter(20, 15).Count && diagnostics.Count == 0, "Actual perimeter resources");
            Require(root.GetChildren().Cast<Sprite2D>().All(sprite => sprite.GetChildCount() == 1 && sprite.GetChild<Sprite2D>(0).Texture != null), "Perimeter filters mounted");
            var textures = cache.TextureCount;
            var second = new Node2D();
            root.AddChild(second);
            BoardScenery.AddPerimeter(second, environment, 20, 15, cache, diagnostics);
            Require(cache.TextureCount == textures, "Repeated perimeter grew filter cache");
        }
        finally { root.Free(); }
    }

    private async Task CoversAndLifecycle(JsonNode oracle, AssetCatalog assets, EnvironmentCatalog environment)
    {
        var engine = new Empire.Core.Engine.BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel();
        var state = engine.CreateState(level);
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        Require(scene.AssembledGroundPath is not null && scene.HasAuthoredFramePlacements && scene.FramePlacements.Count == 0,
            "Current C01 owns its assembled scenery and explicitly suppresses legacy generic frames.");
        var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        var cache = board.LightingResources;
        try
        {
            board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            Require(cache.IsDisposed, "Configure releases prior cache");
            cache = board.LightingResources;
            Require(board.RenderedPerimeterPlacementCount == 0, "Authored frame replaced by generic");
            foreach (var sample in oracle["covers"]!.AsArray())
            {
                state["map"]!["directionalCover"] = sample!["directionalCover"]!.DeepClone();
                var before = state.ToJsonString();
                board.RenderState(state);
                Require(state.ToJsonString() == before, "Cover renderer mutated state");
                var root = board.GetNode<Node2D>("CanonicalProjection/IllustratedCover");
                var pieces = sample["pieces"]!.AsArray();
                Require(board.RenderedCoverPropCount == pieces.Count && root.GetChildCount() == pieces.Count, "Live cover count");
                Require(root.ZIndex < -2, "Cover must remain below range/path");
                for (var i = 0; i < pieces.Count; i++)
                {
                    var piece = pieces[i]!;
                    var image = XElement.Parse(piece["markup"]!.GetValue<string>());
                    var sprite = root.GetChild<Sprite2D>(i);
                    var asset = assets.ResolveTopic(sprite.GetMeta("topic_id").AsString());
                    Require(image.Attribute("href")!.Value.EndsWith(asset.ResourcePath["res://assets/".Length..], StringComparison.Ordinal), "Cover artwork");
                    // Historical TS art used foot (16,31); current contacts belong to BoardProjection.
                    Near(sprite.Offset.X * sprite.Scale.X, Num(image, "x") - 16, "Historical cover artwork offset X");
                    Near(sprite.Offset.Y * sprite.Scale.Y, Num(image, "y") - 31, "Historical cover artwork offset Y");
                    var at = new Empire.Core.Geometry.Coord((int)(piece["x"]!.GetValue<double>() / 32),
                        (int)(piece["y"]!.GetValue<double>() / 32));
                    var logical = board.Projection.LogicalCellContact(at);
                    Require(sprite.Position.IsEqualApprox(board.Projection.CellContact(at)),
                        "Current cover feet must use the registered canonical support plane, not old SVG absolute coordinates.");
                    Require(!sprite.ZAsRelative && sprite.ZIndex == board.Projection.LayerAtDepth(board.Projection.Depth(logical), BoardDepthLayer.Standing),
                        "Current cover uses the same standing-depth owner as actors and buildings.");
                    Near(sprite.Texture.GetWidth() * sprite.Scale.X, Num(image, "width"), "Cover width");
                    Near(sprite.Texture.GetHeight() * sprite.Scale.Y, Num(image, "height"), "Cover height");
                    Require(sprite.GetChildCount() == 1 && sprite.GetChild<Sprite2D>(0).Texture != null, "Cover filter mounted");
                    Require(sprite.GetChild<Sprite2D>(0).Texture == cache.Get(sprite.Texture, ShadowKind.BattleProp, 1 / sprite.Scale.X).Texture, "Cover filter uses logical image coordinates, not source pixels");
                    Require(sprite.GetChild<Sprite2D>(0).Texture != cache.Get(sprite.Texture, ShadowKind.BattleProp, asset.PixelDensity).Texture, "Cover density negative control");
                    Require(sprite.GetChild<Sprite2D>(0).Texture != cache.Get(sprite.Texture, ShadowKind.Topic, 1 / sprite.Scale.X).Texture, "Cover shadow strength negative control");
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var missing = new AuthoredScene { LevelId = scene.LevelId, MapSize = scene.MapSize, RuntimeReady = true };
            board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, missing);
            Require(cache.IsDisposed && cache.TextureCount == 0, "Rebind disposes old compositions");
            cache = board.LightingResources;
            Require(board.RenderedAuthoredPlacementCount == 0 && board.RenderedPerimeterPlacementCount > 0, "Generic and authored counts distinct");
            var explicitEmpty = new AuthoredScene { LevelId = scene.LevelId, MapSize = scene.MapSize, RuntimeReady = true, FramePlacements = new() };
            board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, explicitEmpty);
            Require(cache.IsDisposed && board.RenderedPerimeterPlacementCount == 0, "Explicit empty frame must suppress generic");
            cache = board.LightingResources;
        }
        finally { RemoveChild(board); board.Free(); }
        Require(cache.IsDisposed && cache.TextureCount == 0, "Final board cache released");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
