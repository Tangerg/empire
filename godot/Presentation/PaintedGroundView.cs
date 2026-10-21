#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

public partial class PaintedGroundView : BoardGroundView
{
    private Node2D? _authored;
    private PaintedGroundDocument _document = null!;
    private string[] _tiles = [];
    private int[] _elevation = [];
    public override IReadOnlyList<string> Diagnostics => Array.Empty<string>();
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => Array.Empty<TerrainDepthEntry>();

    public static PaintedGroundView Create(PaintedGroundDocument document, Texture2D borrowedTexture, JsonObject registeredMap)
    {
        var tiles = registeredMap["tiles"]!.AsArray().Select(value => EcmaJson.ReadString(value)).ToArray();
        var heights = registeredMap["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray();
        if (heights.Any(height => height != 0) || registeredMap["cliffs"]!.AsArray().Count != 0)
            throw new InvalidOperationException("The painted valley requires the registered flat, cliff-free playable surface.");
        var view = new PaintedGroundView { Name = "PaintedGround", _document = document, _tiles = tiles, _elevation = heights };
        try
        {
            view.AddChild(new Sprite2D
            {
                Name = "EnvironmentPainting",
                Texture = borrowedTexture,
                Centered = false,
                Scale = Vector2.One * document.WorldScale,
                ZIndex = -4096
            });
            return view;
        }
        catch
        {
            view.Free();
            throw;
        }
    }

    public override void ConfigureAuthoredGround(Node2D authoredGround)
    {
        if (_authored is not null || authoredGround.GetParent() is not null || authoredGround.GetChildCount() != 0)
            throw new InvalidOperationException("Painted ground cannot silently absorb material-ground decorations.");
        AddChild(authoredGround);
        _authored = authoredGround;
    }

    public override Node2D DetachAuthoredGround()
    {
        var authored = _authored ?? throw new InvalidOperationException("No authored ground is attached.");
        RemoveChild(authored);
        _authored = null;
        return authored;
    }

    public override void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size)
    {
        if (!_tiles.SequenceEqual(tiles) || !_elevation.SequenceEqual(elevation))
            throw new InvalidOperationException("Canonical terrain changed without a matching authored painted surface.");
        var plane = _document.CreatePlane(new Vector2(size.X, size.Y) * BoardProjection.TileSize, 0, 0);
        if (projection.Size != plane.CanvasSize || !projection.Project(Vector2.Zero).IsEqualApprox(plane.Origin))
            throw new InvalidOperationException("Painted surface and tactical projection use different registration.");
    }
}
