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
    private void Require(bool condition, string message) { _checks++; if (!condition) throw new InvalidOperationException(message); }
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
            Frames(oracle, environment);
            await CoversAndLifecycle(oracle, assets, environment);
            GD.Print($"PASS {_checks} source scenery differential/resource/lifecycle assertions; no GUI acceptance claim.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Frames(JsonNode oracle, EnvironmentCatalog environment)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        Require(!JsonSerializer.Deserialize<AuthoredScene>("{}", options)!.HasAuthoredFramePlacements, "Absent frames lost");
        Require(JsonSerializer.Deserialize<AuthoredScene>("{\"framePlacements\":[]}", options)!.HasAuthoredFramePlacements, "Explicit empty frames lost");
        foreach (var field in oracle["fields"]!.AsArray())
        {
            var id = field!["id"]!.GetValue<string>();
            var path = $"res://data/scenes/{id}.json";
            var authored = Godot.FileAccess.FileExists(path) ? AuthoredScene.Load(path) : new AuthoredScene();
            Require(authored.HasAuthoredFramePlacements == field["authored"]!.GetValue<bool>(), $"Frame presence {id}");
            var source = XElement.Parse(field["foreground"]!.GetValue<string>()).Elements().ToArray();
            if (authored.HasAuthoredFramePlacements)
            {
                Require(source.Length == authored.FramePlacements.Count, $"Authored frame count {id}");
                continue;
            }
            var native = CandidateBoardScenery.Perimeter(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>());
            Require(native.SequenceEqual(CandidateBoardScenery.Perimeter(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>())), "Generic frame determinism");
            Require(native.Count == source.Length, $"Generic frame count {id}");
            for (var i = 0; i < native.Count; i++)
            {
                var placement = native[i];
                var outer = source[i]; var group = outer.Element("g")!; var raster = group.Element("svg")!; var image = raster.Element("image")!;
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
                Require(placement.Shadow == (Regex.IsMatch(classes, "is-frame-(left|right|bottom)") ? CandidateShadowKind.FrameEdge : CandidateShadowKind.Foreground), "Frame shadow selector");
            }
        }
        using var cache = new CandidateShadowCache();
        var root = new Node2D();
        try
        {
            var diagnostics = new System.Collections.Generic.List<string>();
            var count = CandidateBoardScenery.AddPerimeter(root, environment, 20, 15, cache, diagnostics);
            Require(count == CandidateBoardScenery.Perimeter(20, 15).Count && diagnostics.Count == 0, "Actual perimeter resources");
            Require(root.GetChildren().Cast<Sprite2D>().All(sprite => sprite.GetChildCount() == 1 && sprite.GetChild<Sprite2D>(0).Texture != null), "Perimeter filters mounted");
            var textures = cache.TextureCount;
            var second = new Node2D();
            root.AddChild(second);
            CandidateBoardScenery.AddPerimeter(second, environment, 20, 15, cache, diagnostics);
            Require(cache.TextureCount == textures, "Repeated perimeter grew filter cache");
        }
        finally { root.Free(); }
    }

    private async Task CoversAndLifecycle(JsonNode oracle, AssetCatalog assets, EnvironmentCatalog environment)
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/oracle/grayflag-initial-raw.json");
        var level = fixture["level"]!.AsObject(); var state = fixture["state"]!.AsObject();
        var scene = AuthoredScene.Load();
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        var cache = board.LightingResources;
        try
        {
            board.Configure(assets, level, scene, environment, _ => false);
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
                    var piece = pieces[i]!; var image = XElement.Parse(piece["markup"]!.GetValue<string>());
                    var sprite = root.GetChild<Sprite2D>(i); var asset = assets.ResolveTopic(sprite.GetMeta("topic_id").AsString());
                    Require(image.Attribute("href")!.Value.EndsWith(asset.ResourcePath["res://assets/".Length..], StringComparison.Ordinal), "Cover artwork");
                    Near(sprite.Position.X + sprite.Offset.X * sprite.Scale.X, piece["x"]!.GetValue<double>() + Num(image, "x"), "Cover X");
                    Near(sprite.Position.Y + sprite.Offset.Y * sprite.Scale.Y, piece["y"]!.GetValue<double>() + Num(image, "y"), "Cover Y");
                    Near(sprite.Texture.GetWidth() * sprite.Scale.X, Num(image, "width"), "Cover width");
                    Near(sprite.Texture.GetHeight() * sprite.Scale.Y, Num(image, "height"), "Cover height");
                    Require(sprite.GetChildCount() == 1 && sprite.GetChild<Sprite2D>(0).Texture != null, "Cover filter mounted");
                    Require(sprite.GetChild<Sprite2D>(0).Texture == cache.Get(sprite.Texture, CandidateShadowKind.BattleProp, 1 / sprite.Scale.X).Texture, "Cover filter uses logical image coordinates, not source pixels");
                    Require(sprite.GetChild<Sprite2D>(0).Texture != cache.Get(sprite.Texture, CandidateShadowKind.BattleProp, asset.PixelDensity).Texture, "Cover density negative control");
                    Require(sprite.GetChild<Sprite2D>(0).Texture != cache.Get(sprite.Texture, CandidateShadowKind.Topic, 1 / sprite.Scale.X).Texture, "Cover shadow strength negative control");
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var missing = new AuthoredScene { LevelId = scene.LevelId, MapSize = scene.MapSize, RuntimeReady = true };
            board.Configure(assets, level, missing, environment, _ => false);
            Require(cache.IsDisposed && cache.TextureCount == 0, "Rebind disposes old compositions");
            cache = board.LightingResources;
            Require(board.RenderedAuthoredPlacementCount == 0 && board.RenderedPerimeterPlacementCount > 0, "Generic and authored counts distinct");
            var explicitEmpty = new AuthoredScene { LevelId = scene.LevelId, MapSize = scene.MapSize, RuntimeReady = true, FramePlacements = new() };
            board.Configure(assets, level, explicitEmpty, environment, _ => false);
            Require(cache.IsDisposed && board.RenderedPerimeterPlacementCount == 0, "Explicit empty frame must suppress generic");
            cache = board.LightingResources;
        }
        finally { RemoveChild(board); board.Free(); }
        Require(cache.IsDisposed && cache.TextureCount == 0, "Final board cache released");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
