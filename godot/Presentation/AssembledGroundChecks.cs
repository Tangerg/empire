using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Detached independent-art fixtures exercise the actual board depth/pick pipeline, without changing campaign data.</summary>
public partial class AssembledGroundChecks : Node
{
    private readonly Control viewportFeedback = new()
    {
        Size = new Vector2(1180, 812),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };
    private int assertions;
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    public override async void _Ready()
    {
        AddChild(viewportFeedback);
        try
        {
            await Check();
            GD.Print($"ASSEMBLED_GROUND_CHECKS_OK {assertions} assertions");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Check()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel();
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        var map = state["map"]!.AsObject();
        var actual = PresentationJson.Load<AssembledGroundDocument>("res://data/scenes/c01-01-assembled-ground.json");
        actual.ValidateLevel(level);
        foreach (var decoration in actual.Objects.Where(item => item.SelectionCell is null))
        {
            var x = (int)Math.Floor(decoration.Contact[0]);
            var y = (int)Math.Floor(decoration.Contact[1]);
            var width = map["width"]!.GetValue<int>();
            var height = map["height"]!.GetValue<int>();
            var outside = x < 0 || y < 0 || x >= width || y >= height;
            if (!outside)
            {
                var terrain = engine.Catalog.Definition("terrains", map["tiles"]![y * width + x]!.GetValue<string>())!.Value;
                outside = terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside");
            }
            Require(outside, $"C01 decorative tree foot cannot occupy a playable cell: {decoration.Id}.");
        }
        var actualCatalog = PresentationJson.Load<SceneObjectCatalog>(actual.ObjectCatalogPath);
        foreach (var site in actual.Objects.Where(item => item.SelectionCell is not null))
        {
            var entrance = actualCatalog.Objects[site.AssetId];
            Require(entrance.GroundFrontPixels is { } front && entrance.AnchorPixels.SequenceEqual(front),
                $"C01 doorway is the building sorting front; root shrubs must not bury a unit at the entrance: {site.Id}.");
        }
        foreach (var assetId in actual.Objects.Select(item => item.AssetId).Distinct())
        {
            var asset = actualCatalog.Objects[assetId];
            var texture = ResourceLoader.Load<Texture2D>(asset.ResourcePath);
            using var image = texture.GetImage();
            Require(image.DetectAlpha() != Image.AlphaMode.None,
                $"C01 standing artwork must retain real transparent pixels: {assetId}.");
            Require(image.HasMipmaps(),
                $"C01 minified artwork needs imported mip levels, not only a filter setting: {assetId}.");
        }
        var sites = new[] { new Coord(3, 16), new Coord(18, 13), new Coord(22, 16), new Coord(34, 13) };
        SceneObjectPlacement Site(Coord cell) => new()
        {
            Id = $"Site_{cell.X}_{cell.Y}",
            AssetId = "house",
            Contact = [cell.X + .5, cell.Y + .5],
            Scale = 1,
            SelectionCell = [cell.X, cell.Y],
            OwnsMapStructureBody = true,
            Pointer = "target"
        };
        SceneObjectPlacement Tree(double x, string pointer = "pass-through") => new()
        {
            Id = "Tree",
            AssetId = "house",
            Contact = [x, 20.5],
            Scale = .5f,
            SelectionCell = null,
            OwnsMapStructureBody = false,
            Pointer = pointer
        };
        AssembledGroundDocument Document(params SceneObjectPlacement[] instances) => new()
        {
            LevelId = "c01-01",
            LevelSourceSha256 = EcmaJson.Sha256(level),
            ResourcePath = "fixture-clean-ground",
            PixelSize = [2048, 2048],
            WorldScale = 1,
            OriginPixels = [64, 256],
            CellXPixels = [32, 0],
            CellYPixels = [0, 32],
            ObjectCatalogPath = "fixture-catalog",
            Objects = instances
        };
        var document = Document(sites.Select(Site).Append(Tree(-1)).ToArray());
        document.ValidateLevel(level);
        using var groundImage = Image.CreateEmpty(2048, 2048, false, Image.Format.Rgba8);
        groundImage.Fill(new Color("#607b42"));
        using var ground = ImageTexture.CreateFromImage(groundImage);
        using var objectImage = Image.CreateEmpty(64, 96, false, Image.Format.Rgba8);
        objectImage.Fill(new Color(0, 0, 0, 0));
        objectImage.FillRect(new Rect2I(12, 8, 40, 80), new Color("#a34732"));
        using var house = ImageTexture.CreateFromImage(objectImage);
        var catalog = new SceneObjectCatalog
        {
            Schema = 1,
            Objects = new()
            {
                ["house"] = new SceneObjectAsset { ResourcePath = "fixture-independent.png", PixelSize = [64, 96], AnchorPixels = [32, 88], GroundFrontPixels = [32, 88], ContactShadowRadiiPixels = [15, 4] }
            }
        };
        SceneObjectAsset PointerAsset(double[][]? polygon, SceneObjectDepthStrip[]? strips = null) => new()
        {
            ResourcePath = "fixture-independent.png",
            PixelSize = [64, 96],
            AnchorPixels = [32, 88],
            GroundFrontPixels = [32, 88],
            ContactShadowRadiiPixels = [15, 4],
            PointerPolygonPixels = polygon,
            DepthStrips = strips
        };
        var textures = new Dictionary<string, Texture2D> { ["house"] = house };
        var scene = new AuthoredScene { LevelId = "c01-01", MapSize = [36, 30], RuntimeReady = true, FramePlacements = [] };
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        AssembledGroundView? view = null;
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, document.CreatePlane), level, scene);
            void Bind(AssembledGroundDocument binding)
            {
                board.ConfigureGround((rules, _) => view = AssembledGroundView.Create(binding, ground, catalog, textures, map, rules.Content));
                board.RenderState(state);
            }
            Bind(document);
            Require(board.RenderedStructureCount == 0 && board.RenderedUnitCount == 19,
                "Four registered sites replace structure bodies without changing the 19 canonical actors.");
            Require(view!.Sites.Count == 4 && view.DepthEntries.Count == 5, "Decorative perimeter art is not another canonical site.");
            Require(view.GetNode<Sprite2D>("CleanGround").Texture == ground && view.GetChildren().OfType<Polygon2D>().Count() == 0,
                "Independent objects must not be UV patches cut out of the ground painting.");
            foreach (var cell in sites)
            {
                var sprite = view.GetNode<Sprite2D>($"Site_{cell.X}_{cell.Y}");
                Require(sprite.Texture == house && sprite.Position.IsEqualApprox(board.Projection.CellContact(cell))
                    && SpriteGroundContact.Offset(sprite).IsZeroApprox(), "Authored feet must land exactly at registered elevated contacts.");
                var opaque = sprite.ToGlobal(new Vector2(0, -35));
                Require(board.Pick(board.ToLocal(opaque)) is { Kind: TacticalHitKind.Object } hit
                    && hit.Cell == new BoardCell(cell.X, cell.Y), "Opaque building pixels select only their explicit canonical site.");
                var visual = view.DepthEntries.Single(entry => entry.Node == sprite).Visual!;
                Require(!visual.Covers(sprite.ToGlobal(new Vector2(-30, -35))), "Transparent sprite padding cannot occlude or intercept clicks.");
                var depth = board.Projection.Depth(board.Projection.LogicalCellContact(cell));
                Require(sprite.ZIndex == board.Projection.LayerAtDepth(depth, BoardDepthLayer.Standing),
                    "Independent object draw order must come from the existing board depth owner.");
            }
            foreach (var actor in state["units"]!.AsArray().OfType<JsonObject>())
            {
                var node = board.GetNode<Node2D>($"CanonicalProjection/Unit_{actor["id"]!.GetValue<int>()}");
                var cell = new Coord(actor["x"]!.GetValue<int>(), actor["y"]!.GetValue<int>());
                Require(node.Position.IsEqualApprox(board.Projection.CellContact(cell)), "Independent scenery cannot displace canonical actor feet.");
            }
            var after = state.DeepClone().AsObject();
            var moving = after["units"]![0]!.AsObject();
            var unitId = moving["id"]!.GetValue<int>();
            var from = new Coord(moving["x"]!.GetValue<int>(), moving["y"]!.GetValue<int>());
            var destination = new Coord(from.X + 1, from.Y);
            moving["x"] = destination.X;
            var path = new JsonArray(new JsonObject { ["x"] = from.X, ["y"] = from.Y },
                new JsonObject { ["x"] = destination.X, ["y"] = destination.Y });
            var action = new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = unitId,
                ["path"] = path.DeepClone(),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            };
            var movement = new JsonObject { ["type"] = "move", ["unit"] = unitId, ["path"] = path };
            var transition = new BattlePresentationTransition(1, EcmaJson.ToElement(state), EcmaJson.ToElement(action),
                [EcmaJson.ToElement(movement)], EcmaJson.ToElement(after), false);
            var playback = board.PresentTransitionAsync(viewportFeedback, transition);
            board.AdvancePresentation(.08);
            await Task.Yield();
            var movingNode = board.GetNode<Node2D>($"CanonicalProjection/Unit_{unitId}");
            Require(movingNode.Position.IsEqualApprox(board.Projection.Project(
                board.Projection.LogicalCellContact(from).Lerp(board.Projection.LogicalCellContact(destination), .5f))),
                "Moving feet follow the registered ground continuously rather than a second sprite-space path.");
            board.AdvancePresentation(.081);
            await Task.Yield();
            Require(playback.IsCompleted && await playback && board.GetNode<Node2D>($"CanonicalProjection/Unit_{unitId}").Position
                .IsEqualApprox(board.Projection.CellContact(destination)), "Movement settles on the registered destination with independent scenery active.");
            var tree = view.GetNode<Sprite2D>("Tree");
            var treeVisual = view.DepthEntries.Single(entry => entry.Node == tree).Visual!;
            Require(treeVisual.Covers(tree.ToGlobal(new Vector2(0, -35))) && !treeVisual.ContainsPointer(tree.ToGlobal(new Vector2(0, -35))),
                "Perimeter tree alpha occludes visually while its explicit pass-through contract remains distinct.");
            var oldPoint = tree.GlobalPosition;
            CheckFixtureContacts(view, board.Projection, document);
            var oldView = view;
            var oldContact = view.GetNode<Node2D>("Tree__ground_contact");
            var oldShadow = oldContact.GetNode<Sprite2D>("SourceStructureContact");
            var oldFloor = oldContact.GetNode<PaintedSurface>("RegisteredSurface");
            var movedDocument = Document(sites.Select(Site).Append(Tree(8.5)).ToArray());
            Bind(movedDocument);
            var moved = view!.GetNode<Sprite2D>("Tree");
            CheckFixtureContacts(view, board.Projection, movedDocument);
            Require(IsInstanceValid(oldView) && IsInstanceValid(oldContact) && IsInstanceValid(oldShadow) && IsInstanceValid(oldFloor)
                && oldView.GetParent() is null && oldView.IsQueuedForDeletion()
                && !oldView.IsInsideTree() && !oldContact.IsInsideTree() && !oldShadow.IsInsideTree() && !oldFloor.IsInsideTree(),
                "Replacing ground must immediately detach the old aggregate and queue its owned contacts for deletion.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!IsInstanceValid(oldView) && !IsInstanceValid(oldContact) && !IsInstanceValid(oldShadow) && !IsInstanceValid(oldFloor),
                "Frame cleanup must free replaced ground and its previous sibling contact, source and projected floor.");
            CheckContactNegativeControls(board, view, board.Projection, movedDocument,
                new Vector2I(map["width"]!.GetValue<int>(), map["height"]!.GetValue<int>()));
            Require(moved.GlobalPosition != oldPoint && moved.Texture == house
                && view.GetNode<Sprite2D>("CleanGround").Texture == ground,
                "Moving an independent instance changes only its placement, not ground pixels or resource identity.");
            using var unchanged = ground.GetImage();
            Require(unchanged.GetData().SequenceEqual(groundImage.GetData()), "Object relocation must not bake old objects or shadows into clean ground.");
            Require(EcmaJson.Stringify(state) == before, "Rendering, depth registration and object relocation never mutate canonical state.");
            var originalBody = view.GetNode<Sprite2D>("Site_3_16");
            var contact = originalBody.Position;
            Require(board.Projection.Project(new Vector2(3.5f, 16.5f) * 32).IsEqualApprox(contact),
                "Continuous instance contacts and canonical cell centers share the same projection.");
            catalog.Objects["house"] = PointerAsset([[0, 55], [64, 55], [64, 96], [0, 96]]);
            Bind(document);
            var limited = view!.GetNode<Sprite2D>("Site_3_16");
            var limitedVisual = view.DepthEntries.Single(entry => entry.Node == limited).Visual!;
            var roof = limited.ToGlobal(new Vector2(0, -65));
            var facade = limited.ToGlobal(new Vector2(0, -13));
            Require(limitedVisual.Covers(roof) && !limitedVisual.ContainsPointer(roof),
                "A restricted pointer contour must not erase the opaque roof's material coverage.");
            Require(limitedVisual.ContainsPointer(facade) && board.Pick(board.ToLocal(facade)) is { Kind: TacticalHitKind.Object, Cell.X: 3, Cell.Y: 16 },
                "Independent lower facade must still select its exact canonical site.");
            Require(board.Pick(board.ToLocal(roof)) is not { Kind: TacticalHitKind.Object, Cell.X: 3, Cell.Y: 16 },
                "Opaque roof outside the pointer contour redirected a click to the building.");
            Require(!limitedVisual.ContainsPointer(limited.ToGlobal(new Vector2(-29, -13))),
                "Transparent pixels inside the pointer contour must not become a rectangular hit target.");
            var roofPolygon = new[] { new Vector2(-5, -70), new Vector2(5, -70), new Vector2(5, -60), new Vector2(-5, -60) }.Select(limited.ToGlobal).ToArray();
            Require(!limitedVisual.IntersectsPointer(roofPolygon), "Cue placement treated the whole roof as an interactive facade.");
            var limitedTreeVisual = view.DepthEntries.Single(entry => entry.Node.Name == "Tree").Visual!;
            Require(!limitedTreeVisual.ContainsPointer(limitedTreeVisual.Node.ToGlobal(new Vector2(0, -13))) && !limitedTreeVisual.IntersectsPointer(roofPolygon),
                "Adding a material contour made a pass-through decoration interactive.");
            board.Position = new Vector2(91, 47);
            board.Scale = new Vector2(1.4f, 1.4f);
            limited.Scale = new Vector2(.75f, .75f);
            Require(limitedVisual.ContainsPointer(limited.ToGlobal(new Vector2(0, -13)))
                && !limitedVisual.ContainsPointer(limited.ToGlobal(new Vector2(0, -65)))
                && limitedVisual.Covers(limited.ToGlobal(new Vector2(0, -65))),
                "Pointer contour applied anchor/instance/parent scale twice or altered visual coverage.");
            CheckFixtureContacts(view, board.Projection, document);
            Require(EcmaJson.Stringify(state) == before, "Pointer-only geometry changed canonical battle state.");
            board.Position = Vector2.Zero;
            board.Scale = Vector2.One;
            catalog.Objects["house"] = PointerAsset([[12, 70], [52, 70], [52, 88], [12, 88]],
                [new() { RightPixels = 20, GroundFrontPixels = [12, 88] },
                 new() { RightPixels = 40, GroundFrontPixels = [20, 86] },
                 new() { RightPixels = 64, GroundFrontPixels = [40, 84] }]);
            Bind(Document(Site(sites[0])));
            var strips = view!.DepthEntries.ToArray();
            var firstStrip = (Sprite2D)strips[0].Node;
            Require(strips.Length == 3 && strips.All(entry => ((Sprite2D)entry.Node).Texture == house)
                && strips.Select(entry => entry.Node.ZIndex).Distinct().Count() == 3,
                "Sloping independent material must share its complete texture and use distinct physical depth contacts.");
            for (var y = 0; y < 96; y++)
                for (var x = 0; x < 64; x++)
                {
                    var global = firstStrip.ToGlobal(new Vector2(x + .25f, y + .25f) - new Vector2(32, 88));
                    var expected = objectImage.GetPixel(x, y).A > .5f;
                    var visible = strips.Where(entry => entry.Visual!.Covers(global)).ToArray();
                    Require(visible.Length == (expected ? 1 : 0),
                        $"Independent draw-strip partition must preserve every alpha sample once, without gaps or duplicate coverage: {x},{y}.");
                    var shouldTarget = expected && y >= 70;
                    Require(visible.Any(entry => entry.Visual!.ContainsPointer(global)) == shouldTarget,
                        "Independent depth strips must preserve the original alpha-limited pointer contour.");
                }
            var stripDoor = firstStrip.ToGlobal(new Vector2(32, 76) - new Vector2(32, 88));
            Require(board.Pick(board.ToLocal(stripDoor)) == TacticalHit.Object(new(sites[0].X, sites[0].Y)),
                "A partitioned independent site must still resolve to its original single canonical owner.");
            CheckFixtureContacts(view, board.Projection, Document(Site(sites[0])));
            Require(view.GetChildren().OfType<Node2D>().Count(node => node.Name.ToString().EndsWith("__ground_contact", StringComparison.Ordinal)) == 1,
                "Depth strips must share exactly one independent floor contact, never a standing-child shadow.");
            Require(EcmaJson.Stringify(state) == before, "Depth partitioning cannot create another gameplay writer.");
            catalog.Objects["house"] = PointerAsset(null);
            var lifecycleDocument = Document(Site(sites[0]), Tree(8.5));
            var tiles = map["tiles"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
            var heights = map["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray();
            var size = new Vector2I(map["width"]!.GetValue<int>(), map["height"]!.GetValue<int>());
            var playable = Enumerable.Range(0, tiles.Length).Where(index => !engine.Catalog.Definition("terrains", tiles[index])!
                .Value.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside"))
                .Select(index => new Coord(index % size.X, index / size.X)).ToHashSet();
            foreach (var cached in new[] { false, true })
            {
                using var cache = new ShadowCache();
                var lifetime = AssembledGroundView.Create(lifecycleDocument, ground, catalog, textures, map, engine.Catalog, cached ? cache : null);
                AddChild(lifetime);
                var projection = new BoardProjection(engine.Grids.Get("square4"), lifecycleDocument.CreatePlane, map, playable);
                var replacement = new BoardProjection(engine.Grids.Get("square4"), lifecycleDocument.CreatePlane, map, playable);
                try
                {
                    lifetime.Render(projection, tiles, heights, size);
                    Require(!lifetime.HasNode("RegisteredSurface") && ProjectionSubscriptions(projection) == 0,
                        "Prepared ground must not realize floor geometry before final depth assignment.");
                    var invalidSource = lifetime.GetNode<Node2D>(lifecycleDocument.Objects[^1].Id + "__ground_contact")
                        .GetNode<Sprite2D>("SourceStructureContact");
                    invalidSource.RegionEnabled = true;
                    try
                    {
                        RejectLifecycle(lifetime.ApplyDrawOrder,
                            "Bounded surfaces require a complete, unflipped single-frame texture; sprite regions and cropped atlas frames are unsupported.");
                        Require(!lifetime.HasNode("RegisteredSurface") && ProjectionSubscriptions(projection) == 0,
                            "Failed first completion must release every staged floor and subscription.");
                    }
                    finally { invalidSource.RegionEnabled = false; }
                    projection.PrepareDepthPartitions(cached ? new float[] { 23, 77, 145 } : Array.Empty<float>(), []);
                    lifetime.ApplyDrawOrder();
                    Require(lifetime.HasNode("RegisteredSurface") && ProjectionSubscriptions(projection) == lifecycleDocument.Objects.Length + 1,
                        "Completed ground must realize exactly one base floor and one floor per contact.");
                    CheckFixtureContacts(lifetime, projection, lifecycleDocument);
                    var contacts = lifecycleDocument.Objects.Select(item => lifetime.GetNode<Node2D>(item.Id + "__ground_contact")).ToArray();
                    var sources = contacts.Select(node => node.GetNode<Sprite2D>("SourceStructureContact")).ToArray();
                    var floors = contacts.Select(node => node.GetNode<PaintedSurface>("RegisteredSurface")).ToArray();
                    var shadowTextures = sources.Select(source => source.Texture).ToArray();
                    void Retained(PaintedSurface[] expectedFloors, BoardProjection currentProjection)
                    {
                        Require(contacts.All(IsInstanceValid) && sources.All(IsInstanceValid) && shadowTextures.All(IsInstanceValid)
                            && contacts.Select(node => node.GetNode<PaintedSurface>("RegisteredSurface")).SequenceEqual(expectedFloors)
                            && contacts.Select(node => node.GetNode<Sprite2D>("SourceStructureContact")).SequenceEqual(sources)
                            && sources.Select(source => source.Texture).SequenceEqual(shadowTextures),
                            $"Contact lifecycle ({cached}): repeated render must retain sources, textures and current floors.");
                        CheckFixtureContacts(lifetime, currentProjection, lifecycleDocument);
                    }
                    void RejectLifecycle(Action check, string expected)
                    {
                        var rejected = false;
                        try
                        {
                            check();
                        }
                        catch (InvalidOperationException error) when (error.Message == expected) { rejected = true; }
                        Require(rejected, "Contact lifecycle negative control failed: " + expected);
                        GD.Print($"ASSEMBLED_CONTACT_LIFECYCLE_NEGATIVE_OK cached={cached} {expected}");
                    }
                    void Replaced() => Require(floors.All(floor => !IsInstanceValid(floor)) && ProjectionSubscriptions(projection) == 0,
                        "Projection replacement must free old floors and unsubscribe the old projection.");
                    sources[0].Texture = house;
                    try
                    {
                        RejectLifecycle(() => Retained(floors, projection),
                            $"Contact lifecycle ({cached}): repeated render must retain sources, textures and current floors.");
                    }
                    finally { sources[0].Texture = shadowTextures[0]; }
                    RejectLifecycle(Replaced, "Projection replacement must free old floors and unsubscribe the old projection.");
                    for (var repeat = 0; repeat < 3; repeat++)
                    {
                        var polygons = floors.SelectMany(floor => floor.GetChildren().OfType<Polygon2D>()).ToArray();
                        lifetime.Render(projection, tiles, heights, size);
                        lifetime.ApplyDrawOrder();
                        Retained(floors, projection);
                        Require(floors.SelectMany(floor => floor.GetChildren().OfType<Polygon2D>()).SequenceEqual(polygons),
                            "Repeated draw completion must retain already-realized floor polygons.");
                    }
                    Require(cached ? shadowTextures[0] == shadowTextures[1] : shadowTextures[0] != shadowTextures[1],
                        "Equal radii borrow one cached texture, while uncached sources own independent textures.");
                    var oldPolygons = floors.SelectMany(floor => floor.GetChildren().OfType<Polygon2D>()).ToArray();
                    projection.PrepareDepthPartitions([32, 100, 170], []);
                    Require(oldPolygons.Length > 0 && oldPolygons.All(polygon => !IsInstanceValid(polygon)),
                        "Realized floors must still rebuild synchronously when depth partitions change.");
                    Retained(floors, projection);
                    lifetime.Render(replacement, tiles, heights, size);
                    Require(ProjectionSubscriptions(replacement) == 0,
                        "Preparing a replacement must not create premature floor subscriptions.");
                    Retained(floors, projection);
                    var committedBase = lifetime.GetNode<PaintedSurface>("RegisteredSurface");
                    var committedOrder = lifetime.GetChildren().ToArray();
                    var committedPositions = contacts.Select(node => node.Position).ToArray();
                    // Reject the last contact after earlier replacement surfaces have been staged.
                    sources[^1].RegionEnabled = true;
                    try
                    {
                        RejectLifecycle(lifetime.ApplyDrawOrder,
                            "Bounded surfaces require a complete, unflipped single-frame texture; sprite regions and cropped atlas frames are unsupported.");
                        Require(ProjectionSubscriptions(replacement) == 0,
                            "Failed floor completion must release every staged subscription.");
                        Require(ProjectionSubscriptions(projection) == contacts.Length + 1,
                            "Failed floor completion must retain every committed subscription.");
                        Require(IsInstanceValid(committedBase) && lifetime.GetNode<PaintedSurface>("RegisteredSurface") == committedBase
                            && lifetime.GetChildren().SequenceEqual(committedOrder) && contacts.Select(node => node.Position).SequenceEqual(committedPositions),
                            "Failed floor completion must preserve the committed base, sibling order and contact positions.");
                        Retained(floors, projection);
                    }
                    finally { sources[^1].RegionEnabled = false; }
                    replacement.PrepareDepthPartitions([50, 130], []);
                    lifetime.ApplyDrawOrder();
                    Replaced();
                    var replacementFloors = contacts.Select(node => node.GetNode<PaintedSurface>("RegisteredSurface")).ToArray();
                    Retained(replacementFloors, replacement);
                    lifetime.Render(replacement, tiles, heights, size);
                    lifetime.ApplyDrawOrder();
                    Retained(replacementFloors, replacement);
                    Require(ProjectionSubscriptions(replacement) == contacts.Length + 1,
                        "Only the base floor and one floor per contact may subscribe to the projection.");
                    void Unloaded() => Require(contacts.All(node => !IsInstanceValid(node)) && sources.All(source => !IsInstanceValid(source))
                        && replacementFloors.All(floor => !IsInstanceValid(floor)) && ProjectionSubscriptions(replacement) == 0,
                        "Unload must release contacts, sources, floors and all projection subscriptions.");
                    RejectLifecycle(Unloaded, "Unload must release contacts, sources, floors and all projection subscriptions.");
                    var pending = new BoardProjection(engine.Grids.Get("square4"), lifecycleDocument.CreatePlane, map, playable);
                    lifetime.Render(pending, tiles, heights, size);
                    Require(ProjectionSubscriptions(pending) == 0,
                        "An uncompleted replacement must not own floor subscriptions.");
                    RemoveChild(lifetime);
                    lifetime.Free();
                    Unloaded();
                    Require(ProjectionSubscriptions(pending) == 0,
                        "Unload before draw completion must not subscribe to the prepared projection.");
                    Require(shadowTextures.All(texture => IsInstanceValid(texture) == cached),
                        "Unload releases uncached source-owned textures but preserves cache-owned textures.");
                    cache.Dispose();
                    Require(shadowTextures.All(texture => !IsInstanceValid(texture)), "Disposing the cache must release its retained contact texture.");
                }
                finally
                {
                    if (IsInstanceValid(lifetime))
                    {
                        RemoveChild(lifetime);
                        lifetime.Free();
                    }
                }
            }
            Require(EcmaJson.Stringify(state) == before, "Contact lifecycle checks cannot mutate canonical battle state.");
        }
        finally { RemoveChild(board); board.Free(); }
        void Reject(AssembledGroundDocument invalid, string reason)
        {
            var rejected = false;
            try
            {
                AssembledGroundView.Create(invalid, ground, catalog, textures, map, engine.Catalog).Free();
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException) { rejected = true; }
            Require(rejected && IsInstanceValid(ground) && IsInstanceValid(house), reason);
        }
        using var solidImage = Image.CreateEmpty(64, 96, false, Image.Format.Rgb8);
        solidImage.Fill(Colors.White);
        using var solidTexture = ImageTexture.CreateFromImage(solidImage);
        Require(solidImage.DetectAlpha() == Image.AlphaMode.None, "Opaque material fixture must really be RGB without alpha.");
        catalog.Objects["house"] = PointerAsset(null);
        textures["house"] = solidTexture;
        var solidSpriteRejected = false;
        try
        {
            AssembledGroundView.Create(Document(Site(sites[0])), ground, catalog, textures, map, engine.Catalog).Free();
        }
        catch (InvalidOperationException error) when (error.Message.Contains("must have transparency", StringComparison.Ordinal))
        {
            solidSpriteRejected = true;
        }
        Require(solidSpriteRejected && IsInstanceValid(solidTexture), "Full opaque sprite must still reject at the transparency contract without disposing borrowed material.");
        SceneObjectAsset SurfaceAsset(string coverage) => new()
        {
            ResourcePath = "fixture-independent.png",
            PixelSize = [64, 96],
            AnchorPixels = [32, 88],
            ContactShadowRadiiPixels = null,
            SurfaceGeometryPath = $"res://data/scene-test-fixtures/{coverage}-material-surface.json"
        };
        var materialBoard = new BoardView { AnimationsEnabled = false };
        AddChild(materialBoard);
        using var nativeImage = Image.CreateEmpty(64, 96, false, Image.Format.Rgba8);
        nativeImage.Fill(Colors.White);
        nativeImage.SetPixel(16, 40, Colors.Transparent);
        using var nativeTexture = ImageTexture.CreateFromImage(nativeImage);
        try
        {
            var binding = Document(Site(sites[0]));
            materialBoard.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, binding.CreatePlane), level, scene);
            var empty = state.DeepClone().AsObject();
            empty["units"] = new JsonArray();
            foreach (var coverage in new[] { "opaque", "native-alpha" })
            {
                var material = coverage == "opaque" ? solidTexture : nativeTexture;
                textures["house"] = material;
                catalog.Objects["house"] = SurfaceAsset(coverage);
                AssembledGroundView? materialView = null;
                GD.Print("RGB_SURFACE_MATERIAL_ACCEPTANCE_BEGIN " + coverage);
                materialBoard.ConfigureGround((rules, _) => materialView = AssembledGroundView.Create(binding, ground, catalog, textures, map, rules.Content));
                materialBoard.RenderState(empty);
                Require(materialView!.DepthEntries.Count == 2 && materialView.Sites.Count == 1, "Two geometry surfaces must keep one canonical site.");
                var entry = materialView.DepthEntries[0];
                var authoredMaterial = PresentationJson.Load<SceneSurfaceDocument>(catalog.Objects["house"].SurfaceGeometryPath!);
                CheckFixtureMaterial(materialView, authoredMaterial, material);
                CheckMaterialNegativeControls(materialView, authoredMaterial, material, house);
                Vector2 Global(float x, float y) => entry.Node.ToGlobal(new Vector2(x, y) - new Vector2(32, 88));
                var target = TacticalHit.Object(new(sites[0].X, sites[0].Y));
                Require(entry.Visual!.Covers(Global(15.25f, 42.25f)) && materialBoard.Pick(materialBoard.ToLocal(Global(15.25f, 42.25f))) == target,
                    "RGB/native material interior must render and pick its explicit site.");
                Require(materialView.DepthEntries.All(e => !e.Visual!.Covers(Global(32.25f, 40.25f)))
                    && materialBoard.Pick(materialBoard.ToLocal(Global(32.25f, 40.25f))) != target,
                    "Opaque RGB texture must not fill the actual geometric gap.");
                var nativeHole = Global(16.25f, 40.25f);
                Require(entry.Visual.Covers(nativeHole) == (coverage == "opaque")
                    && (materialBoard.Pick(materialBoard.ToLocal(nativeHole)) == target) == (coverage == "opaque"),
                    "Native-alpha mapped hole must remain transparent while solid RGB material covers its polygon.");
                var rightEntry = materialView.DepthEntries[1];
                Require(rightEntry.Visual!.Covers(Global(45.25f, 42.25f)) && rightEntry.Visual.ContainsPointer(Global(45.25f, 42.25f))
                    && !entry.Visual.Covers(Global(45.25f, 42.25f)) && !rightEntry.Visual.Covers(Global(15.25f, 42.25f))
                    && materialBoard.Pick(materialBoard.ToLocal(Global(45.25f, 42.25f))) == target,
                    "Batched drawing must preserve the right physical support and its separate pick coverage.");
                materialBoard.RenderState(empty);
                CheckFixtureMaterial(materialView, authoredMaterial, material);
                Require(materialBoard.RenderingIssueCount == 0, "Material fixture must not hide rendering issues.");
            }
            Require(EcmaJson.Stringify(state) == before, "Material coverage cannot mutate gameplay state.");
        }
        finally
        {
            RemoveChild(materialBoard);
            materialBoard.Free();
            textures["house"] = house;
            catalog.Objects["house"] = PointerAsset(null);
        }
        foreach (var invalidPolygon in new double[][][]
        {
            [], [[0, 0], [1, 1]], [[1, 1], [1, 1], [2, 3]],
            [[1, 1], [2, 2], [3, 3]], [[0, 0], [40, 40], [0, 40], [40, 0]],
            [[-1, 0], [40, 0], [0, 40]], [[0, 0], [65, 0], [0, 40]],
            [[0, 0], [double.NaN, 0], [0, 40]]
        })
        {
            catalog.Objects["house"] = PointerAsset(invalidPolygon);
            Reject(Document(Site(sites[0])), "Invalid independent pointer polygon was accepted or disposed borrowed textures.");
        }
        foreach (var strips in new SceneObjectDepthStrip[][]
        {
            [], [new() { RightPixels = 64, GroundFrontPixels = [32, 88] }],
            [new() { RightPixels = 32, GroundFrontPixels = [16, 88] }, new() { RightPixels = 63, GroundFrontPixels = [48, 88] }],
            [new() { RightPixels = 32, GroundFrontPixels = [16, 88] }, new() { RightPixels = 65, GroundFrontPixels = [48, 88] }],
            [new() { RightPixels = 32, GroundFrontPixels = [16, 88] }, new() { RightPixels = 32, GroundFrontPixels = [32, 88] }],
            [new() { RightPixels = 32, GroundFrontPixels = [40, 88] }, new() { RightPixels = 64, GroundFrontPixels = [48, 88] }],
            [new() { RightPixels = 32, GroundFrontPixels = [16, double.NaN] }, new() { RightPixels = 64, GroundFrontPixels = [48, 88] }]
        })
        {
            catalog.Objects["house"] = PointerAsset(null, strips);
            Reject(Document(Site(sites[0])), "Malformed depth partitions must reject atomically without discarding independent texture pixels.");
        }
        catalog.Objects["house"] = PointerAsset(null);
        Reject(Document(Site(sites[0]), Site(sites[0])), "Duplicate instance/site ownership is rejected without disposing borrowed art.");
        Reject(Document(Tree(-1000)), "Decorative contacts may leave the map but not the registered canvas.");
        Reject(Document(Tree(2, "automatic")), "Pointer semantics must be explicit.");
        Reject(Document(new SceneObjectPlacement
        {
            Id = "invalid",
            AssetId = "house",
            Contact = [1, 1],
            Scale = 1,
            SelectionCell = null,
            OwnsMapStructureBody = true,
            Pointer = "target"
        }), "A structure body cannot own an absent canonical site.");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonNode.Parse(JsonSerializer.Serialize(document, options))!.AsObject();
        json["occluders"] = new JsonArray();
        var rejectedOldPatches = false;
        try
        {
            JsonSerializer.Deserialize<AssembledGroundDocument>(json.ToJsonString(), options);
        }
        catch (JsonException) { rejectedOldPatches = true; }
        Require(rejectedOldPatches, "Assembled documents reject obsolete background-patch occluders rather than silently combining renderers.");
    }
    private void CheckFixtureMaterial(AssembledGroundView view, SceneSurfaceDocument authored, Texture2D material)
    {
        var entries = view.DepthEntries.ToArray();
        var anchor = new Vector2(32, 88);
        Vector2 Point(double[] point) => new((float)point[0], (float)point[1]);
        Require(entries.Length == authored.Supports.Length && entries.Select(entry => entry.Node).Distinct().Count() == entries.Length
            && entries.All(entry => entry.Node.Visible && entry.SelectionCell == entries[0].SelectionCell),
            "Material contract: drawing must retain every physical support and its canonical site.");
        var expected = authored.Supports.SelectMany(support => authored.Members.Where(member => member.SupportId == support.Id))
            .SelectMany(member => Geometry2D.TriangulatePolygon(member.PolygonPixels.Select(Point).ToArray()).Chunk(3)
                .Select(indices => (Points: indices.Select(index => Point(member.PolygonPixels[index])).ToArray(),
                    Uv: indices.Select(index => Point(member.UvPixels[index])).ToArray(), Tint: Color.FromHtml(member.Tint))))
            .ToArray();
        for (var index = 0; index < entries.Length; index++)
            Require(entries[index].Visual!.FrontOffset(entries[index].Node).IsEqualApprox(Point(authored.Supports[index].FrontPixels) - anchor)
                && !entries[index].Node.ZAsRelative && entries[index].Node.ZIndex == entries[0].Node.ZIndex,
                "Material contract: each support must retain its own front at the assigned equal depth.");
        var draws = entries.OrderBy(entry => entry.Node.GetIndex()).SelectMany(entry => entry.Node.GetChildren().OfType<Polygon2D>()).ToArray();
        var visible = draws.Where(draw => draw.IsVisibleInTree()).ToArray();
        var primitives = visible.SelectMany(draw => draw.Polygons.Select(indices => (Draw: draw, Indices: indices.AsInt32Array()))).ToArray();
        Require(primitives.Length == expected.Length, "Material contract: visible primitives must cover the authored geometry exactly once.");
        Require(draws.Length == entries.Length && draws[0].Visible && draws.Skip(1).All(draw => !draw.Visible),
            "Material contract: adjacent equal-depth supports must share only their drawing projection.");
        foreach (var draw in visible)
        {
            Require(draw.Texture == material && draw.Material is null && !draw.UseParentMaterial,
                "Material contract: visible primitives must use the selected complete material.");
            Require(draw.Color == Colors.White && draw.Modulate == Colors.White && draw.SelfModulate == Colors.White
                && entries.All(entry => entry.Node.Modulate == Colors.White && entry.Node.SelfModulate == Colors.White),
                "Material contract: authored tint must not be multiplied by another draw tint.");
            Require(draw.Polygon.Length == draw.UV.Length && draw.Polygon.Length == draw.VertexColors.Length
                && draw.Polygon.Length == expected.Sum(primitive => primitive.Points.Length),
                "Material contract: independent primitive vertices must retain their UV and tint arrays.");
        }
        for (var primitive = 0; primitive < expected.Length; primitive++)
        {
            var (draw, indices) = primitives[primitive];
            Require(indices.Length == 3 && indices.All(index => index >= 0 && index < draw.Polygon.Length),
                "Material contract: each visible primitive must retain its triangle indices.");
            for (var corner = 0; corner < indices.Length; corner++)
            {
                var index = indices[corner];
                Require(draw.ToGlobal(draw.Polygon[index]).IsEqualApprox(entries[0].Node.ToGlobal(expected[primitive].Points[corner] - anchor)),
                    "Material contract: visible primitive geometry and order must match the authored fixture.");
                Require(draw.UV[index].IsEqualApprox(expected[primitive].Uv[corner]),
                    "Material contract: visible primitive UVs must match the authored fixture.");
                Require(draw.VertexColors[index] == expected[primitive].Tint,
                    "Material contract: visible primitive tint must match the authored fixture.");
            }
        }
    }

    private void CheckMaterialNegativeControls(AssembledGroundView view, SceneSurfaceDocument authored, Texture2D material, Texture2D wrongMaterial)
    {
        var draw = view.DepthEntries.SelectMany(entry => entry.Node.GetChildren().OfType<Polygon2D>()).First(node => node.Visible);
        void RejectMutation(Action mutate, Action restore, string expected)
        {
            var rejected = false;
            try
            {
                mutate();
                try
                {
                    CheckFixtureMaterial(view, authored, material);
                }
                catch (InvalidOperationException error) when (error.Message == "Material contract: " + expected) { rejected = true; }
                Require(rejected, "Material negative control failed to detect: " + expected);
                GD.Print("ASSEMBLED_MATERIAL_NEGATIVE_OK " + expected);
            }
            finally { restore(); }
            CheckFixtureMaterial(view, authored, material);
        }
        RejectMutation(() => draw.Texture = wrongMaterial, () => draw.Texture = material,
            "visible primitives must use the selected complete material.");
        var uv = draw.UV;
        var wrongUv = (Vector2[])uv.Clone();
        wrongUv[0] += Vector2.Right;
        RejectMutation(() => draw.UV = wrongUv, () => draw.UV = uv,
            "visible primitive UVs must match the authored fixture.");
        var polygons = draw.Polygons;
        var missing = polygons.Duplicate();
        missing.RemoveAt(missing.Count - 1);
        RejectMutation(() => draw.Polygons = missing, () => draw.Polygons = polygons,
            "visible primitives must cover the authored geometry exactly once.");
        var duplicate = polygons.Duplicate();
        duplicate.Add(polygons[0]);
        RejectMutation(() => draw.Polygons = duplicate, () => draw.Polygons = polygons,
            "visible primitives must cover the authored geometry exactly once.");
        var follower = view.DepthEntries[1].Node.GetChildren().OfType<Polygon2D>().Single();
        RejectMutation(() => follower.Visible = true, () => follower.Visible = false,
            "visible primitives must cover the authored geometry exactly once.");
        var reversed = polygons.Duplicate();
        reversed[0] = polygons[1];
        reversed[1] = polygons[0];
        RejectMutation(() => draw.Polygons = reversed, () => draw.Polygons = polygons,
            "visible primitive geometry and order must match the authored fixture.");
        var colors = draw.VertexColors;
        var wrongColors = (Color[])colors.Clone();
        wrongColors[0] = Colors.Red;
        RejectMutation(() => draw.VertexColors = wrongColors, () => draw.VertexColors = colors,
            "visible primitive tint must match the authored fixture.");
    }

    private void CheckFixtureContacts(AssembledGroundView view, BoardProjection projection, AssembledGroundDocument document)
    {
        var contacts = view.GetChildren().OfType<Node2D>()
            .Where(node => node.Name.ToString().EndsWith("__ground_contact", StringComparison.Ordinal)).ToArray();
        Require(contacts.Length == document.Objects.Length, "Contact contract: exactly one sibling contact per object.");
        var baseFloor = view.GetNode<PaintedSurface>("RegisteredSurface");
        foreach (var placement in document.Objects)
        {
            var contact = contacts.SingleOrDefault(node => node.Name == placement.Id + "__ground_contact");
            Require(contact is not null, "Contact contract: exactly one sibling contact per object.");
            var sources = contact!.GetChildren().OfType<Sprite2D>().ToArray();
            Require(sources.Length == 1 && sources[0].Name == "SourceStructureContact",
                "Contact contract: exactly one source belongs to the sibling contact.");
            var source = sources[0];
            Require(!source.Visible, "Contact contract: source must remain hidden.");
            var floors = contact.GetChildren().OfType<PaintedSurface>().ToArray();
            Require(floors.Length == 1 && floors[0].Name == "RegisteredSurface" && contact.GetChildCount() == 2,
                "Contact contract: exactly one projected floor belongs to the sibling contact.");
            var floor = floors[0];
            var expected = projection.Project(new Vector2((float)placement.Contact[0], (float)placement.Contact[1]) * BoardProjection.TileSize);
            Require(contact.Position.IsEqualApprox(expected) && contact.Scale.IsEqualApprox(Vector2.One)
                && floor.Position.IsEqualApprox(-expected) && floor.Scale.IsEqualApprox(Vector2.One),
                "Contact contract: floor placement must follow the registered authored foot.");
            Require(source.ShowBehindParent && !source.Centered && source.Scale.IsEqualApprox(Vector2.One * placement.Scale / 2)
                && Mathf.IsEqualApprox(source.GetGlobalTransform().X.Length(), view.GetGlobalTransform().X.Length() * placement.Scale / 2)
                && Mathf.IsEqualApprox(source.GetGlobalTransform().Y.Length(), view.GetGlobalTransform().Y.Length() * placement.Scale / 2),
                "Contact contract: source density and instance scale must apply exactly once.");
            Require(source.ToGlobal(source.Texture.GetSize() / 2).IsEqualApprox(view.ToGlobal(expected)),
                "Contact contract: source texture must be centered on the authored foot.");
            Require(contact.GetIndex() > baseFloor.GetIndex() && floor.Visible
                && !view.DepthEntries.Any(entry => entry.Node == contact || contact.IsAncestorOf(entry.Node)),
                "Contact contract: floor must follow base ground without standing or picking registration.");
            var surfaceDepths = projection.DepthPartitions.Select(partition => projection.LayerAtDepth(partition.StartDepth, BoardDepthLayer.Surface)).ToHashSet();
            Require(placement.SelectionCell is null || floor.GetChildren().OfType<Polygon2D>().Any(),
                "Contact contract: a registered site must have visible projected floor geometry.");
            foreach (var polygon in floor.GetChildren().OfType<Polygon2D>())
            {
                Require(!polygon.ZAsRelative && surfaceDepths.Contains(polygon.ZIndex),
                    "Contact contract: projected polygons must use absolute floor depth, never standing depth.");
                Require(polygon.Texture == source.Texture && polygon.Polygon.Length == polygon.UV.Length,
                    "Contact contract: projected floor must borrow the complete source texture.");
                for (var index = 0; index < polygon.Polygon.Length; index++)
                    Require(polygon.ToGlobal(polygon.Polygon[index]).IsEqualApprox(source.ToGlobal(polygon.UV[index])),
                        "Contact contract: projected floor UVs must preserve source placement and scale.");
            }
            Require(view.DepthEntries.All(entry => !entry.Node.HasNode("SourceStructureContact")),
                "Contact contract: standing objects must not own a legacy contact source.");
        }
    }

    private void CheckContactNegativeControls(BoardView board, AssembledGroundView view, BoardProjection projection, AssembledGroundDocument document, Vector2I mapSize)
    {
        var placement = document.Objects.First(item => item.SelectionCell is not null);
        var contact = view.GetNode<Node2D>(placement.Id + "__ground_contact");
        var source = contact.GetNode<Sprite2D>("SourceStructureContact");
        var floor = contact.GetNode<PaintedSurface>("RegisteredSurface");
        var standing = view.GetNode<Sprite2D>(placement.Id);
        void RejectMutation(Action mutate, Action restore, string expected)
        {
            var rejected = false;
            try
            {
                mutate();
                try
                {
                    CheckFixtureContacts(view, projection, document);
                }
                catch (InvalidOperationException error) when (error.Message == "Contact contract: " + expected) { rejected = true; }
                Require(rejected, "Contact negative control failed to detect: " + expected);
                GD.Print("ASSEMBLED_CONTACT_NEGATIVE_OK " + expected);
            }
            finally { restore(); }
            CheckFixtureContacts(view, projection, document);
        }
        RejectMutation(() => contact.RemoveChild(source), () => contact.AddChild(source),
            "exactly one source belongs to the sibling contact.");
        RejectMutation(() => source.Visible = true, () => source.Visible = false, "source must remain hidden.");
        RejectMutation(() => contact.RemoveChild(floor), () => contact.AddChild(floor),
            "exactly one projected floor belongs to the sibling contact.");
        var duplicateFloor = PaintedSurface.Create(projection, source, contact.Position);
        try
        {
            RejectMutation(() => contact.AddChild(duplicateFloor), () => contact.RemoveChild(duplicateFloor),
                "exactly one projected floor belongs to the sibling contact.");
        }
        finally { duplicateFloor.Free(); }
        var contactIndex = contact.GetIndex();
        RejectMutation(() => view.RemoveChild(contact), () => { view.AddChild(contact); view.MoveChild(contact, contactIndex); },
            "exactly one sibling contact per object.");
        var duplicateContact = new Node2D { Name = "Extra__ground_contact" };
        try
        {
            RejectMutation(() => view.AddChild(duplicateContact), () => view.RemoveChild(duplicateContact),
                "exactly one sibling contact per object.");
        }
        finally { duplicateContact.Free(); }
        var sourceScale = source.Scale;
        RejectMutation(() => source.Scale *= .5f, () => source.Scale = sourceScale,
            "source density and instance scale must apply exactly once.");
        var position = contact.Position;
        RejectMutation(() => contact.Position += Vector2.Right, () => contact.Position = position,
            "floor placement must follow the registered authored foot.");
        var sourcePosition = source.Position;
        RejectMutation(() => source.Position += Vector2.Right, () => source.Position = sourcePosition,
            "source texture must be centered on the authored foot.");
        var polygon = floor.GetChildren().OfType<Polygon2D>().FirstOrDefault();
        Require(polygon is not null, "The negative-control site must have a real projected floor polygon.");
        var relative = polygon!.ZAsRelative;
        RejectMutation(() => polygon.ZAsRelative = true, () => polygon.ZAsRelative = relative,
            "projected polygons must use absolute floor depth, never standing depth.");
        var depth = polygon.ZIndex;
        RejectMutation(() => polygon.ZIndex = standing.ZIndex, () => polygon.ZIndex = depth,
            "projected polygons must use absolute floor depth, never standing depth.");
        RejectMutation(() => { contact.RemoveChild(source); standing.AddChild(source); },
            () => { standing.RemoveChild(source); contact.AddChild(source); },
            "exactly one source belongs to the sibling contact.");
        var points = Enumerable.Range(0, mapSize.X).SelectMany(x => Enumerable.Range(0, mapSize.Y)
            .Select(y => projection.CellContact(new Coord(x, y)))).ToArray();
        var hits = points.Select(board.Pick).ToArray();
        foreach (var node in view.GetChildren().OfType<Node2D>().Where(node => node.Name.ToString().EndsWith("__ground_contact", StringComparison.Ordinal)))
            node.Visible = false;
        Require(points.Select(board.Pick).SequenceEqual(hits), "Hiding contact floors must leave every canonical cell Pick unchanged.");
        foreach (var node in view.GetChildren().OfType<Node2D>().Where(node => node.Name.ToString().EndsWith("__ground_contact", StringComparison.Ordinal)))
            node.Visible = true;
        CheckFixtureContacts(view, projection, document);
    }

    private static int ProjectionSubscriptions(BoardProjection projection)
    {
        var field = typeof(BoardProjection).GetField(nameof(BoardProjection.DepthPartitionsChanged),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return ((Delegate?)field.GetValue(projection))?.GetInvocationList().Length ?? 0;
    }

}
