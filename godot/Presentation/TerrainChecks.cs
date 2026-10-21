#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

/// <summary>Native resource/alpha regression checks. Test projections never dispatch battle actions.</summary>
internal static class TerrainChecks
{
    internal static AuthoredScene Scene(JsonObject level)
    {
        var id = level["id"]!.GetValue<string>();
        var path = $"res://data/scenes/{id}.json";
        return Godot.FileAccess.FileExists(path) ? AuthoredScene.Load(path) : new AuthoredScene
        {
            LevelId = id,
            MapSize = [level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>()],
            RuntimeReady = false,
            Title = "No recovered authored scene; canonical terrain projection only."
        };
    }

    internal static void Run(Node parent)
    {
        var materials = WorldMaterialsManifest.Load();
        var environment = EnvironmentCatalog.Load();
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var riverChecked = false;
        foreach (var level in levels.OfType<JsonObject>())
        {
            var state = engine.CreateState(level);
            var unchanged = state.ToJsonString();
            var tiles = state["map"]!["tiles"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
            var size = new Vector2I(level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>());
            var scene = Scene(level);
            var view = new TerrainView();
            parent.AddChild(view);
            try
            {
                view.Configure(materials, scene, environment);
                view.Render(tiles, size);
                Require(!view.Diagnostics.Any(message => message.Contains("Missing", StringComparison.Ordinal)),
                    $"{scene.LevelId}: {string.Join("; ", view.Diagnostics)}");
                Require(state.ToJsonString() == unchanged, "Terrain composition changed the canonical state.");
                if (scene.LevelId == "c01-02")
                {
                    CheckRiver(view, materials, scene, tiles, size);
                    riverChecked = true;
                }
                GD.Print($"Terrain resource projection: {scene.LevelId}, {view.RenderedSceneryCount} standing props, {view.Diagnostics.Count} coverage diagnostics.");
            }
            finally { parent.RemoveChild(view); view.Free(); }
        }
        Require(riverChecked, "The canonical c01-02 river fixture must be tested.");
        CheckOccupancy(parent, materials, environment);
        CheckCrossings(materials, environment);
        CheckRiverGeometry();
        CheckEdgeSupport(materials);
        GD.Print("Terrain checks passed: 16 canonical resource projections, c01-02 river permissions/shape/bridge registration, raw diagonal occupancy, river width validation and junction topology.");
    }

    private static void CheckRiver(TerrainView view, WorldMaterialsManifest materials, AuthoredScene scene, string[] tiles, Vector2I size)
    {
        var ground = view.GetNode<Node2D>("ComposedTerrain/Ground");
        using var river = ground.GetNode<Sprite2D>("RiverWater").Texture.GetImage();
        using var bank = ground.GetNode<Sprite2D>("RiverBank").Texture.GetImage();
        using var shape = ground.GetNode<Sprite2D>("WorldField").Texture.GetImage();
        Require(river.GetWidth() == size.X * 64 && river.GetHeight() == size.Y * 64, "River mask must preserve full density-two extent.");
        var water = materials.Materials["water-material"].Replaces;
        var road = materials.Materials["road-material"].Replaces;
        bool Matches(string tile, MaterialReplacement replacement) => materials.TerrainBindings[tile].Connected is { } binding
            && binding.Atlas == replacement.Atlas && binding.Mask == replacement.Mask;
        bool Settlement(string tile) => materials.TerrainTags[tile].Intersect(materials.GroundTagFamilies["settlement"]).Any();
        RequireAlphaOnly(river, size, index => Matches(tiles[index], water), "River painted a dry canonical cell.");
        RequireAlphaOnly(bank, size, index => Matches(tiles[index], water) || tiles[index] == "plain"
            || Matches(tiles[index], road) && !Settlement(tiles[index]), "River bank painted forbidden terrain.");
        RequireAlphaOnly(shape, size, index => !materials.TerrainTags[tiles[index]].Intersect(materials.GroundTagFamilies["outside"]).Any(),
            "Playable shape painted an outside cell.");
        Require(river.GetData().Where((_, index) => index % 4 == 3).Any(alpha => alpha == 255), "Authored river has no actual opaque coverage.");
        Require(view.RenderedEdgeDecorationCount == 17, "All 17 terrain-supported c01-02 source overlays must render.");
        var surface = ground.GetNode<Sprite2D>("Surface_meadow-stony");
        Require(surface.Material is ShaderMaterial clipped && clipped.GetShaderParameter("has_playable_shape").AsBool(),
            "Irregular boundary must clip hill/ground materials, not just the field underneath.");
        foreach (var edge in view.GetNode<Node2D>("ComposedTerrain/EdgeDecorations").GetChildren().OfType<Sprite2D>())
            Require(edge.HasMeta("ground_clipped"), "Authored edge overlays must share the playable ground silhouette.");
        var bridges = view.GetNode<Node2D>("ComposedTerrain/Crossings");
        Require(bridges.GetChildCount() > 0, "The c01-02 bridge must use a native source sprite.");
        foreach (var bridge in bridges.GetChildren().OfType<Sprite2D>())
        {
            if (bridge.GetMeta("axis").AsString() != "horizontal")
                continue;
            var source = materials.Crossings!.Horizontal;
            Require((bridge.Texture.GetSize() * bridge.Scale).IsEqualApprox(new Vector2((float)source.SourceSize[0], (float)source.SourceSize[1])
                * (float)source.LogicalPerSourcePixel), "Registered crossing must preserve its full original canvas, not stretch by run length.");
            Require((bridge.Offset * bridge.Scale).IsEqualApprox(-new Vector2((float)source.SourceFoot[0], (float)source.SourceFoot[1])
                * (float)source.LogicalPerSourcePixel), "Bridge registration foot changed.");
        }
        Require(scene.River?.Widths?.Distinct().Count() > 1, "c01-02 must exercise its authored narrow bridge throat.");
    }

    private static void RequireAlphaOnly(Image image, Vector2I size, Func<int, bool> allowed, string message)
    {
        image.Convert(Image.Format.Rgba8);
        var pixels = image.GetData();
        for (var y = 0; y < size.Y * 64; y++)
            for (var x = 0; x < size.X * 64; x++)
                if (!allowed(y / 64 * size.X + x / 64) && pixels[(y * size.X * 64 + x) * 4 + 3] != 0)
                    throw new InvalidOperationException($"{message} Pixel {x},{y}.");
    }

    private static void CheckOccupancy(Node parent, WorldMaterialsManifest materials, EnvironmentCatalog environment)
    {
        var replacement = materials.Materials["water-material"].Replaces;
        var waterId = materials.TerrainBindings.First(pair => pair.Value.Connected is { } connection
            && connection.Atlas == replacement.Atlas && connection.Mask == replacement.Mask).Key;
        var tiles = Enumerable.Repeat("plain", 9).ToArray();
        tiles[4] = waterId;
        tiles[2] = waterId; // NE-only neighbor: raw mask 2, deliberately not canonical Blob47 mask 0.
        var view = new TerrainView();
        parent.AddChild(view);
        try
        {
            view.Configure(materials, new AuthoredScene { LevelId = "alpha-contract", MapSize = [3, 3] }, environment);
            view.Render(tiles, new Vector2I(3, 3));
            using var actual = view.GetNode<Sprite2D>("ComposedTerrain/Ground/AreaWater").Texture.GetImage();
            using var source = ResourceLoader.Load<Texture2D>(materials.Materials["water-material"].Occupancy!.ResourcePath).GetImage();
            if (source.IsCompressed())
                Require(source.Decompress() == Error.Ok, "Occupancy PNG decompression failed.");
            actual.Convert(Image.Format.Rgba8);
            source.Convert(Image.Format.Rgba8);
            using var center = actual.GetRegion(new Rect2I(64, 64, 64, 64));
            using var expected = source.GetRegion(new Rect2I(2 * 64, 0, 64, 64));
            Require(center.GetData().SequenceEqual(expected.GetData()), "Water must preserve all eight raw neighbor bits and original source alpha.");
            RequireAlphaOnly(actual, new Vector2I(3, 3), index => tiles[index] == waterId, "Occupancy water escaped water cells.");
        }
        finally { parent.RemoveChild(view); view.Free(); }
    }

    private static void CheckCrossings(WorldMaterialsManifest materials, EnvironmentCatalog environment)
    {
        bool Bridge(int x, int y) => (x == 2 && y >= 1 && y <= 3) || (y == 2 && x >= 1 && x <= 3);
        bool Route(int x, int y) => (x == 2 && (y == 0 || y == 4)) || (y == 2 && (x == 0 || x == 4));
        var topology = CrossingRenderer.Topology(5, 5, Bridge, Route);
        Require(topology.Count == 1 && topology[0].Runs.Count == 2 && CrossingRenderer.Junction(topology[0]) == new Vector2(80, 80),
            "Five-cell source plus crossing must have two complete runs about one common pivot.");
        var parent = new Node2D();
        try
        {
            var source = materials.Crossings!;
            var forceRegisteredStone = new CrossingManifest
            {
                Horizontal = source.Horizontal,
                Vertical = source.Vertical,
                Junction = source.Junction,
                VerticalLegacyIds = ["stone-bridge-vertical"]
            };
            var diagnostics = new List<string>();
            CrossingRenderer.Add(parent, forceRegisteredStone, environment, path => ResourceLoader.Load<Texture2D>(path), topology, diagnostics);
            Require(diagnostics.Count == 0 && parent.GetChildCount() == 1 && parent.GetChild(0).GetChildCount() == 2,
                "Registered plus crossing must compose its two original sources with no resource fallback.");
            foreach (var sprite in parent.GetChild(0).GetChildren().OfType<Sprite2D>())
                Require(sprite.Material is ShaderMaterial shader && shader.GetShaderParameter("first_cut").VariantType == Variant.Type.Vector4,
                    "Each plus crossing axis needs its source-local alpha openings.");
        }
        finally { parent.Free(); }
    }

    private static void CheckEdgeSupport(WorldMaterialsManifest materials)
    {
        var manifest = materials.EdgeDecorations ?? throw new InvalidOperationException("Edge decoration dependency absent.");
        var scene = manifest.Scenes.Single(scene => scene.LevelId == "c01-02" && scene.MapSize.SequenceEqual(new[] { 36, 24 }));
        var placement = scene.Placements.First();
        var tiles = Enumerable.Repeat("not-the-authored-terrain", 36 * 24).ToArray();
        var root = new Node2D();
        try
        {
            var requested = false;
            var count = EdgeDecorationRenderer.Add(root, manifest, scene.LevelId, new Vector2I(36, 24), tiles,
                path => { requested = true; return ResourceLoader.Load<Texture2D>(path); }, new List<string>());
            Require(count == 0 && !requested, "Changed local terrain must disable authored overlays before loading art.");
            for (var y = 0; y < placement.TerrainSupport.Rows.Length; y++)
                for (var x = 0; x < placement.TerrainSupport.Rows[y].Length; x++)
                    tiles[(placement.TerrainSupport.Origin[1] + y) * 36 + placement.TerrainSupport.Origin[0] + x] = placement.TerrainSupport.Rows[y][x];
            count = EdgeDecorationRenderer.Add(root, manifest, scene.LevelId, new Vector2I(36, 24), tiles,
                path => ResourceLoader.Load<Texture2D>(path), new List<string>());
            Require(count > 0 && root.HasNode(placement.Id), "An exactly supported source overlay must render.");
            var sprite = root.GetNode<Sprite2D>(placement.Id);
            var asset = manifest.Assets.Single(asset => asset.Id == placement.AssetId);
            var sign = new Vector2(placement.FlipX ? -1 : 1, placement.FlipY ? -1 : 1);
            var anchor = (RasterResources.Pair(asset.SourceAnchor, asset.Id) + Vector2.One * asset.SourcePadding)
                * (float)(asset.LogicalPerSourcePixel * placement.Scale);
            Require((sprite.Offset * sprite.Scale).IsEqualApprox(-anchor * sign),
                "Source padding/anchor and transform-local flips must survive native registration.");
        }
        finally { root.Free(); }
    }

    private static void CheckRiverGeometry()
    {
        var profile = new RiverProfile { Width = 48, BankWidth = 58, Knots = [[1.5, 0], [1.5, 2]], Widths = [48, 24] };
        using var mask = WaterMasks.River(3, 2, profile, (_, _) => true, (_, _) => true, false);
        Require(mask.GetPixel(96, 64).A > .99f && mask.GetPixel(0, 64).A == 0, "Authored variable river ribbon lost its center/edge.");
        try
        {
            WaterMasks.ValidateRiver(new RiverProfile { Width = 48, BankWidth = 58, Knots = [[1, 1], [1, 0]] });
            throw new InvalidOperationException("Upstream/reversed river knots were accepted.");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("River requires", StringComparison.Ordinal)) { }
    }
    private static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
