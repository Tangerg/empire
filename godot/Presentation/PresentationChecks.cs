#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

/// <summary>Executable native-resource checks; run only through the dedicated test scene.</summary>
public partial class PresentationChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Check();
            TerrainChecks.Run(this);
            GD.Print("Presentation resource contracts passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check()
    {
        var assets = AssetCatalog.Load();
        var generic = assets.ResolveUnit("c01.swordsman");
        var torren = assets.ResolveUnit("c01.swordsman", "campaign-torren");
        Require(torren.Identity == "torren-stage01", "Actor key must select Torren's own declared sheet.");
        Require(torren.TopicId == generic.TopicId && torren.Footprint == generic.Footprint,
            "Named sheet must preserve canonical identity/footprint.");
        Require(torren.FrameWidth != generic.FrameWidth, "Fixture must exercise independent actor canvas width.");
        Throws<InvalidOperationException>(() => assets.ResolveUnit("c01.archer", "campaign-torren"),
            "Mismatched actor type must be rejected.");

        var unitSprite = assets.CreateSprite(torren);
        try
        {
            var frame = unitSprite.Texture as AtlasTexture
                ?? throw new InvalidOperationException("Sprite must use a native AtlasTexture.");
            Require(frame.Region.Size.IsEqualApprox(new Vector2(
                torren.FrameWidth * torren.PixelDensity, torren.FrameHeight * torren.PixelDensity)),
                "Atlas frame must be measured in physical pixels.");
            Require((unitSprite.Offset * unitSprite.Scale).IsEqualApprox(-torren.Anchor * torren.PresentationScale),
                "Logical anchor must be preserved through density scaling.");
            Require(unitSprite.Modulate == Colors.White, "Actor pixels must not receive a team recolor.");
            Require((unitSprite.Texture.GetSize() * unitSprite.Scale).IsEqualApprox(
                new Vector2(torren.FrameWidth, torren.FrameHeight) * torren.PresentationScale),
                "Density must not double the visual canvas.");
        }
        finally { unitSprite.Free(); }
        Require(AssetCatalog.FrameIndex(torren, "walk-b") == 3
            && AssetCatalog.FrameIndex(torren, "attack") == 2,
            "Animation roles must use declared ordering.");
        var attack = assets.CreateFrameTexture(torren, AssetCatalog.FrameIndex(torren, "attack"));
        Require(Mathf.IsEqualApprox(attack.Region.Position.X, 2 * torren.FrameWidth * torren.PixelDensity),
            "Attack frame must crop the correct atlas region.");
        Throws<InvalidOperationException>(() => AssetCatalog.FrameIndex(torren, "nonexistent"),
            "Undeclared animation role must not fall back to idle.");
        const string missingPath = "res://assets/not-exported-contract-check.png";
        try
        {
            assets.CreateFrameTexture(torren with
            {
                ResourcePath = missingPath
            }, 0);
            throw new InvalidOperationException("Missing physical art must not produce a placeholder texture.");
        }
        catch (MissingArtResourceException error)
        {
            Require(error.Resource == missingPath, "Missing resource diagnostics must retain the exact path.");
        }

        var environment = EnvironmentCatalog.Load();
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var placement = environment.ResolveCell("temperate-hill-cap");
        var propSprite = environment.CreateSprite(placement.Id, 0.82f, true);
        try
        {
            Require(propSprite.FlipH, "Environment flip must remain frame-local.");
            Require((propSprite.Offset * propSprite.Scale).IsEqualApprox(
                -placement.Anchor * placement.PresentationScale * 0.82f),
                "Authored environment scale must be applied exactly once.");
        }
        finally { propSprite.Free(); }

        var fixture = PresentationJson.Load<JsonObject>("res://tests/oracle/grayflag-initial-raw.json");
        var level = fixture["level"] as JsonObject ?? throw new InvalidOperationException("Fixture level absent.");
        var state = fixture["state"] as JsonObject ?? throw new InvalidOperationException("Fixture state absent.");
        var before = state.ToJsonString();
        var board = new BoardView();
        AddChild(board);
        board.Configure(new(assets, environment, facing => facing == "west", null), level, scene);
        board.RenderState(state);
        Require(state.ToJsonString() == before, "Rendering must not mutate canonical state.");
        Require(board.RenderedUnitCount == board.ProjectionUnitCount && board.ProjectionUnitCount == 19,
            "All GrayFlag actors must resolve to real resources.");
        Require(board.RenderedAuthoredPlacementCount == scene.Placements.Count + scene.FramePlacements.Count,
            "Every authored placement must resolve to a real resource.");
        Require(!board.Diagnostics.Any(message => message.Contains("Missing", StringComparison.Ordinal)
            || message.Contains("not included", StringComparison.Ordinal)),
            "The current fixture must not silently omit selected art.");
        var unit = board.GetNode<Node2D>("CanonicalProjection/Unit_2");
        Require(unit.GetMeta("actor_key").AsString() == "campaign-torren",
            "Canonical actor key must survive projection.");
        Require(unit.Position.IsEqualApprox(new Vector2(5 * 32 + 16, 8 * 32 + 31)),
            "Actor contact must preserve CELL_FOOT.");
        Require(!board.SurfaceReady,
            "Incomplete compositor must never be described as the complete scene.");
        board.RenderState(state);
        Require(board.GetChildCount() == 2, "Repeated projection must not duplicate visual layers.");
        var changed = (JsonObject)state.DeepClone();
        changed["units"]!.AsArray()[1]!["x"] = 6;
        changed["units"]!.AsArray()[1]!["hp"] = 98.0d;
        board.RenderState(changed);
        Require(board.GetNode<Node2D>("CanonicalProjection/Unit_2").Position.X == 6 * 32 + 16,
            "New canonical position must replace, not append to, the old projection.");
        Require(state.ToJsonString() == before, "Rendering another projection must leave the original intact.");
        changed["units"]!.AsArray()[1]!["key"] = null;
        changed["units"]!.AsArray()[1]!["type"] = "not-exported-unit";
        board.RenderState(changed);
        Require(board.RenderedUnitCount == 18 && board.ProjectionUnitCount == 19
            && board.Diagnostics.Any(message => message.Contains("not-exported-unit", StringComparison.Ordinal))
            && board.RenderingIssueCount > 0,
            "Unavailable art must be explicitly diagnosed without substituting another actor.");
        board.Configure(new(assets, environment, facing => facing == "west", null), level, scene);
        board.RenderState(state);
        Require(board.GetChildCount() == 2, "Repeated configuration must not duplicate scene layers.");
        CheckTerrain(board, state);
        board.QueueFree();
    }

    private static void CheckTerrain(BoardView board, JsonObject state)
    {
        var materials = WorldMaterialsManifest.Load();
        Require(Enumerable.Range(0, 256).Select(TerrainView.CanonicalBlobMask).Distinct().Count() == 47,
            "Canonical diagonal suppression must produce exactly Blob47.");
        board.ConfigureTerrain(materials);
        board.RenderState(state);
        var terrain = board.GetNode<TerrainView>("AuthoredScene/Terrain");
        Require(terrain.RenderedSceneryCount > 0, "Terrain must use deterministic resource-backed scenery.");
        Require(!terrain.Diagnostics.Any(message => message.Contains("Missing", StringComparison.Ordinal)),
            string.Join("\n", terrain.Diagnostics));
        Require(board.SurfaceReady,
            "Player-facing status must retain scene warnings without developer acceptance prose.");
        Require(board.Diagnostics.Any(message => message.Contains("runtimeReady=false", StringComparison.Ordinal))
            && board.RenderingIssueCount == board.Diagnostics.Count(message => !message.Contains("runtimeReady=false", StringComparison.Ordinal)),
            "Source art-review metadata must remain in diagnostics without leaking into player-facing status.");
        var ground = terrain.GetNode<Node2D>("ComposedTerrain/Ground");
        var field = ground.GetNode<Polygon2D>("WorldField");
        Require(field.Texture is not null && field.Polygon.Length == AuthoredScene.Load("res://data/scenes/c01-01.json").GroundOutline.Length,
            "World field must use the authored polygon and original texture.");
        var road = ground.GetNode<Sprite2D>("RoadCorridor");
        using var roadMask = road.Texture.GetImage();
        Require(roadMask.GetWidth() == 27 * 64 && roadMask.GetHeight() == 17 * 64,
            "Road technical mask must preserve the logical map extent at density two.");
        foreach (var cell in AuthoredScene.Load("res://data/scenes/c01-01.json").RoadCorridor!.ProtectedCells)
            Require(roadMask.GetPixel(cell[0] * 64 + 32, cell[1] * 64 + 32).A == 0,
                "Reviewed protected road notch must remain unpainted.");
        var tiles = state["map"]!["tiles"]!.AsArray();
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index]!.GetValue<string>() != "road")
                continue;
            Require(roadMask.GetPixel(index % 27 * 64 + 32, index / 27 * 64 + 32).A > 0.99,
                $"Road center must have actual native coverage at {index % 27},{index / 27}.");
        }
        var forest = ground.GetNode<Sprite2D>("ForestFloor");
        using var forestMask = forest.Texture.GetImage();
        Require(forestMask.GetPixel(32, 32).A > 0.99, "Original forest alpha must reach the first forest cell center.");
        var cachedLayer = terrain.GetChild(0);
        board.RenderState(state);
        Require(terrain.GetChildCount() == 1 && terrain.GetChild(0) == cachedLayer,
            "Unchanged canonical terrain must retain one native composition, not append duplicate layers.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
