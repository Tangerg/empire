#nullable enable
using System;
using Empire.Presentation.Tactical;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Native floor-art registration, lifecycle and shared-depth regression fixture.</summary>
public partial class OathPadChecks : Node
{
    private int assertions;
    private string? captureDirectory;

    public override async void _Ready()
    {
        try
        {
            captureDirectory = OS.GetCmdlineUserArgs().SingleOrDefault(value => value.StartsWith("--capture-dir=", StringComparison.Ordinal))?["--capture-dir=".Length..];
            if (captureDirectory is not null)
            {
                Require(Path.IsPathFullyQualified(captureDirectory) && DisplayServer.GetName() != "headless", "Captures require an absolute destination and a real native display.");
                Directory.CreateDirectory(captureDirectory);
            }
            await Run();
            GD.Print($"Oath pad native checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task Run()
    {
        var assets = AssetCatalog.Load();
        CheckBindings();
        CheckRegistration(assets);
        CheckVisibleSurfaceSelection(assets);
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var structure = state["structures"]![0]!.AsObject();
        var target = new Coord(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
        var unit = (JsonObject)state["units"]![0]!.DeepClone();
        var id = unit["id"]!.GetValue<int>();
        unit["x"] = target.X;
        unit["y"] = target.Y;
        // A detached presentation fixture, not a game-state transition or campaign edit.
        state["units"] = new JsonArray(unit);
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
        board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
        board.ConfigureGround(rendering.Ground);
        board.ConfigureDynamicState();
        board.RenderState(state);
        CheckSurfaceSources(board.Projection, assets);
        board.Scale = Vector2.One * 5;
        board.Position = new Vector2(640, 400) - board.Projection.CellContact(target) * 5;
        Node2D Pad() => board.GetNode<Node2D>("CanonicalProjection/DynamicState/Structure_0");
        Node2D Actor() => board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}");
        var expectedContact = board.Projection.CellContact(target);
        var cases = new[] { ("normal", 100, false, 0), ("captured", 100, false, 2), ("damaged", 50, false, 2), ("disabled", 100, true, 2), ("destroyed", 0, true, 2) };
        foreach (var (name, hp, disabled, owner) in cases)
        {
            structure["hp"] = hp;
            structure["disabled"] = disabled;
            structure["owner"] = owner;
            var before = state.ToJsonString();
            board.RenderState(state);
            Require(before == state.ToJsonString(), "Rendering cannot change canonical ownership, health or occupancy.");
            Require(Pad().Position.IsEqualApprox(expectedContact) && Actor().Position.IsEqualApprox(expectedContact), "Every physical state keeps pad center and same-cell actor on the shared canonical contact.");
            Require(Pad().GetMeta("visual_state").AsString() == name, "The logical lifecycle chooses the matching physical state.");
            Require(Pad().ZIndex < Actor().ZIndex, "Floor art must draw below same-cell shoes, not win a standing tie.");
            Require(Pad().ZIndex == board.Projection.LayerAtDepth(board.Projection.Depth(board.Projection.LogicalCellContact(target)), BoardDepthLayer.Surface), "The pad uses the existing surface depth partition.");
            var actorBody = Actor().GetNode<Sprite2D>("AuthoredSprite");
            var contactHit = actorBody.IsPixelOpaque(actorBody.ToLocal(board.ToGlobal(expectedContact)))
                ? TacticalHit.Unit(new(target.X, target.Y), id) : TacticalHit.Object(new(target.X, target.Y));
            Require(board.Pick(expectedContact) == contactHit, "Same-cell floor art must preserve the visible actor or surface-object subject, never become a standing blocker.");
            var active = !disabled && hp > 0;
            Require(Pad().HasNode("HealthGauge") == active && Pad().HasNode("OwnerMark") == (active && owner != 0) && !Pad().HasNode("OwnerBanner"), "Surface state decoration is flat, derived, and absent when inactive.");
            await Capture(name + "-occupied");
            await CaptureLogicalScale(board, expectedContact, name + "-occupied-1x");
            Actor().Visible = false;
            await Capture(name + "-unoccupied");
            await CaptureLogicalScale(board, expectedContact, name + "-unoccupied-1x");
        }
        structure["hp"] = 100;
        structure["disabled"] = false;
        await CheckTraversal(board, state, target, id);
        // A standing binding must retain occlusion; floor depth is not a global shortcut.
        structure["type"] = "gate";
        board.RenderState(state);
        Require(Pad().ZIndex >= Actor().ZIndex && Pad().HasNode("OwnerBanner") && !Pad().HasNode("OwnerMark"), "Standing structures retain standing depth and ownership decoration.");
        board.Free();
    }

    private void CheckVisibleSurfaceSelection(AssetCatalog assets)
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var source = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(source);
        var level = (JsonObject)source.DeepClone();
        level["width"] = 7;
        level["height"] = 7;
        var map = state["map"]!.AsObject();
        map["width"] = 7;
        map["height"] = 7;
        map["tiles"] = new JsonArray(Enumerable.Range(0, 49).Select(_ => (JsonNode?)JsonValue.Create("plain")).ToArray());
        foreach (var name in new[] { "owners", "elevation", "captureProgress" })
            map[name] = new JsonArray(Enumerable.Range(0, 49).Select(_ => (JsonNode?)JsonValue.Create(0)).ToArray());
        map["cliffs"] = new JsonArray();
        map["directionalCover"] = new JsonArray();
        var structure = (JsonObject)state["structures"]![0]!.DeepClone();
        structure["x"] = 3;
        structure["y"] = 3;
        structure["owner"] = 0;
        state["structures"] = new JsonArray(structure);
        var unit = (JsonObject)state["units"]![0]!.DeepClone();
        unit["x"] = 3;
        unit["y"] = 4;
        state["units"] = new JsonArray();
        var board = new BoardView();
        AddChild(board);
        try
        {
            // A 24 by 27 projected cell exposes the real pad's lateral overhang.
            board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog,
                (size, _, _) => new BoardPlane(new(.75f, 0), new(0, 27f / 32), Vector2.Zero, size)),
                level, new AuthoredScene { LevelId = "c01-03", MapSize = [7, 7], RuntimeReady = true, FramePlacements = [] });
            board.ConfigureDynamicState();
            var target = new Coord(3, 3);
            foreach (var (hp, disabled) in new[] { (100, false), (50, false), (100, true), (0, true) })
            {
                structure["hp"] = hp;
                structure["disabled"] = disabled;
                var before = state.ToJsonString();
                board.RenderState(state);
                var pad = board.GetNode<Node2D>("CanonicalProjection/DynamicState/Structure_0");
                var sprite = pad.GetChildren().OfType<Sprite2D>().First();
                var body = pad.GetChildren().OfType<PaintedSurface>().First();
                var center = board.Projection.CellContact(target);
                Require(center == board.Projection.CellCenter(target) && pad.Position == center,
                    "Surface registration and canonical cell center must agree.");
                var overhang = 0;
                var transparent = 0;
                var occupied = 0;
                var visibleSelection = true;
                var transparentSelection = true;
                for (var y = -9f; y <= 9; y += .5f)
                    for (var x = -15f; x <= 15; x += .5f)
                    {
                        var point = center + new Vector2(x, y);
                        var opaque = sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(point)));
                        if (opaque)
                        {
                            occupied++;
                            visibleSelection &= body.HitPolygon(board.ToGlobal(point)) is not null && board.Pick(point) == TacticalHit.Object(new(target.X, target.Y));
                            if (board.Projection.Pick(point)?.Cell != target)
                                overhang++;
                        }
                        else
                        {
                            transparent++;
                            var ground = board.Projection.Pick(point)?.Cell;
                            transparentSelection &= board.Pick(point) == (ground is { } cell ? TacticalHit.Ground(new(cell.X, cell.Y)) : null);
                        }
                    }
                Require(visibleSelection, "Every visible surface pixel, including its overhang, must select the owning cell.");
                Require(transparentSelection, "Transparent source canvas must not create a surface-object hitbox.");
                Require(overhang > 0 && occupied > 100 && transparent > 100,
                    "Each physical state must exercise visible overhang and transparent canvas, not only its center.");
                Require(before == state.ToJsonString(), "Pixel selection must not mutate canonical state.");
            }
            state["units"] = new JsonArray(unit);
            board.RenderState(state);
            var actor = board.GetNode<Node2D>($"CanonicalProjection/Unit_{unit["id"]!.GetValue<int>()}");
            var actorSprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var padCenter = board.Projection.CellContact(new(3, 3));
            var overlap = 0;
            var unitSelection = true;
            for (var y = -6f; y <= 6; y += .5f)
                for (var x = -13f; x <= 13; x += .5f)
                {
                    var point = padCenter + new Vector2(x, y);
                    if (!actorSprite.IsPixelOpaque(actorSprite.ToLocal(board.ToGlobal(point))))
                        continue;
                    overlap++;
                    unitSelection &= board.Pick(point) == TacticalHit.Unit(new(3, 4), unit["id"]!.GetValue<int>());
                }
            Require(unitSelection, "Visible unit pixels must win over a neighboring surface overhang.");
            Require(overlap > 0, "The unit-occlusion fixture must overlap actual source body pixels.");
            state["units"] = new JsonArray();
            board.RenderState(state);
            using var image = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            image.SetPixel(1, 1, Colors.White);
            using var texture = ImageTexture.CreateFromImage(image);
            var foreground = new Sprite2D { Texture = texture, Position = padCenter };
            board.GetNode<Node2D>("AuthoredScene/AuthoredFrame").AddChild(foreground);
            Require(board.Pick(padCenter) is null, "Opaque foreground pixels must block a visible surface target.");
            foreground.Modulate = new Color(1, 1, 1, 0);
            Require(board.Pick(padCenter) == TacticalHit.Object(new(3, 3)), "A zero-opacity foreground must not block the visible surface target.");
            foreground.Modulate = Colors.White;
            Require(board.Pick(padCenter + new Vector2(-1, 0)) == TacticalHit.Object(new(3, 3)),
                "Transparent foreground canvas must not block the surface target.");
            foreground.Free();
        }
        finally { board.Free(); }
    }

    private void CheckBindings()
    {
        var manifest = PresentationJson.Load<AssetManifest>("res://data/selected-assets.json");
        var document = PresentationJson.Load<JsonObject>("res://data/art-bindings.json");
        void Reject(Action<JsonObject> edit)
        {
            var copy = (JsonObject)document.DeepClone();
            edit(copy["CANDIDATE_01_STRUCTURE_ART"]!["command_node"]!.AsObject());
            var rejected = false;
            try
            {
                var bindings = JsonSerializer.Deserialize<ArtBindings>(copy.ToJsonString(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
                _ = new AssetCatalog(manifest, bindings);
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) { rejected = true; }
            Require(rejected, "Missing/unknown depth and missing lifecycle frames must fail admission, not default to standing or a different frame.");
        }
        Reject(binding => binding.Remove("depthRole"));
        Reject(binding => binding["depthRole"] = "range");
        Reject(binding => binding["depthRole"] = null);
        Reject(binding => binding.Remove("disabled"));
        Reject(binding => binding["destroyed"]!["topicId"] = "absent-art");
    }

    private void CheckSurfaceSources(BoardProjection projection, AssetCatalog assets)
    {
        var art = assets.ResolveStructure("command_node", StructureVisualState.Normal)!.Value;
        void Reject(Action<Sprite2D> edit)
        {
            var sprite = assets.CreateSprite(art.Asset, art.Frame);
            var rejected = false;
            try
            {
                edit(sprite);
                PaintedSurface.Create(projection, sprite, Vector2.Zero).Free();
            }
            catch (InvalidOperationException) { rejected = true; }
            finally { sprite.Free(); }
            Require(rejected, "Unsupported sprite regions, frame grids, flips and cropped atlases must fail explicitly.");
        }
        Reject(sprite => sprite.RegionEnabled = true);
        Reject(sprite => sprite.Hframes = 2);
        Reject(sprite => sprite.Vframes = 2);
        Reject(sprite => sprite.FlipH = true);
        Reject(sprite => sprite.FlipV = true);
        var full = (AtlasTexture)assets.CreateFrameTexture(art.Asset, 0);
        using var cropped = new AtlasTexture { Atlas = full.Atlas, Region = new Rect2(1, 0, full.GetWidth() - 1, full.GetHeight()) };
        Reject(sprite => sprite.Texture = cropped);
    }

    private void CheckRegistration(AssetCatalog assets)
    {
        var physicalPaths = Enum.GetValues<StructureVisualState>().Select(state => assets.ResolveStructure("command_node", state)!.Value.Asset.ResourcePath).Distinct().Count();
        Require(physicalPaths == 4, "Five states require four physical originals, with captured explicitly reusing normal.");
        Require(assets.ResolveStructure("command_node", StructureVisualState.Normal) == assets.ResolveStructure("command_node", StructureVisualState.Captured), "Captured must reuse exactly the normal image and registration.");
        foreach (var state in Enum.GetValues<StructureVisualState>())
        {
            var art = assets.ResolveStructure("command_node", state)!.Value;
            Require(art.Depth == BoardDepthLayer.Surface && art.Asset.Footprint == Vector2I.One, "Every node state has one-cell surface art, never a tall console footprint.");
            using var image = assets.CreateFrameTexture(art.Asset, 0).GetImage();
            var min = new Vector2I(image.GetWidth(), image.GetHeight());
            var max = new Vector2I(-1, -1);
            for (var y = 0; y < image.GetHeight(); y++)
                for (var x = 0; x < image.GetWidth(); x++)
                    if (image.GetPixel(x, y).A * 255 > 8)
                    {
                        min = new Vector2I(Math.Min(min.X, x), Math.Min(min.Y, y));
                        max = new Vector2I(Math.Max(max.X, x), Math.Max(max.Y, y));
                    }
            var size = (Vector2)(max - min + Vector2I.One) * art.Asset.PresentationScale;
            Require(Math.Abs(size.X - 26) < .06 && size.Y is > 12.8f and < 13.3f, "Measured nontrivial alpha must register a common ~26 by 13 low footprint using uniform scale.");
            var center = art.Asset.Anchor;
            Require(center.X > min.X && center.X < max.X && center.Y > min.Y && center.Y < max.Y && image.GetPixel((int)center.X, (int)center.Y).A > .9f, "Walkable center anchor must lie on the plate, not the front rim or transparent canvas.");
            var top = ((Vector2)min - center) * art.Asset.PresentationScale;
            var bottom = ((Vector2)(max + Vector2I.One) - center) * art.Asset.PresentationScale;
            Require(top.Y < -5 && bottom.Y > 6 && Math.Abs((top.X + bottom.X) / 2) < .15f, "Per-state registration centers the physical pad under actor shoes without stretching or rim anchoring.");
        }
    }

    private async Task CheckTraversal(BoardView board, JsonObject state, Coord target, int id)
    {
        var before = (JsonObject)state.DeepClone();
        var after = (JsonObject)state.DeepClone();
        // Cross the authored depth axis: Redstone's registered painting is not a default dimetric plane.
        before["units"]![0]!["y"] = target.Y - 1;
        after["units"]![0]!["y"] = target.Y + 1;
        JsonObject At(int y) => new()
        {
            ["x"] = target.X,
            ["y"] = y
        };
        var path = new JsonArray(At(target.Y - 1), At(target.Y), At(target.Y + 1));
        var action = new JsonObject { ["kind"] = "command", ["unit"] = id, ["path"] = path, ["command"] = new JsonObject { ["ability"] = "wait" } };
        var transition = new BattlePresentationTransition(1, EcmaJson.ToElement(before), EcmaJson.ToElement(action), [], EcmaJson.ToElement(after), false);
        var task = board.PresentTransitionAsync(transition);
        var actor = board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}");
        var pad = board.GetNode<Node2D>("CanonicalProjection/DynamicState/Structure_0");
        var origin = pad.Position;
        var overlapSamples = 0;
        var unsplitDepthFailures = 0;
        for (var segment = 0; segment < 2; segment++)
        {
            for (var tick = 1; tick <= 10; tick++)
            {
                board.AdvancePresentation(tick == 10 ? .00850001 : .0085);
                await Task.Yield();
                actor = board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}");
                pad = board.GetNode<Node2D>("CanonicalProjection/DynamicState/Structure_0");
                var t = tick / 10f;
                var eased = t < .5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2;
                var logical = board.Projection.LogicalCellContact(new(target.X, target.Y - 1 + segment)).Lerp(
                    board.Projection.LogicalCellContact(new(target.X, target.Y + segment)), eased);
                var expected = board.Projection.Project(logical) - new Vector2(0, MathF.Sin(MathF.PI * t) * 2.5f);
                Require(actor.Position.DistanceTo(expected) < .02f && pad.Position == origin, "Traversal follows the existing canonical interpolation and authored walking bob, with no pad-induced jump or motion.");
                var floorContact = board.Projection.Project(logical);
                var image = pad.GetChildren().OfType<Sprite2D>().First();
                if (image.IsPixelOpaque(image.ToLocal(board.ToGlobal(floorContact))))
                {
                    overlapSamples++;
                    if (pad.ZIndex >= actor.ZIndex)
                        unsplitDepthFailures++;
                    var strips = pad.GetChildren().OfType<PaintedSurface>().SelectMany(surface => surface.GetChildren().OfType<Polygon2D>())
                        .Where(strip => strip.Polygons.Any(piece => Geometry2D.IsPointInPolygon(
                            strip.ToLocal(board.ToGlobal(floorContact)), piece.AsInt32Array().Select(index => strip.Polygon[index]).ToArray()))).ToArray();
                    Require(strips.Length > 0 && strips.All(strip => strip.ZIndex < actor.ZIndex), "The actual surface pieces under moving shoes must remain below the actor, including the rear half of the pad.");
                }
                if (tick is 3 or 5 or 7 or 10)
                    await Capture($"traversal-{segment}-{tick}");
            }
        }
        board.AdvancePresentation(.001);
        await Task.Yield();
        Require(unsplitDepthFailures > 0, "The fixture must expose the prior whole-sprite center-depth failure on an actual overlapping shoe sample.");
        Require(overlapSamples >= 4, "Traversal must sample real plate pixels, not only empty neighboring cells.");
        Require(task.IsCompleted && await task, "Traversal must settle through the real native presentation timeline.");
        Require(board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}").Position == board.Projection.CellContact(new(target.X, target.Y + 1)), "Settled actor returns to the canonical endpoint.");
    }

    private async Task CaptureLogicalScale(BoardView board, Vector2 contact, string name)
    {
        if (captureDirectory is null)
            return;
        var scale = board.Scale;
        var position = board.Position;
        board.Scale = Vector2.One;
        board.Position = new Vector2(640, 400) - contact;
        await Capture(name);
        board.Scale = scale;
        board.Position = position;
    }

    private async Task Capture(string name)
    {
        if (captureDirectory is null)
            return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.Combine(captureDirectory, name + ".png")) == Error.Ok, "Native viewport capture must succeed.");
    }

    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
}
