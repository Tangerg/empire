#nullable enable
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

/// <summary>One session projection, drawn in the source range/path and cursor planes.</summary>
public partial class BoardOverlay : Node2D
{
    private TacticalSelectionOverlay? _tactical;
    private BoardCell[] _unseen = [];
    private KeyValuePair<BoardCell, double>[] _incoming = [];
    private FrozenSet<BoardCell> _interactiveCells = FrozenSet<BoardCell>.Empty;
    private BoardDecorations? _resources;
    private BoardRangePlane? _ranges;
    internal int RangeDepth => _ranges?.ZIndex ?? -2;
    internal int OwnedTextureCount => _resources?.TextureCount ?? 0;

    public override void _EnterTree()
    {
        _resources = new(BoardDecorationDocument.Load());
        ZIndex = 3000;
        TextureFilter = TextureFilterEnum.Linear;
        _ranges = new BoardRangePlane { Name = "SourceRangeAndPath", OwnerOverlay = this, ZAsRelative = false, ZIndex = -2 };
        AddChild(_ranges);
    }
    public override void _ExitTree()
    {
        // Clear retained draw commands before releasing the textures they reference.
        RenderingServer.CanvasItemClear(GetCanvasItem());
        if (_ranges is not null)
        {
            RenderingServer.CanvasItemClear(_ranges.GetCanvasItem());
            _ranges.OwnerOverlay = null;
            RemoveChild(_ranges);
            _ranges.Free();
            _ranges = null;
        }
        _resources?.Dispose();
        _resources = null;
    }
    public void ShowPublicState(IReadOnlySet<BoardCell> interactiveCells, IReadOnlySet<BoardCell>? visible,
        IReadOnlyDictionary<BoardCell, double>? incoming)
    {
        _interactiveCells = interactiveCells.ToFrozenSet();
        _unseen = visible is null ? [] : _interactiveCells.Where(cell => !visible.Contains(cell))
            .OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        _incoming = incoming?.ToArray() ?? [];
        RedrawPlanes();
    }
    public void ShowTactical(TacticalSelectionOverlay projection)
    {
        _tactical = projection;
        RedrawPlanes();
    }
    private void RedrawPlanes()
    {
        QueueRedraw();
        _ranges?.QueueRedraw();
    }
    internal bool Contains(BoardCell cell) => _interactiveCells.Contains(cell);
    internal IEnumerable<(string Role, BoardCell Cell)> RangeOrder()
    {
        if (_tactical is { } before)
        {
            foreach (var cell in before.Threat.Where(Contains))
                yield return ("threat", cell);
            foreach (var cell in before.Controlled.Where(Contains))
                yield return ("controlled", cell);
        }
        foreach (var pair in _incoming.Where(pair => Contains(pair.Key)))
            yield return ("incoming", pair.Key);
        if (_tactical is { } tactical)
        {
            foreach (var cell in tactical.Move.Where(cell => Contains(cell) && !tactical.Attack.Contains(cell)))
                yield return ("move", cell);
            foreach (var cell in tactical.Helpful.Where(Contains))
                yield return ("heal", cell);
            foreach (var cell in tactical.Attack.Where(Contains))
                yield return ("attack", cell);
        }
        foreach (var cell in _unseen.Where(Contains))
            yield return ("fog", cell);
    }
    internal void DrawRanges(Node2D canvas)
    {
        if (_resources is null)
            return;
        foreach (var (role, cell) in RangeOrder())
        {
            DrawDecoration(canvas, _resources.Spot(role), new(cell.X * 32, cell.Y * 32));
            if (role == "incoming")
            {
                var text = _incoming.First(pair => pair.Key == cell).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var font = ThemeDB.FallbackFont;
                var width = font.GetStringSize(text, fontSize: 11).X;
                canvas.DrawString(font, new(cell.X * 32 + 16 - width / 2, cell.Y * 32 + 20), text, fontSize: 11, modulate: new Color("#2a1a00"));
            }
        }
        if (_tactical is { Path.Count: >= 2 } tactical && _resources.Path(tactical.Path) is { } path)
            DrawDecoration(canvas, path, Vector2.Zero);
    }
    public override void _Draw()
    {
        if (_resources is null)
            return;
        if (_tactical?.Selected is { } selected && Contains(selected))
            DrawDecoration(this, _resources.Ring("selection"), new(selected.X * 32, selected.Y * 32));
        if (_tactical?.Cursor is { } cursor && Contains(cursor))
            DrawDecoration(this, _resources.Ring("cursor"), new(cursor.X * 32, cursor.Y * 32));
    }
    private static void DrawDecoration(Node2D canvas, BoardDecorations.Drawing drawing, Vector2 offset) =>
        canvas.DrawTextureRect(drawing.Texture, new(drawing.Bounds.Position + offset, drawing.Bounds.Size), false, new Color(1, 1, 1, drawing.Opacity));
}
