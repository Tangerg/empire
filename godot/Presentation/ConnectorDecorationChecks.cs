#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Source-executed route cell order versus the actual native terrain tree.</summary>
public partial class ConnectorDecorationChecks : Node
{
    public override async void _Ready()
    {
        var assertions = 0;
        try
        {
            var arg = OS.GetCmdlineUserArgs().Single(value => value.StartsWith("--connectors="));
            var path = arg["--connectors=".Length..];
            using var input = System.IO.File.OpenRead(path);
            using var expanded = path.EndsWith(".gz", StringComparison.Ordinal) ? new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress) : null;
            var oracle = JsonNode.Parse(expanded ?? (System.IO.Stream)input)!.AsObject();
            var materials = WorldMaterialsManifest.Load();
            var environment = EnvironmentCatalog.Load();
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().ToDictionary(level => level["id"]!.GetValue<string>());
            foreach (var field in oracle["fields"]!.AsArray().OfType<JsonObject>())
            {
                var id = field["id"]!.GetValue<string>();
                using var cache = new CandidateShadowCache();
                var terrain = new TerrainView();
                AddChild(terrain);
                try
                {
                    terrain.Configure(materials, TerrainChecks.Scene(levels[id]), environment, cache);
                    terrain.Render(field["tiles"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray(), new(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>()));
                    var actual = terrain.GetNode<Node2D>("ComposedTerrain/Ground").GetChildren().OfType<Sprite2D>().Where(sprite => sprite.HasMeta("route_atlas")).ToArray();
                    var expected = field["connectors"]!.AsArray().OfType<JsonObject>().ToArray();
                    Check(actual.Length == expected.Length, $"{id} connector count {actual.Length}/{expected.Length}");
                    for (var index = 0; index < expected.Length; index++)
                    {
                        var sprite = actual[index]; var source = expected[index];
                        Check(sprite.GetMeta("route_cell_x").AsInt32() * 32 == source["x"]!.GetValue<int>() && sprite.GetMeta("route_cell_y").AsInt32() * 32 == source["y"]!.GetValue<int>(), $"{id}/{index} source row-major route/edge position");
                        var markup = XElement.Parse(source["markup"]!.GetValue<string>());
                        var atlas = sprite.GetMeta("route_atlas").AsString();
                        var spec = environment.ResolveAtlas(atlas).Spec;
                        var image = markup.Descendants("image").Last();
                        var href = image.Attribute("href")!.Value;
                        var expectedFile = materials.ConnectorMasks.TryGetValue(atlas, out var maskSpec) ? System.IO.Path.GetFileName(maskSpec.ResourcePath) : $"{atlas}@2x.png";
                        Check(href.EndsWith($"/{expectedFile}", StringComparison.Ordinal), $"{id}/{index} source route/edge atlas {atlas}: {href}");
                        var x = double.Parse(image.Attribute("x")!.Value, System.Globalization.CultureInfo.InvariantCulture);
                        var y = double.Parse(image.Attribute("y")!.Value, System.Globalization.CultureInfo.InvariantCulture);
                        var cellIndex = (int)(-y / 32) * spec.Columns + (int)(-x / 32);
                        Check(sprite.GetMeta("route_index").AsInt32() == cellIndex, $"{id}/{index} source mask/variant index");
                        Check(sprite.HasMeta("route_filter_scope") || sprite.GetChildren().OfType<CandidateSpriteShadows>().Any(child => child.GetMeta("shadow_kind").AsString() == "Route"), $"{id}/{index} filter applied to individual cell before composition");
                    }
                    Check(!terrain.Diagnostics.Any(message => message.StartsWith("Connected terrain", StringComparison.Ordinal)), $"{id} no silently skipped route cells");
                    GD.Print($"Connector source/native {id}: {actual.Length} ordered cells.");
                }
                finally { RemoveChild(terrain); terrain.Free(); }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var state = JsonNode.Parse("""{"players":[],"structures":[],"markers":[{"id":1,"kind":"fallen_corpse","owner":1,"at":{"x":2,"y":3}}]}""")!.AsObject();
            var markerView = new DynamicStateView(AssetCatalog.Load(), PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var markers = markerView.Create(state);
            try
            {
                var sprite = markers.Root.GetNode<Sprite2D>("Marker_0/AuthoredMarker");
                Check(Math.Abs(sprite.Modulate.A - BoardDecorationDocument.Load().MarkerOpacity) < .00001, "Authored marker source opacity is applied after composition");
                Check(sprite.TextureFilter == CanvasItem.TextureFilterEnum.Linear, "Authored marker source image-rendering auto");
                Check(markers.Diagnostics.Count == 0, "Marker check must exercise actual authored asset, not fallback");
            }
            finally { markers.Root.Free(); }
            GD.Print($"Connector decoration checks passed: {assertions} assertions across all 16 original fields; no browser pixel acceptance implied.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
    }
}
