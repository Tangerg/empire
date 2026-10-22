#nullable enable
using System;
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
        board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric), level, AuthoredScene.Load("res://data/scenes/c01-03.json"));
        board.ConfigureGround(TerrainView.Factory(WorldMaterialsManifest.Load()));
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
            Require(Pad().ZIndex == board.Projection.LayerAtDepth(board.Projection.Depth(new Vector2(target.X * 32 + 16, target.Y * 32 + 31)), BoardDepthLayer.Surface), "The pad uses the existing surface depth partition.");
            Require(board.Pick(expectedContact) == target, "Same-cell floor art must not become a standing occluder that blocks canonical picking.");
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
        before["units"]![0]!["x"] = target.X - 1;
        after["units"]![0]!["x"] = target.X + 1;
        JsonObject At(int x) => new()
        {
            ["x"] = x,
            ["y"] = target.Y
        };
        var path = new JsonArray(At(target.X - 1), At(target.X), At(target.X + 1));
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
                var logical = new Vector2((target.X - 1 + segment + eased) * 32 + 16, target.Y * 32 + 31);
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
        Require(board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}").Position == board.Projection.CellContact(new(target.X + 1, target.Y)), "Settled actor returns to the canonical endpoint.");
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
