#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Empire.Presentation.Tactical;
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

public partial class BoardDecorationChecks : Node
{
    private int assertions;
    public override async void _Ready()
    {
        try
        {
            var data = BoardDecorationDocument.Load();
            var oracle = PresentationJson.Load<JsonObject>("res://data/board-decorations.json");
            using var resources = new SourceBoardDecorations(data);
            foreach (var path in oracle["paths"]!.AsArray().OfType<JsonObject>())
            {
                var points = path["points"]!.AsArray().OfType<JsonObject>().Select(point => new BoardCell(point["x"]!.GetValue<int>(), point["y"]!.GetValue<int>())).ToArray();
                Require(SourceBoardDecorations.PathData(points) == path["d"]!.GetValue<string>(), "Actual source cubic path");
                var drawing = resources.Path(points)!;
                Require(drawing.Bounds.HasPoint(new(points[^1].X * 32 + 16, points[^1].Y * 32 + 16)), "Arrow remains in path bounds");
                Require(ReferenceEquals(drawing, resources.Path(points)), "Identical path uses same owned texture");
            }
            foreach (var role in data.Spots.Keys)
            {
                var drawing = resources.Spot(role);
                Require(drawing.Opacity == 1, "Fill opacity remains inside source shape");
                using var image = drawing.Texture.GetImage();
                var centre = (new Vector2(16, 21.76f) - drawing.Bounds.Position) * 2;
                Require(image.GetPixel((int)centre.X, (int)centre.Y).A > 0.1, "Source ellipse centre is drawn");
                // The centre and shadow extend beyond a square-only substitute.
                Require(drawing.Bounds.Position.X < -8 && drawing.Bounds.End.Y > 40, "Action filter has source spill");
                Require(ReferenceEquals(drawing, resources.Spot(role)), "Static shapes share texture");
            }
            Require(Math.Abs(resources.Ring("selection").Opacity - .96f) < .00001, "Selection opacity is post-filter");
            Require(resources.Ring("cursor").Opacity == 1, "Cursor source opacity");
            for (var i = 0; i < 200; i++)
            {
                var previous = resources.Path(new[] { new BoardCell(i % 20, 2), new BoardCell(i % 20 + 1, 3) });
                resources.Path(new[] { new BoardCell(i % 20, 4), new BoardCell(i % 20 + 1, 5) });
                Require(!GodotObject.IsInstanceValid(previous!.Texture), "Replaced path texture is disposed, not leaked");
                Require(resources.TextureCount <= 10, "Moving path cache remains bounded");
            }
            resources.Dispose();
            Require(resources.TextureCount == 0, "All owned decoration textures released");
            resources.Dispose();
            try { resources.Spot("move"); throw new Exception("Disposed cache resurrected"); }
            catch (ObjectDisposedException) { assertions++; }
            await Planes();
            RouteCells();
            GD.Print($"Board decoration checks passed: {assertions} assertions; source markup/path contracts, not browser pixel parity.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Planes()
    {
        var cell = new BoardCell(1, 1); var outside = new BoardCell(0, 0);
        var overlay = new BoardOverlay();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            AddChild(overlay);
            overlay.ShowPublicState(new HashSet<BoardCell> { cell }, new HashSet<BoardCell>(),
                new Dictionary<BoardCell, double> { [cell] = 2, [outside] = 1 });
            overlay.ShowTactical(new(cell, cell, new[] { cell, outside }, new[] { cell }, new[] { cell }, new[] { cell }, new[] { cell, new BoardCell(2, 1) }, outside, new[] { cell, outside }));
            Require(overlay.RangeOrder().Select(item => item.Role).SequenceEqual(new[] { "threat", "controlled", "incoming", "heal", "attack", "fog" }), "Source range ordering, move/attack exclusion and outside containment");
            Require(overlay.RangeOrder().All(item => item.Cell == cell), "No outside ground decorations");
            Require(overlay.RangeDepth < -1 && overlay.RangeDepth > -90 && overlay.ZIndex > 2500, "Source ranges/path below actors; cursor above effects");
            Require(!overlay.GetNode<Node2D>("SourceRangeAndPath").ZAsRelative, "Parent cursor depth cannot lift ranges above actors");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(overlay.OwnedTextureCount > 0 && overlay.OwnedTextureCount <= 10, "Mounted drawing uses bounded textures");
            RemoveChild(overlay);
            Require(overlay.OwnedTextureCount == 0 && overlay.GetChildCount() == 0, "Detach releases commands, planes and resources before rebind");
        }
        overlay.Free();
    }
    private void RouteCells()
    {
        using var mask = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);
        mask.FillRect(new Rect2I(0, 0, 64, 64), Colors.White);
        using var material = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        material.Fill(Colors.Red);
        var raster = CandidateRouteComposition.Compose(mask, new(0, 0, 64, 64), material, new(128, 128), Vector2.Zero);
        using (raster.Image)
        {
            var centre = (new Vector2(16, 16) - raster.Origin) * 2;
            Require(raster.Image.GetPixel((int)centre.X, (int)centre.Y).R == 1, "Route material RGB preserved inside alpha mask");
            var seam = (new Vector2(32.25f, 16) - raster.Origin) * 2;
            Require(raster.Image.GetPixel((int)seam.X, (int)seam.Y).A > .99f, "Per-cell source mask bleeds half logical pixel without sampling neighboring atlas cell");
            var shadow = (new Vector2(16, 33.25f) - raster.Origin) * 2;
            Require(raster.Image.GetPixel((int)shadow.X, (int)shadow.Y).A > 0, "Route warm one-pixel filter remains outside cell before overlap");
        }
        using var cache = new CandidateShadowCache();
        var a = CandidateRouteComposition.Create(mask, new(0, 0, 64, 64), material, new(128, 128), Vector2.Zero, "same", cache);
        var b = CandidateRouteComposition.Create(mask, new(0, 0, 64, 64), material, new(128, 128), Vector2.Zero, "same", cache);
        Require(ReferenceEquals(a.Texture, b.Texture) && cache.TextureCount == 1, "Repeated route phase shares board-owned filtered frame");
        var texture = a.Texture; a.Free(); b.Free(); cache.Dispose();
        Require(!GodotObject.IsInstanceValid(texture), "Route cache disposal releases texture after nodes");
    }
    private void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }
}
