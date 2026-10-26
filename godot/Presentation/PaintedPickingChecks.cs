#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Native coverage and input regressions. All positioned actors are detached fixtures.</summary>
internal static class PaintedPickingChecks
{
    internal static void CheckAdmission(Action<bool, string> require)
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = Redstone();
        var map = engine.CreateState(level)["map"]!.AsObject();
        var source = PresentationJson.Load<JsonObject>("res://data/scenes/c01-03-painted-ground.json");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var original = JsonSerializer.Deserialize<PaintedGroundDocument>(source.ToJsonString(), options)!;
        var texture = ResourceLoader.Load<Texture2D>(original.ResourcePath);
        JsonObject Object(JsonObject document) => document["occluders"]!.AsArray().OfType<JsonObject>()
            .Single(item => item["id"]!.GetValue<string>() == "SouthHouse");
        JsonObject Part(JsonObject document) => Object(document)["parts"]![0]!.AsObject();
        JsonArray Points(params (int X, int Y)[] points) => new(points.Select(point => (JsonNode?)new JsonArray(point.X, point.Y)).ToArray());
        void Reject(string label, Action<JsonObject> mutate)
        {
            var copy = source.DeepClone().AsObject();
            mutate(copy);
            var rejected = false;
            try
            {
                var document = JsonSerializer.Deserialize<PaintedGroundDocument>(copy.ToJsonString(), options)!;
                PaintedGroundView.Create(document, texture, map, engine.Catalog).Free();
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
            {
                rejected = true;
            }
            require(rejected && GodotObject.IsInstanceValid(texture), "Painted admission must reject " + label + " without disposing the borrowed texture.");
        }
        Reject("a missing pointer declaration", document => Part(document).Remove("pointer"));
        Reject("an unknown pointer kind", document => Part(document)["pointer"]!["kind"] = "automatic");
        Reject("an ignored clickPolygon field", document => Part(document)["clickPolygon"] = Points((1, 1), (2, 1), (1, 2)));
        Reject("an unknown document field", document => document["clickThroughRoofs"] = true);
        Reject("a target without a pointer polygon", document => Part(document)["pointer"]!.AsObject().Remove("polygonPixels"));
        Reject("a pass-through declaration with a polygon", document => Part(document)["pointer"]!["kind"] = "pass-through");
        Reject("a target without a logical site", document => Object(document).Remove("selectionCell"));
        Reject("a non-site binding", document => Object(document)["selectionCell"] = new JsonArray(54, 35));
        Reject("an out-of-map contact", document => Part(document)["contactCell"] = new JsonArray(-1, 35));
        Reject("an object with no material parts", document => Object(document)["parts"] = new JsonArray());
        Reject("duplicate material identities", document => Object(document)["parts"]!.AsArray().Add(Part(document).DeepClone()));
        Reject("duplicate logical object identities", document => document["occluders"]!.AsArray().Add(Object(document).DeepClone()));
        Reject("two logical owners for one site", document =>
        {
            var second = Object(document).DeepClone().AsObject();
            second["id"] = "SecondSouthHouse";
            document["occluders"]!.AsArray().Add(second);
        });
        Reject("a crossed draw polygon", document => Part(document)["polygonPixels"] = Points((10, 10), (50, 50), (10, 50), (50, 10)));
        Reject("a crossed pointer polygon", document => Part(document)["pointer"]!["polygonPixels"] = Points((850, 700), (920, 715), (850, 715), (920, 700)));
        Reject("duplicate pointer vertices", document => Part(document)["pointer"]!["polygonPixels"] = Points((850, 700), (920, 700), (920, 700), (850, 715)));
        Reject("an out-of-canvas pointer", document => Part(document)["pointer"]!["polygonPixels"] = Points((-1, 700), (920, 700), (850, 715)));
        Reject("a pointer outside its drawable material", document => Part(document)["pointer"]!["polygonPixels"] = Points((10, 10), (20, 10), (20, 20), (10, 20)));
        Reject("pointer edges crossing a concave material gap despite contained vertices", document =>
        {
            Part(document)["polygonPixels"] = Points((10, 10), (70, 10), (70, 70), (50, 70), (50, 30), (30, 30), (30, 70), (10, 70));
            Part(document)["pointer"]!["polygonPixels"] = Points((20, 60), (60, 60), (40, 20));
        });
    }

    internal static void CheckOpenMouth(Node host, Action<bool, string> require)
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel().DeepClone().AsObject();
        level["width"] = 7;
        level["height"] = 7;
        var state = engine.CreateState(BattleTextChecks.OpeningLevel());
        var map = state["map"]!.AsObject();
        map["width"] = 7;
        map["height"] = 7;
        map["tiles"] = new JsonArray(Enumerable.Range(0, 49).Select(index => (JsonNode?)JsonValue.Create(index == 24 ? "village" : "plain")).ToArray());
        foreach (var key in new[] { "owners", "elevation", "captureProgress" })
            map[key] = new JsonArray(Enumerable.Range(0, 49).Select(_ => (JsonNode?)JsonValue.Create(0)).ToArray());
        map["owners"]![24] = 1;
        map["cliffs"] = new JsonArray();
        map["directionalCover"] = new JsonArray();
        state["structures"] = new JsonArray();
        var actor = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == 2).DeepClone().AsObject();
        actor["x"] = 3;
        actor["y"] = 3;
        state["units"] = new JsonArray(actor);
        PaintedOccluderPart Material(string id, double left, double top, double right, double bottom, double front, bool target) => new()
        {
            Id = id,
            ContactCell = [3, 3],
            GroundFrontPixels = [112, front],
            PolygonPixels = [[left, top], [right, top], [right, bottom], [left, bottom]],
            Pointer = target
                ? new PaintedPointer { Kind = "target", PolygonPixels = [[left + 1, top + 1], [right - 1, top + 1], [right - 1, bottom - 1], [left + 1, bottom - 1]] }
                : new PaintedPointer { Kind = "pass-through" }
        };
        var art = new PaintedGroundDocument
        {
            LevelId = level["id"]!.GetValue<string>(),
            LevelSourceSha256 = "detached-material-fixture",
            ResourcePath = "borrowed-fixture",
            PixelSize = [224, 224],
            WorldScale = 1,
            OriginPixels = [0, 0],
            CellXPixels = [32, 0],
            CellYPixels = [0, 32],
            Occluders = [new PaintedOccluder
            {
                Id = "OpenCanopy", SelectionCell = [3, 3], OwnsMapStructureBody = true,
                Parts = [Material("Roof", 66, 32, 158, 58, 58, false), Material("LeftPost", 74, 58, 86, 132, 132, true),
                    Material("RightPost", 138, 58, 150, 132, 132, true)]
            }]
        };
        using var image = Image.CreateEmpty(224, 224, false, Image.Format.Rgb8);
        image.Fill(Color.FromHtml("#79543d"));
        using var texture = ImageTexture.CreateFromImage(image);
        var board = new BoardView();
        host.AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane),
                level, new AuthoredScene { LevelId = art.LevelId, MapSize = [7, 7], RuntimeReady = true, FramePlacements = [] });
            board.ConfigureGround((_, _) => PaintedGroundView.Create(art, texture, map, engine.Catalog));
            board.RenderState(state);
            var ground = Descendants(board).OfType<PaintedGroundView>().Single();
            require(ground.DepthEntries.Count == 3 && ground.Sites.Count == 1 && ground.Sites[0].Cell == new Coord(3, 3)
                && ground.Sites[0].OwnsMapStructureBody, "Three canopy materials must expose exactly one logical body/site binding.");
            require(!Descendants(board).Any(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)),
                "A multipart painted site must suppress its one redundant map structure, not nominate a first-part owner.");
            Node[] Banners() => Descendants(board).Where(node => node.HasMeta("owner_color")).ToArray();
            require(Banners().Length == 1, "A multipart owned site must create exactly one owner banner.");
            var beforeColor = Banners()[0].GetMeta("owner_color").AsString();
            var bodyPoints = OpaqueActorPoints(board, 2).ToArray();
            require(bodyPoints.Length > 100 && bodyPoints.All(point => ground.DepthEntries.All(entry => !entry.Visual!.Covers(board.ToGlobal(point)))),
                "Every opaque actor-body pixel in the open mouth, not only its feet, must be outside foreground material coverage.");
            var missedBodyPoints = bodyPoints.Where(point => board.Pick(point) != TacticalHit.Unit(new(3, 3), 2)).ToArray();
            require(!Descendants(board).Any(node => node.Name == "UnitCue_2"), "A fully exposed actor must not acquire an occlusion cue.");
            var actorNode = board.GetNode<Node2D>("CanonicalProjection/Unit_2");
            var actorSprite = actorNode.GetNode<Sprite2D>("AuthoredSprite");
            require(missedBodyPoints.Length == 0,
                "The entire visible body through an open mouth must remain a Unit hit; no floor-sampling shell may repaint it. "
                + $"Missed {missedBodyPoints.Length}/{bodyPoints.Length}; actor position={actorNode.Position}, Z={actorNode.ZIndex}, "
                + $"sprite position={actorSprite.Position}, scale={actorSprite.Scale}, front={SpriteGroundContact.Offset(actorSprite)}, visible={actorSprite.IsVisibleInTree()}; "
                + string.Join("; ", missedBodyPoints.Take(6).Select(point => $"point={point}, source={actorSprite.ToLocal(board.ToGlobal(point))}, hit={board.Pick(point)}, surface={board.Projection.Pick(point)}, groundZ={string.Join(",", Descendants(ground).OfType<PaintedSurface>().Select(surface => surface.HitPolygon(board.ToGlobal(point))?.ZIndex))}")));
            foreach (var point in new[] { new Vector2(100, 120), new Vector2(112, 125), new Vector2(128, 120) })
                require(ground.DepthEntries.All(entry => !entry.Visual!.Covers(board.ToGlobal(point))), "Open floor and between-post negative space must not be redrawn as foreground.");
            require(ground.DepthEntries.Select(entry => entry.Node.ZIndex).Distinct().Count() == 2,
                "Roof and front posts must retain their separately authored depths, not one arbitrary object front.");
            foreach (var point in new[] { new Vector2(80, 90), new Vector2(144, 90) })
                require(board.Pick(point) == TacticalHit.Object(new(3, 3)), "Both independently targeted posts must resolve to the same logical entrance.");
            actor["x"] = 2;
            board.RenderState(state);
            var covered = OpaqueActorPoints(board, 2).Where(point => point.X > 75 && point.X < 85 && point.Y > 59 && point.Y < 131).ToArray();
            require(covered.Length > 0 && covered.All(point => board.Pick(point) == TacticalHit.Object(new(3, 3))),
                "Real opaque foreground post pixels must hide a rear actor without selecting its occupied ground cell.");
            actor["y"] = 4;
            board.RenderState(state);
            var inFront = OpaqueActorPoints(board, 2).Where(point => point.X > 75 && point.X < 85 && point.Y > 59 && point.Y < 131).ToArray();
            require(inFront.Length > 0 && inFront.All(point => board.Pick(point) == TacticalHit.Unit(new(2, 4), 2)),
                "An actor grounded in front of the same post must win by the existing draw depth, without a global actor-Z override.");
            map["owners"]![24] = 2;
            board.RenderState(state);
            require(ground.Sites.Count == 1 && Banners().Length == 1 && Banners()[0].GetMeta("owner_color").AsString() != beforeColor,
                "A canonical owner change must update exactly one banner despite several material targets.");

            actor["x"] = 3;
            actor["y"] = 3;
            var canopy = art.Occluders[0];
            void Materials(params PaintedOccluderPart[] parts)
            {
                art.Occluders[0] = new PaintedOccluder
                {
                    Id = canopy.Id,
                    SelectionCell = canopy.SelectionCell,
                    OwnsMapStructureBody = true,
                    Parts = parts
                };
                board.ConfigureGround((_, _) => PaintedGroundView.Create(art, texture, map, engine.Catalog));
                board.RenderState(state);
            }
            var cueRoof = Material("CueRoof", 64, 48, 160, 132, 164, false);
            foreach (var owner in new[] { 1, 2 })
            {
                actor["owner"] = owner;
                Materials(cueRoof, Material("CuePost", 138, 58, 150, 132, 180, true));
                var cue = board.GetNode<Polygon2D>("CanonicalProjection/OccludedUnitCues/UnitCue_2");
                var center = board.ToLocal(cue.ToGlobal(Vector2.Zero));
                require(cue.IsVisibleInTree() && board.Pick(center) == TacticalHit.Unit(new(3, 3), 2),
                    "A visibly rendered cue must select either an allied or enemy render-visible occluded actor.");
                require(cue.Color == Color.FromHtml(state["players"]!.AsArray().OfType<JsonObject>()
                    .Single(player => player["id"]!.GetValue<int>() == owner)["color"]!.GetValue<string>()),
                    "The cue must carry the actor's actual owner color, not an allied-only identity.");
                require(OpaqueActorPoints(board, 2).All(point => board.Pick(point)?.Kind != TacticalHitKind.Unit),
                    "A cue must not promote the original covered actor pixels or arbitrary roof pixels into Unit hits.");
                require(board.Pick(new(144, 90)) == TacticalHit.Object(new(3, 3)),
                    "The independently opaque facade post remains its structural target with a cue present.");
                require(board.Pick(center + new Vector2(10, 10)) != TacticalHit.Unit(new(3, 3), 2),
                    "Transparent corners of the cue bounds must not become an invisible rectangular target.");
                board.Position = new(37, 29);
                board.Scale = new(1.6f, .85f);
                board.Rotation = .12f;
                board.RenderState(state);
                cue = board.GetNode<Polygon2D>("CanonicalProjection/OccludedUnitCues/UnitCue_2");
                require(board.Pick(board.ToLocal(cue.ToGlobal(Vector2.Zero))) == TacticalHit.Unit(new(3, 3), 2),
                    "Cue drawing and picking must share projected transforms under pan, zoom and rotation.");
                board.Position = Vector2.Zero;
                board.Scale = Vector2.One;
                board.Rotation = 0;
                board.RenderState(state, new HashSet<int>());
                require(!Descendants(board).Any(node => node.Name == "UnitCue_2"),
                    "Fog-filtered actors must produce neither body nor identity cue.");
                actor["x"] = 1;
                actor["y"] = 5;
                board.RenderState(state);
                require(!Descendants(board).Any(node => node.Name == "UnitCue_2"),
                    "Moving into open ground must remove the old cue instead of retaining a stale target.");
                actor["x"] = 3;
                actor["y"] = 3;
            }
            Materials(cueRoof, Material("CuePost", 138, 58, 150, 132, 180, true));
            var oldCue = board.GetNode<Polygon2D>("CanonicalProjection/OccludedUnitCues/UnitCue_2");
            var oldCueCenter = oldCue.Position;
            var oldCuePolygon = oldCue.Polygon.Select(oldCue.ToGlobal).ToArray();
            var surfaceStructure = engine.CreateState(Redstone())["structures"]![0]!.DeepClone().AsObject();
            var oldCueCell = board.Projection.Pick(oldCueCenter)!.Cell!.Value;
            surfaceStructure["x"] = oldCueCell.X;
            surfaceStructure["y"] = oldCueCell.Y;
            state["structures"] = new JsonArray(surfaceStructure);
            board.ConfigureDynamicState();
            board.RenderState(state);
            var flatBody = Descendants(board).OfType<PaintedSurface>().First(surface => surface.GetParent().HasMeta("structure_id"));
            var surfacePolygons = flatBody.PointerPolygons(board).ToArray();
            var surfaceBounds = StandingVisual.BoundsOf(surfacePolygons.SelectMany(polygon => polygon).ToArray());
            require(surfacePolygons.Any(polygon => Geometry2D.IntersectPolygons(polygon, oldCuePolygon).Count > 0),
                "The actual flattened surface footprint must overlap the formerly preferred cue, so conservative placement must relocate it.");
            require(Enumerable.Range((int)surfaceBounds.Position.Y, (int)surfaceBounds.Size.Y + 1).Any(y =>
                Enumerable.Range((int)surfaceBounds.Position.X, (int)surfaceBounds.Size.X + 1).Any(x => flatBody.HitPolygon(new(x, y)) is not null)),
                "The flattened structure fixture must contain genuine opaque pointer pixels, not a transparent placeholder.");
            var movedCue = board.GetNode<Polygon2D>("CanonicalProjection/OccludedUnitCues/UnitCue_2");
            var cuePolygon = movedCue.Polygon.Select(movedCue.ToGlobal).ToArray();
            require(flatBody.PointerPolygons(board).All(polygon => Geometry2D.IntersectPolygons(polygon, cuePolygon).Count == 0)
                && board.Pick(movedCue.Position) == TacticalHit.Unit(new(3, 3), 2),
                "The visible cue must move clear of flattened structure pointer geometry, not cover its target.");
            var hiddenParent = new Node2D { Visible = false };
            host.AddChild(hiddenParent);
            board.Reparent(hiddenParent);
            board.RenderState(state);
            flatBody = Descendants(board).OfType<PaintedSurface>().First(surface => surface.GetParent().HasMeta("structure_id"));
            movedCue = board.GetNode<Polygon2D>("CanonicalProjection/OccludedUnitCues/UnitCue_2");
            cuePolygon = movedCue.Polygon.Select(movedCue.ToGlobal).ToArray();
            require(flatBody.PointerPolygons(board).Count() == surfacePolygons.Length
                && flatBody.PointerPolygons(board).All(polygon => Geometry2D.IntersectPolygons(polygon, cuePolygon).Count == 0),
                "Hidden-parent preparation must retain flattened structural blockers and place the cue clear of their footprint.");
            hiddenParent.Visible = true;
            require(board.Pick(movedCue.Position) == TacticalHit.Unit(new(3, 3), 2),
                "Revealing a prepared board must expose the same visible selectable cue without a second render.");
            board.Reparent(host);
            hiddenParent.Free();
            state["structures"] = new JsonArray();
            var savedActors = state["units"]!.DeepClone();
            state["units"] = new JsonArray(Enumerable.Range(0, 4).Select(index =>
            {
                var nearby = actor.DeepClone().AsObject();
                nearby["id"] = 91 + index;
                nearby["key"] = $"cue-crowd-{index}";
                nearby["x"] = 1 + index % 2;
                nearby["y"] = 1 + index / 2;
                return (JsonNode)nearby;
            }).ToArray());
            Materials(Material("EdgeCanopy", 0, 0, 132, 132, 164, false), canopy.Parts[2]);
            var crowded = Descendants(board).OfType<Polygon2D>().Where(node => node.Name.ToString().StartsWith("UnitCue_", StringComparison.Ordinal)).ToArray();
            require(crowded.Length == 4 && crowded.All(cue => cue.Position.X >= 10 && cue.Position.Y >= 10),
                "Four adjacent covered actors near the board edge must retain in-bounds cues.");
            require(crowded.All(cue => crowded.All(other => cue == other || cue.Position.DistanceTo(other.Position) >= 24)),
                "Crowded occlusion cues must not overlap or share a pointer target.");
            foreach (var cue in crowded)
            {
                var id = int.Parse(cue.Name.ToString()["UnitCue_".Length..], System.Globalization.CultureInfo.InvariantCulture);
                require(board.Pick(cue.Position)?.UnitId == id, "Each near-edge cue must independently select its own canonical actor.");
            }
            state["units"] = savedActors;
            actor = state["units"]![0]!.AsObject();
            actor["owner"] = 1;
            var frontRoof = Material("FrontRoof", 96, 80, 128, 128, 164, false);
            var backObject = Material("BackObject", 96, 80, 128, 128, 148, true);
            var rearBlock = new PaintedOccluderPart
            {
                Id = "RearBlock",
                ContactCell = [3, 3],
                GroundFrontPixels = [112, 148],
                PolygonPixels = backObject.PolygonPixels,
                Pointer = new PaintedPointer { Kind = "block", PolygonPixels = backObject.Pointer.PolygonPixels }
            };
            var rightPost = canopy.Parts[2];
            var overlapPoint = new Vector2(112, 108);
            Materials(frontRoof, rearBlock, rightPost);
            require(board.Projection.Pick(overlapPoint)?.Cell == new Coord(3, 3) && board.Pick(overlapPoint) is null,
                "A front visual-only roof must not tunnel through a rear explicit pointer blocker before reaching ground.");
            var frontTarget = new PaintedOccluderPart
            {
                Id = "FrontTarget",
                ContactCell = frontRoof.ContactCell,
                GroundFrontPixels = frontRoof.GroundFrontPixels,
                PolygonPixels = frontRoof.PolygonPixels,
                Pointer = new PaintedPointer { Kind = "target", PolygonPixels = [[97, 81], [102, 81], [102, 86], [97, 86]] }
            };
            Materials(frontTarget, rearBlock, rightPost);
            require(board.Pick(overlapPoint) is null,
                "The visual-only region outside a front target contour must preserve rear pointer blockers too.");
            var belowGroundBlock = new PaintedOccluderPart
            {
                Id = "BelowGroundBlock",
                ContactCell = rearBlock.ContactCell,
                GroundFrontPixels = [112, 80],
                PolygonPixels = rearBlock.PolygonPixels,
                Pointer = rearBlock.Pointer
            };
            Materials(frontRoof, belowGroundBlock, rightPost);
            require(board.Pick(overlapPoint) == TacticalHit.Ground(new(3, 3)),
                "A blocker behind the actually visible ground surface cannot intercept its Ground hit.");
            Materials(frontRoof, backObject, rightPost);
            require(board.Pick(overlapPoint) == TacticalHit.Ground(new(3, 3)),
                "Pass-through permits projected ground, not the covered target object or its same-cell occupant.");
            Materials(backObject, rightPost);
            var sharedCellCovered = OpaqueActorPoints(board, 2).Where(point => point.X > 97 && point.X < 127 && point.Y > 81 && point.Y < 127).ToArray();
            require(sharedCellCovered.Length > 0 && sharedCellCovered.All(point => board.Pick(point) == TacticalHit.Object(new(3, 3))),
                "A foreground object over its own occupied site must remain Object, never Unit merely because the cell matches.");

            Materials(frontRoof, rightPost);
            var roofNode = Descendants(board).OfType<PaintedGroundView>().Single().DepthEntries.Single(entry => entry.Node.Name == "OpenCanopy_FrontRoof").Node;
            var frame = board.GetNode<Node2D>("AuthoredScene/AuthoredFrame");
            frame.ZIndex = 0;
            using var frameImage = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
            frameImage.Fill(Colors.Transparent);
            frameImage.SetPixel(1, 1, Colors.White);
            using var frameTexture = ImageTexture.CreateFromImage(frameImage);
            var frameSprite = new Sprite2D { Texture = frameTexture, Position = overlapPoint, ZIndex = roofNode.ZIndex - 1 };
            frame.AddChild(frameSprite);
            require(frameSprite.ZIndex > board.Projection.Pick(overlapPoint)!.DrawDepth && board.Pick(overlapPoint) is null,
                "An opaque frame between ground and a front pass-through roof must still block the pointer.");
            frameSprite.Modulate = new Color(1, 1, 1, 0);
            require(board.Pick(overlapPoint) == TacticalHit.Ground(new(3, 3)), "A zero-opacity rear frame must not block pass-through ground.");
            frameSprite.Modulate = Colors.White;
            require(board.Pick(overlapPoint + Vector2.Left) == TacticalHit.Ground(new(3, 3)), "Transparent rear-frame canvas must not acquire a blocking rectangle.");
            frameSprite.Free();
        }
        finally
        {
            host.RemoveChild(board);
            board.Free();
        }
        require(GodotObject.IsInstanceValid(texture), "Multipart teardown must preserve the exact borrowed opaque mother texture.");
    }

    internal static async Task CheckSouthRoad(Node host, Action<bool, string> require)
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = Redstone();
        var source = engine.CreateState(level);
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var art = PresentationJson.Load<PaintedGroundDocument>("res://data/scenes/c01-03-painted-ground.json");
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var screen = new BattleScreen();
        host.AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        void Bind(GrayFlagSession next)
        {
            session = next;
            screen.BindSession(new(presentation.Messages, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane)), level, scene);
            screen.ConfigureGround(art.Factory(source["map"]!.AsObject()));
        }
        try
        {
            Bind(session);
            // First Bind builds the interface and applies FullRect. Size this detached
            // Node-parent screen afterwards, exactly as the main native screen fixture does.
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(1280, 800);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            var map = (Control)screen.FindChild("MapArea", true, false);
            var world = map.GetNode<Node2D>("World");
            var pointerContext = "initial layout";
            Vector2 Field(Vector2 point) => world.Position + point * world.Scale;
            void Down(MouseButton button, Vector2 point) => map.EmitSignal(Control.SignalName.GuiInput,
                new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = point });
            void Up(MouseButton button, Vector2 point) => screen._Input(new InputEventMouseButton
            {
                ButtonIndex = button,
                Position = map.GetGlobalTransformWithCanvas() * point
            });
            void PanTo(Vector2 point)
            {
                var start = map.Size / 2;
                var end = start + start - Field(point);
                Down(MouseButton.Middle, start);
                screen._Input(new InputEventMouseMotion { Position = map.GetGlobalTransformWithCanvas() * end });
                Up(MouseButton.Middle, end);
            }
            void Click(Vector2 field)
            {
                var point = Field(field);
                require(new Rect2(Vector2.Zero, map.Size).HasPoint(point),
                    $"The regression must press an actual on-screen pointer location: context={pointerContext}, field={field}, mapPoint={point}, "
                    + $"map.Size={map.Size}, screen.Size={screen.Size}, world.Position={world.Position}, world.Scale={world.Scale}.");
                Down(MouseButton.Left, point);
                Up(MouseButton.Left, point);
            }
            void Hover(Vector2 field) => map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = Field(field) });
            Vector2 VisibleActor(int id, BoardCell cell) => OpaqueActorPoints(screen.Board, id)
                .First(point => screen.Board.Pick(point) == TacticalHit.Unit(cell, id));
            // Detached placement only: the real engineer has movement 4 and these road
            // cells cost 1. Origin (51,35) makes all four destinations canonically reachable.
            var movementLevel = level.DeepClone().AsObject();
            movementLevel["units"]!.AsArray().OfType<JsonObject>()
                .Single(unit => unit["key"]!.GetValue<string>() == "north-sapper")["y"] = 35;
            var movementSource = engine.CreateState(movementLevel);
            var mover = movementSource["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "north-sapper");
            var moverId = mover["id"]!.GetValue<int>();
            var origin = new BoardCell(51, 35);
            foreach (var zoomed in new[] { false, true })
            {
                foreach (var x in new[] { 52, 53, 54, 55 })
                {
                    // The second pass enters through the real save/load contract and rebuilds all render nodes.
                    Bind(zoomed ? GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(movementSource))
                        : new GrayFlagSession(new(engine, presentation, new JsonArray()), movementLevel));
                    var destination = new BoardCell(x, 35);
                    pointerContext = $"road zoomed={zoomed}, destination={destination}, actor={moverId}, origin={origin}, actorPosition={screen.Board.GetNode<Node2D>($"CanonicalProjection/Unit_{moverId}").Position}";
                    var point = new Vector2(13 + (x + .5f) * 16, 656) * art.WorldScale;
                    PanTo(point);
                    if (zoomed)
                    {
                        map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
                        {
                            ButtonIndex = MouseButton.WheelUp,
                            Pressed = true,
                            CtrlPressed = true,
                            Position = map.Size / 2
                        });
                        screen._Process(0);
                        PanTo(point + new Vector2(20, -10));
                    }
                    require(screen.Board.Projection.CellContact(new(x, 35)).IsEqualApprox(point), "The SouthHouse regression must use the audited source-pixel registration.");
                    foreach (var delta in new[] { Vector2.Zero, new Vector2(-3, -2), new Vector2(3, -2), new Vector2(-3, 2), new Vector2(3, 2) })
                        require(screen.Board.Pick(point + delta) == TacticalHit.Ground(destination),
                            $"SouthHouse roof over road {destination} must preserve Ground across the pointer footprint, not target entrance (55,40).");
                    var queries = session.GetTacticalQueries(0);
                    var path = queries.PathTo(moverId, destination);
                    var stops = queries.MoveStops(moverId);
                    require(stops.Contains(destination) && path is { Count: > 1 },
                        $"Each audited road stop must have a canonical legal actor path: origin={origin}, destination={destination}, "
                        + $"PathTo={(path is null ? "null" : string.Join(" -> ", path))}, MoveStops={string.Join(",", stops)}.");
                    Click(VisibleActor(moverId, origin));
                    require(screen.SelectedUnitId == moverId, "The movement fixture must select its visible actor through actual native pointer input.");
                    Hover(point);
                    require(screen.Selection?.Kind == TacticalSelectionKind.Unit, "Ground hover must retain the selected actor rather than inspect a roof target.");
                    Click(point);
                    if (session.Revision == 0)
                    {
                        require(screen.Selection?.Destination == destination && screen.Selection.Path.SequenceEqual(path!),
                            "The native road click must preview the canonical road path, never the house entrance.");
                        var wait = Descendants(screen).OfType<Button>().Single(button => button.GetMeta("hud_key", "").AsString() == "commands/wait");
                        wait.EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    require(session.Revision == 1 && session.GetTacticalQueries(0).Unit(moverId)!.At == destination,
                        "The actual pointer-selected road destination must permit and commit the real movement command.");
                }
            }
            Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
            // The reported south-route obstruction is this different house, not the
            // SouthHouse movement fixture above. Protect both authored registrations.
            var southeastRoad = screen.Board.Projection.CellContact(new(88, 34));
            require(southeastRoad.IsEqualApprox(new Vector2(1429, 638) * art.WorldScale),
                "The original SoutheastHouse road must retain its audited source-pixel registration.");
            foreach (var delta in new[] { Vector2.Zero, new Vector2(-3, -2), new Vector2(3, -2), new Vector2(-3, 2), new Vector2(3, 2) })
                require(screen.Board.Pick(southeastRoad + delta) == TacticalHit.Ground(new(88, 34)),
                    "The reported SoutheastHouse roof must not redirect road (88,34) to entrance (88,39).");
            require(screen.Board.Pick(new Vector2(1450, 687) * art.WorldScale) == TacticalHit.Object(new(88, 39)),
                "The original SoutheastHouse must retain its independent selectable facade.");
            pointerContext = "independent SouthHouse facade";
            var facade = new Vector2(890, 708) * art.WorldScale;
            PanTo(facade);
            require(screen.Board.Pick(facade) == TacticalHit.Object(new(55, 40)), "The independently authored SouthHouse facade must remain an Object target.");
            Click(facade);
            require(screen.Selection is { Kind: TacticalSelectionKind.Site, Destination: { X: 55, Y: 40 } } && session.Revision == 0,
                "A real facade mouse click must pin the canonical site without advancing the battle.");

            var hidden = source.DeepClone().AsObject();
            var attacker = hidden["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "campaign-torren");
            var enemy = hidden["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "oath-guard-1");
            var secondEnemy = hidden["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "oath-guard-2");
            attacker["x"] = 51;
            attacker["y"] = 35;
            enemy["x"] = 52;
            enemy["y"] = 35;
            // Keep two genuine legal targets so the existing singleton-command shortcut
            // cannot auto-execute instead of entering explicit target mode.
            secondEnemy["x"] = 50;
            secondEnemy["y"] = 35;
            Bind(GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(hidden)));
            pointerContext = "covered road enemy=(52,35), second enemy=(50,35), attacker=(51,35)";
            var hiddenId = enemy["id"]!.GetValue<int>();
            var attackerId = attacker["id"]!.GetValue<int>();
            var hiddenCell = new BoardCell(52, 35);
            var roof = art.Occluders.Single(item => item.Id == "SouthHouse").Parts
                .Select(part => part.PolygonPixels.Select(point => new Vector2((float)point[0], (float)point[1]) * art.WorldScale).ToArray()).ToArray();
            var coveredPoints = OpaqueActorPoints(screen.Board, hiddenId)
                .Where(point => screen.Board.Projection.Pick(point)?.Cell == new Coord(hiddenCell.X, hiddenCell.Y)
                    && roof.Any(polygon => Geometry2D.IsPointInPolygon(point, polygon))).ToArray();
            require(coveredPoints.Length > 0, "The hidden-road regression must sample opaque actor-body pixels inside the actual SouthHouse roof, not empty ground.");
            require(coveredPoints.All(point => screen.Board.Pick(point) == TacticalHit.Ground(hiddenCell)),
                "A visually covered unit body on a road must yield Ground, never Unit or a hidden Object.");
            var hiddenPoint = coveredPoints[coveredPoints.Length / 2];
            PanTo(hiddenPoint);
            Hover(hiddenPoint);
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            require(hud.Projection!.Portrait is null && screen.SelectedUnitId is null,
                "Actual ground hover over a concealed actor must not reveal its portrait or inspect identity.");
            Click(hiddenPoint);
            require(screen.SelectedUnitId is null && hud.Projection!.Portrait is null && session.Revision == 0,
                "An idle ground click must not re-promote the concealed canonical occupant.");
            var attackerCell = new BoardCell(51, 35);
            require(session.GetTacticalQueries(0).CommandsAt(attackerId, attackerCell).Any(command => command.Hostile && command.Targets.Contains(hiddenCell)),
                "The hidden enemy must genuinely be a legal canonical attack target so the no-autoattack assertion is meaningful.");
            Click(VisibleActor(attackerId, attackerCell));
            Click(hiddenPoint);
            require(screen.Selection?.Kind != TacticalSelectionKind.Target && session.Revision == 0
                && hud.Projection!.Portrait?.ActorKey != enemy["key"]!.GetValue<string>(),
                "Ground intent must survive BoardView and BattleScreen without auto-arming or inspecting the hidden enemy.");
            Click(VisibleActor(attackerId, attackerCell));
            Click(VisibleActor(attackerId, attackerCell));
            var attackButton = Descendants(screen).OfType<Button>().First(button => button.GetMeta("hud_key", "").AsString().StartsWith("commands/attack:", StringComparison.Ordinal));
            var attackKey = attackButton.GetMeta("hud_key").AsString()["commands/".Length..];
            var explicitAttack = session.GetTacticalQueries(0).CommandsAt(attackerId, attackerCell).Single(command => command.Key == attackKey);
            require(explicitAttack.TargetSubjects.Count >= 2
                && explicitAttack.TargetSubjects.Contains(TacticalTarget.Unit(hiddenCell, hiddenId))
                && explicitAttack.TargetSubjects.Contains(TacticalTarget.Unit(new(50, 35), secondEnemy["id"]!.GetValue<int>())),
                $"The actual mounted attack command must have two explicit legal Unit targets, preserving singleton auto-execution semantics: key={attackKey}, targets={string.Join(",", explicitAttack.TargetSubjects)}.");
            attackButton.EmitSignal(BaseButton.SignalName.Pressed);
            require(screen.Selection?.Kind == TacticalSelectionKind.Target && session.Revision == 0,
                $"The mounted multi-target command must enter genuine explicit entity-target mode: selection={screen.Selection?.Kind}, revision={session.Revision}, key={attackKey}.");
            Hover(hiddenPoint);
            require(hud.Projection!.Sections.All(section => section.Key != "forecast"),
                "Ground hover in explicit attack mode must not expose a hidden-occupant combat forecast.");
            var previewBeforeMiss = screen.Selection;
            Click(hiddenPoint);
            require(session.Revision == 0 && screen.Selection == previewBeforeMiss
                && screen.Selection?.Kind == TacticalSelectionKind.Target,
                "An invalid Ground click preserves the complete explicit attack preview without becoming a hidden entity-specific command.");
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            require(screen.Selection?.Kind == TacticalSelectionKind.Destination && session.Revision == 0,
                "Explicit Escape still backs out of targeting after a refused Ground click.");

            var mixed = hidden.DeepClone().AsObject();
            var structure = mixed["structures"]![0]!.AsObject();
            structure["x"] = hiddenCell.X;
            structure["y"] = hiddenCell.Y;
            var mixedSession = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(mixed));
            var mixedQueries = mixedSession.GetTacticalQueries(41);
            var attack = mixedQueries.CommandsAt(attackerId, attackerCell).First(command => command.Hostile && command.Targets.Contains(hiddenCell));
            require(attack.TargetSubjects.Single(target => target.Cell == hiddenCell) == TacticalTarget.Unit(hiddenCell, hiddenId),
                "A UnitOrStructure command must preserve the canonical primary Unit identity when both occupy the target cell.");
            var targetSelection = new TacticalSelection
            {
                Kind = TacticalSelectionKind.Target,
                UnitId = attackerId,
                Destination = attackerCell,
                Path = [attackerCell],
                Ability = attack.Ability,
                Weapon = attack.Weapon,
                Hostile = attack.Hostile,
                TargetSubjects = attack.TargetSubjects
            };
            foreach (var rejectedHit in new[] { TacticalHit.Ground(hiddenCell), TacticalHit.Object(hiddenCell) })
                require(mixedSession.ProjectHud(mixedQueries.Stamp, targetSelection, rejectedHit, attackerId, false).Sections.All(section => section.Key != "forecast"),
                    "Neither Ground nor a same-cell Object may reveal the canonical Unit attack forecast.");
            require(mixedSession.ProjectHud(mixedQueries.Stamp, targetSelection, TacticalHit.Unit(hiddenCell, hiddenId), attackerId, false)
                    .Sections.Any(section => section.Key == "forecast"),
                "An explicit visible Unit target must still show its actual canonical combat forecast.");
            var movedEnemy = mixed["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == hiddenId);
            movedEnemy["x"] = 72;
            movedEnemy["y"] = 14;
            var structureSession = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(mixed));
            require(structureSession.GetTacticalQueries(0).CommandsAt(attackerId, attackerCell)
                    .Where(command => command.Hostile).SelectMany(command => command.TargetSubjects)
                    .Any(target => target == TacticalTarget.Object(hiddenCell)),
                "The same UnitOrStructure query must expose Object when only the canonical structure remains there.");
        }
        finally
        {
            host.RemoveChild(screen);
            screen.Free();
        }
    }

    private static JsonObject Redstone() => PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
        .OfType<JsonObject>().Single(level => level["id"]!.GetValue<string>() == "c01-03");

    private static IEnumerable<Vector2> OpaqueActorPoints(BoardView board, int id)
    {
        var sprite = board.GetNode<Sprite2D>($"CanonicalProjection/Unit_{id}/AuthoredSprite");
        var rect = sprite.GetRect();
        for (var y = rect.Position.Y + .5f; y < rect.End.Y; y += 1)
            for (var x = rect.Position.X + .5f; x < rect.End.X; x += 1)
            {
                var local = new Vector2(x, y);
                if (sprite.IsPixelOpaque(local))
                    yield return board.ToLocal(sprite.ToGlobal(local));
            }
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
