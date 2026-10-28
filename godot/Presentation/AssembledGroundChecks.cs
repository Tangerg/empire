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
    private int assertions;
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    public override async void _Ready()
    {
        try { await Check(); GD.Print($"ASSEMBLED_GROUND_CHECKS_OK {assertions} assertions"); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Check()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var level = BattleTextChecks.OpeningLevel();
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        var map = state["map"]!.AsObject();
        var sites = new[] { new Coord(3, 16), new Coord(18, 13), new Coord(22, 16), new Coord(34, 13) };
        SceneObjectPlacement Site(Coord cell) => new() { Id = $"Site_{cell.X}_{cell.Y}", AssetId = "house",
            Contact = [cell.X + .5, cell.Y + .5], Scale = 1, SelectionCell = [cell.X, cell.Y], OwnsMapStructureBody = true, Pointer = "target" };
        SceneObjectPlacement Tree(double x, string pointer = "pass-through") => new() { Id = "Tree", AssetId = "house",
            Contact = [x, 20.5], Scale = .5f, SelectionCell = null, OwnsMapStructureBody = false, Pointer = pointer };
        AssembledGroundDocument Document(params SceneObjectPlacement[] instances) => new()
        {
            LevelId = "c01-01", LevelSourceSha256 = EcmaJson.Sha256(level), ResourcePath = "fixture-clean-ground",
            PixelSize = [2048, 2048], WorldScale = 1, OriginPixels = [64, 256], CellXPixels = [32, 0], CellYPixels = [0, 32],
            ObjectCatalogPath = "fixture-catalog", Objects = instances
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
        var catalog = new SceneObjectCatalog { Schema = 1, Objects = new()
        {
            ["house"] = new SceneObjectAsset { ResourcePath = "fixture-independent.png", PixelSize = [64, 96], AnchorPixels = [32, 88], GroundFrontPixels = [32, 88], ContactShadowRadiiPixels = [15, 4] }
        } };
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
            var action = new JsonObject { ["kind"] = "command", ["unit"] = unitId, ["path"] = path.DeepClone(),
                ["command"] = new JsonObject { ["ability"] = "wait" } };
            var movement = new JsonObject { ["type"] = "move", ["unit"] = unitId, ["path"] = path };
            var transition = new BattlePresentationTransition(1, EcmaJson.ToElement(state), EcmaJson.ToElement(action),
                [EcmaJson.ToElement(movement)], EcmaJson.ToElement(after), false);
            var playback = board.PresentTransitionAsync(transition);
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
            var oldShadow = tree.GetNode<Sprite2D>("SourceStructureContact");
            Require(oldShadow.ShowBehindParent && oldShadow.Scale == Vector2.One / 2
                && Mathf.IsEqualApprox(oldShadow.GetGlobalTransform().X.Length(), .25f)
                && oldShadow.ToGlobal(oldShadow.Texture.GetSize() / 2).IsEqualApprox(tree.GlobalPosition),
                "Contact shadow is centered on the authored foot and inherits instance scale exactly once.");
            Bind(Document(sites.Select(Site).Append(Tree(8.5)).ToArray()));
            var moved = view!.GetNode<Sprite2D>("Tree");
            var movedShadow = moved.GetNode<Sprite2D>("SourceStructureContact");
            Require(!oldShadow.IsInsideTree() && movedShadow.GetParent() == moved
                && movedShadow.ToGlobal(movedShadow.Texture.GetSize() / 2).IsEqualApprox(moved.GlobalPosition),
                "Moving/replacing an object removes its previous contact shadow with the old subtree.");
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
        }
        finally { RemoveChild(board); board.Free(); }
        void Reject(AssembledGroundDocument invalid, string reason)
        {
            var rejected = false;
            try { AssembledGroundView.Create(invalid, ground, catalog, textures, map, engine.Catalog).Free(); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException) { rejected = true; }
            Require(rejected && IsInstanceValid(ground) && IsInstanceValid(house), reason);
        }
        Reject(Document(Site(sites[0]), Site(sites[0])), "Duplicate instance/site ownership is rejected without disposing borrowed art.");
        Reject(Document(Tree(-1000)), "Decorative contacts may leave the map but not the registered canvas.");
        Reject(Document(Tree(2, "automatic")), "Pointer semantics must be explicit.");
        Reject(Document(new SceneObjectPlacement { Id = "invalid", AssetId = "house", Contact = [1, 1], Scale = 1,
            SelectionCell = null, OwnsMapStructureBody = true, Pointer = "target" }), "A structure body cannot own an absent canonical site.");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonNode.Parse(JsonSerializer.Serialize(document, options))!.AsObject();
        json["occluders"] = new JsonArray();
        var rejectedOldPatches = false;
        try { JsonSerializer.Deserialize<AssembledGroundDocument>(json.ToJsonString(), options); }
        catch (JsonException) { rejectedOldPatches = true; }
        Require(rejectedOldPatches, "Assembled documents reject obsolete background-patch occluders rather than silently combining renderers.");
    }
}
