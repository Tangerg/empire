#nullable enable
using System;
using Empire.Presentation.Tactical;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public partial class PaintedSurfaceChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            CheckOpeningScene();
            await Check();
            GD.Print("Painted elevated surface registration, depth and picking passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckOpeningScene()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel();
        var state = engine.CreateState(level);
        TerrainChecks.CheckOpeningHills(engine, state);
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
        var board = new BoardView();
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.RenderState(state);
            Require(board.RenderedUnitCount == 19 && board.RenderedStructureCount == 0,
                "The opening must render all 19 actors without duplicating the four authored buildings.");
            foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
            {
                var cell = new Coord(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
                var actor = board.GetNode<Node2D>($"CanonicalProjection/Unit_{unit["id"]!.GetValue<int>()}");
                Require(actor.Position.IsEqualApprox(board.Projection.CellContact(cell)),
                    "Every opening actor must use the canonical elevated foot contact.");
            }
            var ground = board.GetNode<AssembledGroundView>("AuthoredScene/AssembledGround");
            Require(ground.Sites.Select(site => site.Cell).ToHashSet().SetEquals(
                [new Coord(3, 16), new Coord(18, 13), new Coord(22, 16), new Coord(34, 13)]),
                "The opening must retain all four canonical building sites.");
            foreach (var site in ground.Sites)
            {
                var sprite = ground.GetNode<Sprite2D>(site.Id);
                // A transparent doorway anchor is not a visible building hit target.
                var body = new Vector2(0, -100);
                Require(sprite.IsPixelOpaque(body), $"The doorway-body probe must cover actual building pixels: {site.Id}.");
                Require(board.Pick(board.ToLocal(sprite.ToGlobal(body))) is { Kind: TacticalHitKind.Object, UnitId: null } hit
                    && hit.Cell == new BoardCell(site.Cell.X, site.Cell.Y),
                    $"Visible building pixels must select their canonical site, never infer a same-cell occupant: {site.Id}.");
            }
            var shield = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "vanguard-1");
            shield["x"] = 22;
            shield["y"] = 16;
            shield["facing"] = "west";
            board.RenderState(state);
            var projection = board.GetNode<Node2D>("CanonicalProjection");
            var shieldId = shield["id"]!.GetValue<int>();
            var shieldActor = projection.GetNode<Node2D>($"Unit_{shieldId}");
            var shieldSprite = shieldActor.GetNode<Sprite2D>("AuthoredSprite");
            var granary = ground.GetNode<Sprite2D>(ground.Sites.Single(site => site.Cell == new Coord(22, 16)).Id);
            Require(shieldActor.ZIndex == granary.ZIndex && projection.GetIndex() > board.GetNode("AuthoredScene").GetIndex(),
                "Doorway regression must exercise equal Z with the actor subtree drawn later.");
            var overlap = board.Projection.CellContact(new(22, 16)) + new Vector2(0, -19);
            var globalOverlap = board.ToGlobal(overlap);
            Require(shieldSprite.IsPixelOpaque(shieldSprite.ToLocal(globalOverlap)) && granary.IsPixelOpaque(granary.ToLocal(globalOverlap)),
                "Doorway regression must overlap real opaque actor and building pixels.");
            Require(board.Pick(overlap) == TacticalHit.Unit(new(22, 16), shieldId),
                "At equal depth, the later-drawn visible actor must receive the doorway click.");
            board.MoveChild(projection, 0);
            Require(board.Pick(overlap) == TacticalHit.Object(new(22, 16)),
                "Reversing the native tree order must make the now-frontmost house receive the same click.");
            board.MoveChild(projection, board.GetChildCount() - 1);
            Require(board.Pick(overlap) == TacticalHit.Unit(new(22, 16), shieldId),
                "Restoring the actor subtree's native draw order must restore its visible click target.");
        }
        finally
        {
            RemoveChild(board);
            board.Free();
        }
        foreach (var hp in new int?[] { null, 50, 98 })
        {
            var source = level.DeepClone().AsObject();
            var torren = source["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "campaign-torren");
            if (hp is null)
                torren.Remove("hp");
            else
                torren["hp"] = hp.Value;
            var request = new JsonObject { ["levelId"] = "c01-01", ["level"] = source };
            var context = new JsonObject { ["resources"] = new JsonObject(), ["flags"] = new JsonArray("twin_hills_rushed") };
            var rushed = Empire.Core.Campaign.GrayFlagCampaignPolicy.ApplyBattleContext(request, context)["level"]!.AsObject();
            var result = rushed["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "campaign-torren");
            Require(result["x"]!.GetValue<int>() == 15 && result["y"]!.GetValue<int>() == 7
                && result["hp"]!.GetValue<double>() == Math.Min(hp ?? 98, 72),
                "Rush must relocate Torren onto the real north plateau without healing existing wounds.");
            TerrainChecks.CheckOpeningHills(engine, engine.CreateState(rushed));
        }
        GD.Print("Native opening registration, entrances, two-hill traversal, actor feet and rush HP policy passed.");
    }

    private async Task Check()
    {
        const int width = 6, height = 5;
        var tiles = Enumerable.Repeat("plain", width * height).ToArray();
        var heights = new int[tiles.Length];
        foreach (var y in new[] { 1, 2 })
            foreach (var x in new[] { 2, 3, 4 })
                heights[y * width + x] = 2;
        heights[3 * width + 2] = 1;
        var map = new JsonObject
        {
            ["width"] = width,
            ["height"] = height,
            ["tiles"] = new JsonArray(tiles.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["elevation"] = new JsonArray(heights.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["cliffs"] = new JsonArray(new JsonArray(3, 4).Select(node => (JsonNode?)new JsonObject
            {
                ["from"] = new JsonObject { ["x"] = node!.GetValue<int>(), ["y"] = 2 },
                ["to"] = new JsonObject { ["x"] = node.GetValue<int>(), ["y"] = 3 }
            }).ToArray())
        };
        var art = new PaintedGroundDocument
        {
            LevelId = "elevated-registration-check",
            LevelSourceSha256 = "check-only",
            ResourcePath = "check-only",
            PixelSize = [640, 480],
            WorldScale = .5f,
            OriginPixels = [96, 128],
            CellXPixels = [64, 0],
            CellYPixels = [0, 48],
            Occluders = []
        };
        var playable = Enumerable.Range(0, tiles.Length).Select(index => new Coord(index % width, index / width)).ToHashSet();
        var projection = new BoardProjection(new Square4Grid(), art.CreatePlane, map, playable);
        foreach (var cell in new[] { new Coord(2, 4), new Coord(2, 3), new Coord(2, 2), new Coord(3, 2) })
            Require(projection.Pick(projection.CellCenter(cell))?.Cell == cell, $"Visible ramp/platform center and reverse pick disagree: {cell}.");
        var face = projection.Faces.First();
        Require(projection.Pick((face.ProjectedA + face.ProjectedB + face.ProjectedC + face.ProjectedD) / 4)?.Cell is null,
            "A cliff face cannot become walkable ground.");
        var path = new[] { new Coord(2, 4), new Coord(2, 3), new Coord(2, 2) };
        Require(path.Select(cell => heights[cell.Y * width + cell.X]).SequenceEqual(new[] { 0, 1, 2 }),
            "Guide must contain a real canonical ramp, not a painted height illusion.");

        string Points(params Vector2[] points) => string.Join(" ", points.Select(point => FormattableString.Invariant($"{point.X * 2},{point.Y * 2}")));
        var polygons = string.Join("", projection.Triangles.Select(triangle =>
            $"<polygon points='{Points(triangle.ProjectedA, triangle.ProjectedB, triangle.ProjectedC)}' fill='#91b56b' stroke='#44653e'/>"))
            + string.Join("", projection.Faces.Select(item => $"<polygon points='{Points(item.ProjectedA, item.ProjectedB, item.ProjectedC, item.ProjectedD)}' fill='#9a7955' stroke='#55422c'/>"));
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='640' height='480'><rect width='640' height='480' fill='#edf0e9'/>{polygons}</svg>";
        var guide = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "empire-painted-elevation-registration-guide.svg");
        System.IO.File.WriteAllText(guide, svg);
        using var image = new Image();
        Require(image.LoadSvgFromString(svg) == Error.Ok, "Registration guide must rasterize.");
        using var texture = ImageTexture.CreateFromImage(image);
        var content = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json")).Catalog;
        CheckCoveredCliff(art, map, texture, face);
        var view = PaintedGroundView.Create(art, texture, map, content);
        AddChild(view);
        try
        {
            view.Render(projection, tiles, heights, new(width, height));
            projection.PrepareDepthPartitions(path.Select(cell => projection.Depth(projection.LogicalCellContact(cell))), []);
            var strips = view.GetNode<Node2D>("RegisteredSurface").GetChildren().OfType<Polygon2D>().ToArray();
            Require(strips.Length > 1, "Elevated pixels must participate in the shared depth partitions.");
            foreach (var strip in strips)
                Require(strip.Polygon.Zip(strip.UV).All(pair => pair.First.IsEqualApprox(pair.Second * art.WorldScale)),
                    "Source pixels must stay at their registered screen coordinates without a second warp.");
            var changed = map.DeepClone().AsObject();
            changed["cliffs"] = new JsonArray();
            Reject(() => view.Render(new BoardProjection(new Square4Grid(), art.CreatePlane, changed, playable), tiles, heights, new(width, height)),
                "Cliff registration drift must fail.");
            Reject(() => view.Render(new BoardProjection(new Square4Grid(), (size, min, max) =>
                new BoardPlane(new(1, .05f), new(0, .75f), new(48, 64), new(320, 240)), map, playable), tiles, heights, new(width, height)),
                "A different basis must not pass merely because origin and canvas match.");
            GD.Print($"Registration guide: {guide}");
        }
        finally
        {
            RemoveChild(view);
            view.Free();
        }
        Require(GodotObject.IsInstanceValid(texture), "Releasing painted geometry must not dispose the borrowed source.");
        projection.PrepareDepthPartitions([15], []);
        if (OS.GetCmdlineUserArgs().Contains("--preview"))
            await Preview(art, map, texture);
    }

    private void CheckCoveredCliff(PaintedGroundDocument registration, JsonObject map, Texture2D texture, BoardSurfaceFace face)
    {
        // Own this geometric witness rather than depending on evolving character padding.
        using var actorImage = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        actorImage.Fill(Colors.White);
        using var actorTexture = ImageTexture.CreateFromImage(actorImage);
        actorTexture.TakeOverPath("res://assets/check-fixtures/cliff-cue-actor.tres");
        var actors = new AssetCatalog(new AssetManifest
        {
            Schema = 2,
            LogicalTileSize = 32,
            Assets = [new AssetRecord { TopicId = "cliff-cue-actor", Category = "combat-unit",
                ResourcePath = actorTexture.ResourcePath, Width = 16, Height = 32,
                PhysicalWidth = 16, PhysicalHeight = 32, PixelDensity = 1,
                Anchor = [8, 32], GroundFront = [8, 32], Footprint = [1, 1], FrameOrder = ["idle"] }]
        }, new ArtBindings
        {
            Units = new()
            {
                ["c01.laiya"] = new()
                {
                    TopicId = "cliff-cue-actor"
                },
                ["c01.swordsman"] = new()
                {
                    TopicId = "cliff-cue-actor"
                }
            }
        });
        var point = (face.ProjectedA + face.ProjectedB + face.ProjectedC + face.ProjectedD) / 4;
        var source = point / registration.WorldScale;
        var art = new PaintedGroundDocument
        {
            LevelId = registration.LevelId,
            LevelSourceSha256 = registration.LevelSourceSha256,
            ResourcePath = registration.ResourcePath,
            PixelSize = registration.PixelSize,
            WorldScale = registration.WorldScale,
            OriginPixels = registration.OriginPixels,
            CellXPixels = registration.CellXPixels,
            CellYPixels = registration.CellYPixels,
            Occluders = [new PaintedOccluder { Id = "CliffRoof", Parts = [new PaintedOccluderPart
            {
                Id = "Material", ContactCell = [3, 3], GroundFrontPixels = [source.X, source.Y + 60],
                PolygonPixels = [[source.X - 8, source.Y - 8], [source.X + 8, source.Y - 8], [source.X + 8, source.Y + 8], [source.X - 8, source.Y + 8]],
                Pointer = new PaintedPointer { Kind = "pass-through" }
            }] }]
        };
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel().DeepClone().AsObject();
        level["id"] = art.LevelId;
        level["width"] = map["width"]!.DeepClone();
        level["height"] = map["height"]!.DeepClone();
        level["terrain"] = new JsonArray("......", "......", "......", "......", "......");
        level["elevation"] = map["elevation"]!.DeepClone();
        level["cliffs"] = map["cliffs"]!.DeepClone();
        level["owners"] = new JsonArray();
        level["commanders"] = new JsonArray();
        level["scenario"] = new JsonObject();
        level["units"] = new JsonArray(
            new JsonObject { ["key"] = "campaign-laiya", ["x"] = 0, ["y"] = 4, ["unit"] = "c01.laiya", ["owner"] = 1 },
            new JsonObject { ["key"] = "cliff-check-enemy", ["x"] = 5, ["y"] = 0, ["unit"] = "c01.swordsman", ["owner"] = 2 });
        level["structures"] = new JsonArray();
        var state = engine.CreateState(level);
        var fixtureMap = state["map"]!.AsObject();
        var board = new BoardView();
        AddChild(board);
        try
        {
            board.Configure(new(actors, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level,
                new AuthoredScene { LevelId = art.LevelId, MapSize = [6, 5], RuntimeReady = true, FramePlacements = [] });
            board.ConfigureGround((_, _) => PaintedGroundView.Create(art, texture, fixtureMap, engine.Catalog));
            board.RenderState(state);
            var material = board.GetNode<PaintedGroundView>("AuthoredScene/PaintedGround").DepthEntries.Single();
            Require(material.Visual!.Covers(board.ToGlobal(point)) && material.Node.ZIndex > board.Projection.Pick(point)!.DrawDepth,
                "The layered cliff regression must place actual pass-through material in front of a visible canonical cliff face.");
            Require(board.Projection.Pick(point) is { Cell: null } && board.Pick(point) is null,
                "Pass-through foreground must not create ground interaction through an actual cliff face.");
            var actor = state["units"]![0]!.AsObject();
            actor["x"] = 3;
            actor["y"] = 4;
            var actorFoot = board.Projection.CellContact(new(3, 4)) / art.WorldScale;
            art.Occluders[0] = new PaintedOccluder
            {
                Id = "CliffAndActorRoof",
                Parts = [art.Occluders[0].Parts[0], new PaintedOccluderPart
                {
                    Id = "ActorCover", ContactCell = [3, 4], GroundFrontPixels = [actorFoot.X, actorFoot.Y + 20],
                    PolygonPixels = [[actorFoot.X - 50, actorFoot.Y - 100], [actorFoot.X + 50, actorFoot.Y - 100],
                        [actorFoot.X + 50, actorFoot.Y + 10], [actorFoot.X - 50, actorFoot.Y + 10]],
                    Pointer = new PaintedPointer { Kind = "pass-through" }
                }]
            };
            board.ConfigureGround((_, _) => PaintedGroundView.Create(art, texture, fixtureMap, engine.Catalog));
            board.RenderState(state);
            var id = actor["id"]!.GetValue<int>();
            var sprite = board.GetNode<Sprite2D>($"CanonicalProjection/Unit_{id}/AuthoredSprite");
            var rect = sprite.GetRect();
            var preferred = board.ToLocal(sprite.ToGlobal(new(rect.GetCenter().X, rect.Position.Y))) + new Vector2(0, -14);
            var cue = board.GetNodeOrNull<Polygon2D>($"CanonicalProjection/OccludedUnitCues/UnitCue_{id}");
            Require(cue is not null, "The cliff fixture must contain a genuinely occluded actor with a visible cue.");
            var faces = board.Projection.Faces.Select(item => new[] { item.ProjectedA, item.ProjectedB, item.ProjectedC, item.ProjectedD }).ToArray();
            Require(faces.Any(polygon => Geometry2D.IntersectPolygons(polygon, cue!.Polygon.Select(pixel => pixel + preferred).ToArray()).Count > 0),
                "A real cliff must overlap the preferred cue location so the exclusion guard has a geometric witness.");
            var displayed = cue!.Polygon.Select(pixel => board.ToLocal(cue.ToGlobal(pixel))).ToArray();
            Require(faces.All(polygon => Geometry2D.IntersectPolygons(polygon, displayed).Count == 0)
                && board.Pick(board.ToLocal(cue.ToGlobal(Vector2.Zero))) == TacticalHit.Unit(new(3, 4), id),
                "The cue must relocate clear of every projected cliff while retaining its own visible unit target.");
            Require(board.Projection.Pick(point) is { Cell: null } && board.Pick(point) is null,
                "The original cliff face remains a blocker after the occluded actor obtains a cue.");
        }
        finally
        {
            RemoveChild(board);
            board.Free();
        }
    }

    private async Task Preview(PaintedGroundDocument art, JsonObject map, Texture2D texture)
    {
        Require(DisplayServer.GetName() != "headless", "Preview requires a real native window.");
        var level = BattleTextChecks.OpeningLevel();
        level["id"] = art.LevelId;
        level["width"] = map["width"]!.DeepClone();
        level["height"] = map["height"]!.DeepClone();
        level["terrain"] = new JsonArray("......", "......", "......", "......", "......");
        level["elevation"] = map["elevation"]!.DeepClone();
        level["cliffs"] = map["cliffs"]!.DeepClone();
        level["owners"] = new JsonArray();
        level["commanders"] = new JsonArray();
        level["scenario"] = new JsonObject();
        level["units"] = new JsonArray(
            new JsonObject { ["key"] = "campaign-laiya", ["x"] = 2, ["y"] = 4, ["unit"] = "c01.laiya", ["owner"] = 1 },
            new JsonObject { ["key"] = "preview-enemy", ["x"] = 5, ["y"] = 0, ["unit"] = "c01.swordsman", ["owner"] = 2 });
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(level);
        var initial = battle.Snapshot();
        var board = new BoardView { Position = new(80, 60), Scale = new(3, 3), AdvanceAnimationsAutomatically = false };
        AddChild(board);
        board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane),
            level, new AuthoredScene { LevelId = art.LevelId, MapSize = [6, 5], OpeningFocus = [2, 3], RuntimeReady = true, FramePlacements = [] });
        board.ConfigureGround((_, _) => PaintedGroundView.Create(art, texture, initial["map"]!.AsObject(), engine.Catalog));
        board.RenderState(initial);
        var face = board.Projection.Faces.First();
        var point = (face.ProjectedA + face.ProjectedB + face.ProjectedC + face.ProjectedD) / 4;
        var depth = board.Projection.LayerAtDepth(Mathf.FloorToInt(face.Depth), BoardDepthLayer.Surface);
        var marker = new ColorRect
        {
            Position = point - Vector2.One * 2,
            Size = new(4, 4),
            Color = Colors.Magenta,
            ZAsRelative = false,
            ZIndex = depth - 1
        };
        board.AddChild(marker);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var pixel = board.ToGlobal(point);
        using (var behind = GetViewport().GetTexture().GetImage())
            Require(behind.GetPixel((int)pixel.X, (int)pixel.Y) != Colors.Magenta, "A lower-depth marker must be occluded by painted cliff pixels.");
        marker.ZIndex = depth + 1;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (var front = GetViewport().GetTexture().GetImage())
            Require(front.GetPixel((int)pixel.X, (int)pixel.Y).IsEqualApprox(Colors.Magenta), "The same marker must be visible in front of the cliff.");
        marker.Free();
        GD.Print("Native painted cliff pixel occlusion passed in both depth orders.");
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        var id = initial["units"]![0]!["id"]!.GetValue<int>();
        var action = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = id,
            ["path"] = new JsonArray(new JsonObject { ["x"] = 2, ["y"] = 4 }, new JsonObject { ["x"] = 2, ["y"] = 3 }, new JsonObject { ["x"] = 2, ["y"] = 2 }),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        };
        var receipt = battle.Dispatch(action);
        var transition = new BattlePresentationTransition(1, EcmaJson.ToElement(receipt.Before), EcmaJson.ToElement(action),
            receipt.Events.Select(EcmaJson.ToElement).ToArray(), EcmaJson.ToElement(battle.Snapshot()), false);
        var movement = board.PresentTransitionAsync(transition);
        for (var tick = 0; tick < 30 && !movement.IsCompleted; tick++)
        {
            board.AdvancePresentation(.01);
            await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        }
        Require(movement.IsCompleted && await movement, "Native slope movement must finish.");
        Require(board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}").Position.IsEqualApprox(board.Projection.CellContact(new(2, 2))),
            "Moved actor must settle at its canonical elevated foot contact.");
        GD.Print("Native canonical slope command and actor contact passed.");
        await ToSignal(GetTree().CreateTimer(20), SceneTreeTimer.SignalName.Timeout);
        RemoveChild(board);
        board.Free();
    }

    private static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void Reject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
}
