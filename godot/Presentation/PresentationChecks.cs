#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Empire.Core.Engine;

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

    private static void CheckBodyPicking(BoardView board, JsonObject state, int villageIndex)
    {
        var unit = board.GetNode<Node2D>("CanonicalProjection/Unit_2");
        var sprite = unit.GetNode<Sprite2D>("AuthoredSprite");
        var expected = new Empire.Core.Geometry.Coord(9, 12);
        var bodyHit = false;
        for (var y = -35; y < -8 && !bodyHit; y++)
            for (var x = -18; x <= 18 && !bodyHit; x++)
            {
                var point = unit.Position + new Vector2(x, y);
                if (sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(point))) && board.Pick(point) == expected)
                    bodyHit = true;
            }
        Require(bodyHit, "An opaque visible unit body must select its canonical cell above its ground footprint.");
        var occupied = state.DeepClone().AsObject();
        var width = state["map"]!["width"]!.GetValue<int>();
        var cell = new Empire.Core.Geometry.Coord(villageIndex % width, villageIndex / width);
        var moved = occupied["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == 2);
        moved["x"] = cell.X;
        moved["y"] = cell.Y;
        board.RenderState(occupied);
        unit = board.GetNode<Node2D>("CanonicalProjection/Unit_2");
        sprite = unit.GetNode<Sprite2D>("AuthoredSprite");
        var house = board.GetNode<Node2D>($"CanonicalProjection/MapStructure_{villageIndex}");
        var houseSprite = house.GetNode<Sprite2D>("AuthoredSprite");
        Require(house.ZIndex > unit.ZIndex, "Fixture must put the house foundation in front of its occupant.");
        var overlap = false;
        for (var y = -35; y < 0; y++)
            for (var x = -18; x <= 18; x++)
            {
                var point = unit.Position + new Vector2(x, y);
                var global = board.ToGlobal(point);
                if (!sprite.IsPixelOpaque(sprite.ToLocal(global)) || !houseSprite.IsPixelOpaque(houseSprite.ToLocal(global)))
                    continue;
                overlap = true;
                Require(board.Pick(point) is null, "Opaque foreground architecture must not select its hidden occupant through the shared cell.");
            }
        Require(overlap, "The occlusion regression must exercise actual overlapping opaque pixels.");
        board.RenderState(state);
    }

    private void Check()
    {
        CheckDepthBands();
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
        var house = assets.ResolveMapStructure("village");
        var houseSprite = assets.CreateSprite(house, "normal");
        try
        {
            Require(SpriteGroundContact.Offset(houseSprite).IsEqualApprox(
                (house.GroundFront - house.Anchor) * house.PresentationScale),
                "Foreground depth contact must be distinct from the house placement anchor.");
            Require(SpriteGroundContact.Offset(houseSprite).Y > 8,
                "House foundation must sort after its threshold, not be clipped by flat ground.");
        }
        finally { houseSprite.Free(); }
        var fence = environment.ResolveCell("farm-fence-straight");
        var fenceSprite = environment.CreateSprite(fence.Id, 0.75f, true);
        try
        {
            var expected = new Vector2(fence.CellWidth - fence.GroundFront.X, fence.GroundFront.Y)
                - fence.Anchor;
            Require(SpriteGroundContact.Offset(fenceSprite).IsEqualApprox(expected * fence.PresentationScale * 0.75f),
                "Reflected fence foreground contact must use frame-local reflection and placement scale exactly once.");
        }
        finally { fenceSprite.Free(); }
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

        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var state = engine.CreateBattle(level).Snapshot();
        var before = state.ToJsonString();
        var board = new BoardView();
        AddChild(board);
        board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog), level, scene);
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
        Require(unit.Position.IsEqualApprox(board.Projection.CellContact(new Empire.Core.Geometry.Coord(9, 12))),
            "Actor contact must sample the shared terrain surface at CELL_FOOT.");
        Require(!board.SurfaceReady,
            "Incomplete compositor must never be described as the complete scene.");
        var villageIndex = Enumerable.Range(0, state["map"]!["tiles"]!.AsArray().Count)
            .First(index => state["map"]!["tiles"]![index]!.GetValue<string>() == "village");
        CheckBodyPicking(board, state, villageIndex);
        var ownedVillage = state.DeepClone().AsObject();
        ownedVillage["map"]!["owners"]![villageIndex] = 1;
        board.RenderState(ownedVillage);
        var village = board.GetNode<Node2D>($"CanonicalProjection/MapStructure_{villageIndex}");
        Require(village.HasNode("OwnerBanner"), "Canonical ownership must produce its separate map banner.");
        Require(((AtlasTexture)village.GetNode<Sprite2D>("AuthoredSprite").Texture).Region.Position == Vector2.Zero,
            "Ownership must not demand a duplicated captured building frame.");
        board.RenderState(state);
        var captureTransition = new BattlePresentationTransition(1,
            Empire.Core.Determinism.EcmaJson.ToElement(state),
            Empire.Core.Determinism.EcmaJson.ToElement(new JsonObject { ["kind"] = "capture" }),
            new[] { Empire.Core.Determinism.EcmaJson.ToElement(new JsonObject { ["type"] = "capture", ["captured"] = true }) },
            Empire.Core.Determinism.EcmaJson.ToElement(ownedVillage), false);
        var capturePlayback = board.PresentTransitionAsync(captureTransition);
        Require(capturePlayback.IsCompletedSuccessfully && capturePlayback.Result,
            "Capture playback must update the owner banner without requesting a baked owner frame.");
        Require(board.GetNode<Node2D>($"CanonicalProjection/MapStructure_{villageIndex}").HasNode("OwnerBanner"),
            "Capture playback must preserve the declared structure image and show canonical ownership.");
        board.RenderState(state);
        Require(board.GetChildCount() == 2, "Repeated projection must not duplicate visual layers.");
        var stableSurface = board.Projection;
        var changed = (JsonObject)state.DeepClone();
        changed["units"]!.AsArray()[1]!["x"] = 6;
        changed["units"]!.AsArray()[1]!["hp"] = 98.0d;
        changed["units"]!.AsArray()[1]!["facing"] = "south";
        board.RenderState(changed);
        Require(board.GetNode<Sprite2D>("CanonicalProjection/Unit_2/AuthoredSprite").Scale.X < 0,
            "Settled south-facing actors must preserve the projected down-left direction after movement.");
        Require(ReferenceEquals(stableSurface, board.Projection), "Unit-only changes must retain the terrain projection and viewport cache.");
        Require(board.GetNode<Node2D>("CanonicalProjection/Unit_2").Position.IsEqualApprox(
            board.Projection.CellContact(new(6, changed["units"]!.AsArray()[1]!["y"]!.GetValue<int>()))),
            "New canonical position must replace, not append to, the old projection.");
        Require(state.ToJsonString() == before, "Rendering another projection must leave the original intact.");
        changed["units"]!.AsArray()[1]!["key"] = null;
        changed["units"]!.AsArray()[1]!["type"] = "not-exported-unit";
        board.RenderState(changed);
        Require(board.RenderedUnitCount == 18 && board.ProjectionUnitCount == 19
            && board.Diagnostics.Any(message => message.Contains("not-exported-unit", StringComparison.Ordinal))
            && board.RenderingIssueCount > 0,
            "Unavailable art must be explicitly diagnosed without substituting another actor.");
        board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog), level, scene);
        board.RenderState(state);
        Require(board.GetChildCount() == 2, "Repeated configuration must not duplicate scene layers.");
        CheckTerrain(board, state);
        board.QueueFree();
    }

    private static void CheckDepthBands()
    {
        var projection = TerrainChecks.FlatProjection(["plain"], [0], new Vector2I(1, 1));
        Require(projection.FacingVector("south").X < 0 && projection.FacingVector("south").Y > 0
            && projection.FacingVector("east").X > 0 && projection.FacingVector("east").Y > 0,
            "Grid directions must use the same dimetric basis as movement and attack contacts.");
        projection.PrepareDepthPartitions([20f], Array.Empty<float>());
        var source = projection.Triangles.Single(triangle => triangle.LogicalB.Y == 32 && triangle.LogicalC.Y == 32);
        var patches = projection.DepthPatches.Where(patch => patch.Source == source).ToArray();
        var area = 0f;
        foreach (var patch in patches)
        {
            var partition = projection.DepthPartitions.Single(value => value.StartDepth == patch.Depth);
            foreach (var vertex in patch.Vertices)
            {
                Require(projection.Depth(vertex.Logical) >= patch.Depth - 0.0001f && projection.Depth(vertex.Logical) <= partition.EndDepthExclusive + 0.0001f,
                    "A ground partition must not paint across a required contact boundary.");
                Require(vertex.Projected.IsEqualApprox(source.ProjectedA * vertex.SourceWeights.X
                    + source.ProjectedB * vertex.SourceWeights.Y + source.ProjectedC * vertex.SourceWeights.Z),
                    "Depth bands must interpolate the original surface, not invent another projection.");
            }
            for (var index = 1; index + 1 < patch.Vertices.Count; index++)
                area += Math.Abs((patch.Vertices[index].Logical - patch.Vertices[0].Logical)
                    .Cross(patch.Vertices[index + 1].Logical - patch.Vertices[0].Logical)) / 2;
            var groundDepth = projection.LayerAtDepth(patch.Depth, BoardDepthLayer.Surface);
            var actorDepth = projection.LayerAtDepth(20, BoardDepthLayer.Standing);
            Require(patch.Depth <= 20 ? groundDepth < actorDepth : groundDepth > actorDepth,
                "Ground behind a contact must stay behind; genuine foreground ground must still occlude.");
        }
        Require(Mathf.IsEqualApprox(area, 256), "Depth clipping must preserve the full source triangle area.");
        projection.PrepareDepthPartitions([20f], Array.Empty<float>());
        Require(projection.DepthPartitions.Count < 8, "An injective single cell must batch empty depth space rather than create one node per pixel.");
        Require(projection.DepthPartitions.All(partition => !(partition.StartDepth < 21 && partition.EndDepthExclusive > 21)),
            "No combined terrain partition may cross a standing foreground boundary.");
        var cliffMap = JsonNode.Parse("""
            {"width":1,"height":2,"tiles":["plain","plain"],"elevation":[1,0],
             "cliffs":[{"from":{"x":0,"y":0},"to":{"x":0,"y":1}}]}
            """)!.AsObject();
        var cliff = new BoardProjection(new Empire.Core.Geometry.Square4Grid(), cliffMap,
            new System.Collections.Generic.HashSet<Empire.Core.Geometry.Coord> { new(0, 0), new(0, 1) });
        Require(cliff.Faces.Count > 0, "An actual canonical cliff must expose its camera-facing connection.");
        foreach (var face in cliff.Faces)
            Require(cliff.Pick((face.ProjectedA + face.ProjectedB + face.ProjectedC + face.ProjectedD) / 4)?.Cell is null,
                "A visible cliff face must not pick a hidden top cell behind it.");
    }

    private static void CheckTerrain(BoardView board, JsonObject state)
    {
        var width = state["map"]!["width"]!.GetValue<int>();
        var height = state["map"]!["height"]!.GetValue<int>();
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
        var ground = terrain.GetNode<Node2D>("ComposedTerrain/ProjectedGround/LogicalGround/ComposedTerrain/Ground");
        var field = ground.GetNode<Sprite2D>("WorldField");
        using var fieldMask = field.Texture.GetImage();
        var tiles = state["map"]!["tiles"]!.AsArray();
        for (var index = 0; index < tiles.Count; index++)
            Require((fieldMask.GetPixel(index % width * 64 + 32, index / width * 64 + 32).A > .99)
                == (tiles[index]!.GetValue<string>() != "c01.outside"),
                "Field coverage must derive from canonical playable cells, not a second outline.");
        var road = ground.GetNode<Sprite2D>("RoadCorridor");
        using var roadMask = road.Texture.GetImage();
        Require(roadMask.GetWidth() == width * 64 && roadMask.GetHeight() == height * 64,
            "Road technical mask must preserve the logical map extent at density two.");
        foreach (var cell in AuthoredScene.Load("res://data/scenes/c01-01.json").RoadCorridor!.ProtectedCells)
            Require(roadMask.GetPixel(cell[0] * 64 + 32, cell[1] * 64 + 32).A == 0,
                "Reviewed protected road notch must remain unpainted.");
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index]!.GetValue<string>() != "road")
                continue;
            Require(roadMask.GetPixel(index % width * 64 + 32, index / width * 64 + 32).A > 0.99,
                $"Road center must have actual native coverage at {index % width},{index / width}.");
        }
        var forest = ground.GetNode<Sprite2D>("ForestFloor");
        using var forestMask = forest.Texture.GetImage();
        var firstForest = Enumerable.Range(0, tiles.Count).First(index => tiles[index]!.GetValue<string>() == "forest");
        Require(forestMask.GetPixel(firstForest % width * 64 + 32, firstForest / width * 64 + 32).A > .99,
            "Forest alpha must reach a canonical forest cell center.");
        var cachedLayer = terrain.GetChild(0);
        board.RenderState(state);
        Require(terrain.GetChildCount() == 1 && terrain.GetChild(0) == cachedLayer,
            "Unchanged canonical terrain must retain one native composition, not append duplicate layers.");
        var raised = state.DeepClone().AsObject();
        var elevation = raised["map"]!["elevation"]!.AsArray();
        var changedCell = Enumerable.Range(0, tiles.Count).First(index =>
            tiles[index]!.GetValue<string>() == "plain" && elevation[index]!.GetValue<int>() == 0);
        elevation[changedCell] = 1;
        var raisedBefore = raised.ToJsonString();
        var contactBefore = board.GetNode<Node2D>("CanonicalProjection/Unit_2").Position;
        board.RenderState(raised);
        Require(terrain.GetChild(0) != cachedLayer,
            "An elevation-only change must invalidate the terrain composition cache.");
        var changedCoord = new Empire.Core.Geometry.Coord(changedCell % width, changedCell / width);
        var raisedCenter = board.Projection.CellCenter(changedCoord);
        Require(board.Projection.Pick(raisedCenter)?.Cell == changedCoord,
            "Raised surface center and inverse picking must refer to the same canonical cell.");
        Require(raised.ToJsonString() == raisedBefore,
            "Height projection must never advance canonical state.");
        board.RenderState(state);
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
