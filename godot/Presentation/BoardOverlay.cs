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
    private BoardProjection? _projection;
    private BoardCell[] _unseen = [];
    private KeyValuePair<BoardCell, double>[] _incoming = [];
    private FrozenSet<BoardCell> _interactiveCells = FrozenSet<BoardCell>.Empty;
    private BoardDecorations? _resources;
    private readonly List<BoardRangePlane> _ranges = new();
    private readonly Dictionary<int, BoardSurfacePatch[]> _rangePatches = new();
    private readonly Dictionary<int, (int Start, int End)> _rangeIntervals = new();
    internal int RangeDepth => _ranges.Count == 0 ? -2 : _ranges[0].ZIndex;
    internal int OwnedTextureCount => _resources?.TextureCount ?? 0;

    public override void _EnterTree()
    {
        _resources = new(BoardDecorationDocument.Load());
        ZIndex = 3000;
        TextureFilter = TextureFilterEnum.Linear;
        if (_projection is not null)
            _projection.DepthPartitionsChanged += RebuildRangePlanes;
        RebuildRangePlanes();
    }
    public override void _ExitTree()
    {
        // Clear retained draw commands before releasing the textures they reference.
        RenderingServer.CanvasItemClear(GetCanvasItem());
        if (_projection is not null)
            _projection.DepthPartitionsChanged -= RebuildRangePlanes;
        ClearRangePlanes();
        _resources?.Dispose();
        _resources = null;
    }
    public void ShowPublicState(BoardProjection projection, IReadOnlySet<BoardCell> interactiveCells, IReadOnlySet<BoardCell>? visible,
        IReadOnlyDictionary<BoardCell, double>? incoming)
    {
        var changed = !ReferenceEquals(_projection, projection);
        System.ArgumentNullException.ThrowIfNull(projection);
        if (changed && IsInsideTree() && _projection is not null)
            _projection.DepthPartitionsChanged -= RebuildRangePlanes;
        _projection = projection;
        if (changed && IsInsideTree())
        {
            _projection.DepthPartitionsChanged += RebuildRangePlanes;
            RebuildRangePlanes();
        }
        var unseen = visible is null ? System.Array.Empty<BoardCell>() : interactiveCells.Where(cell => !visible.Contains(cell))
            .OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        var nextIncoming = incoming?.ToArray() ?? [];
        var rangesChanged = changed || !_interactiveCells.SetEquals(interactiveCells) || !_unseen.SequenceEqual(unseen) || !_incoming.SequenceEqual(nextIncoming);
        _interactiveCells = interactiveCells.ToFrozenSet();
        _unseen = unseen;
        _incoming = nextIncoming;
        if (rangesChanged)
            RedrawPlanes();
    }
    public void ShowTactical(TacticalSelectionOverlay projection)
    {
        var before = _tactical;
        _tactical = projection;
        if (before is null || before.Selected != projection.Selected || before.Cursor != projection.Cursor)
            QueueRedraw();
        if (before is null || !before.Move.SequenceEqual(projection.Move)
            || !before.Attack.SequenceEqual(projection.Attack) || !before.Helpful.SequenceEqual(projection.Helpful)
            || !before.Controlled.SequenceEqual(projection.Controlled) || !before.Threat.SequenceEqual(projection.Threat)
            || !before.Path.SequenceEqual(projection.Path))
            foreach (var plane in _ranges)
                plane.QueueRedraw();
    }
    private void RedrawPlanes()
    {
        QueueRedraw();
        foreach (var plane in _ranges)
            plane.QueueRedraw();
    }
    private void ClearRangePlanes()
    {
        foreach (var plane in _ranges)
        {
            RenderingServer.CanvasItemClear(plane.GetCanvasItem());
            plane.OwnerOverlay = null;
            RemoveChild(plane);
            plane.Free();
        }
        _ranges.Clear();
        _rangePatches.Clear();
        _rangeIntervals.Clear();
    }
    private void RebuildRangePlanes()
    {
        if (_projection is null)
        {
            ClearRangePlanes();
            return;
        }
        var planes = new List<BoardRangePlane>();
        var candidates = new Dictionary<int, BoardSurfacePatch[]>();
        var intervals = new Dictionary<int, (int Start, int End)>();
        try
        {
            foreach (var partition in _projection.DepthPartitions)
            {
                var depth = _projection.LayerAtDepth(partition.StartDepth, BoardDepthLayer.Range);
                candidates.Add(depth, partition.Patches.ToArray());
                intervals.Add(depth, (partition.StartDepth, partition.EndDepthExclusive));
                planes.Add(new BoardRangePlane
                {
                    Name = $"SourceRangeAndPath_{depth}",
                    OwnerOverlay = this,
                    ZAsRelative = false,
                    ZIndex = depth,
                });
            }
        }
        catch
        {
            foreach (var plane in planes)
                plane.Free();
            throw;
        }
        ClearRangePlanes();
        foreach (var pair in candidates)
            _rangePatches.Add(pair.Key, pair.Value);
        foreach (var pair in intervals)
            _rangeIntervals.Add(pair.Key, pair.Value);
        foreach (var plane in planes)
        {
            _ranges.Add(plane);
            AddChild(plane);
        }
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
        if (_resources is null || _projection is null)
            return;
        foreach (var group in RangeOrder().GroupBy(item => item.Role))
            BoardDecorations.DrawProjected(canvas, _resources.Spot(group.Key),
                group.Select(item => new Vector2(item.Cell.X * 32, item.Cell.Y * 32)).ToArray(), _rangePatches[canvas.ZIndex]);
        foreach (var (role, cell) in RangeOrder())
        {
            if (role == "incoming" && _projection.Depth(new(cell.X * 32 + 16, cell.Y * 32 + 16)) >= _rangeIntervals[canvas.ZIndex].Start
                && _projection.Depth(new(cell.X * 32 + 16, cell.Y * 32 + 16)) < _rangeIntervals[canvas.ZIndex].End)
            {
                var text = _incoming.First(pair => pair.Key == cell).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var font = ThemeDB.FallbackFont;
                var width = font.GetStringSize(text, fontSize: 11).X;
                canvas.DrawString(font, _projection.Project(new(cell.X * 32 + 16, cell.Y * 32 + 20)) - new Vector2(width / 2, 0), text, fontSize: 11, modulate: new Color("#2a1a00"));
            }
        }
        if (_tactical is { Path.Count: >= 2 } tactical && _resources.Path(tactical.Path) is { } path)
            DrawDecoration(canvas, path, Vector2.Zero);
    }
    public override void _Draw()
    {
        if (_resources is null || _projection is null)
            return;
        if (_tactical?.Selected is { } selected && Contains(selected))
            DrawDecoration(this, _resources.Ring("selection"), new(selected.X * 32, selected.Y * 32));
        if (_tactical?.Cursor is { } cursor && Contains(cursor))
            DrawDecoration(this, _resources.Ring("cursor"), new(cursor.X * 32, cursor.Y * 32));
    }
    private void DrawDecoration(Node2D canvas, BoardDecorations.Drawing drawing, Vector2 offset)
    {
        if (_projection is not null)
            BoardDecorations.DrawProjected(canvas, drawing, [offset],
                canvas == this ? _projection.DepthPatches : _rangePatches[canvas.ZIndex]);
    }
}
