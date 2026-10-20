#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

/// <summary>Draws only the target cells supplied by the session's legal-order query.</summary>
public partial class BoardOverlay : Node2D
{
    private BoardCell? _selected;
    private BoardCell? _target;
    private TacticalSelectionOverlay? _tactical;
    private BoardCell[] _unseen = [];
    private KeyValuePair<BoardCell, double>[] _incoming = [];

    public void ShowPublicState(int width, int height, IReadOnlySet<BoardCell>? visible, IReadOnlyDictionary<BoardCell, double>? incoming)
    {
        _unseen = visible is null ? [] : Enumerable.Range(0, width * height).Select(index => new BoardCell(index % width, index / width)).Where(cell => !visible.Contains(cell)).ToArray();
        _incoming = incoming?.ToArray() ?? [];
        QueueRedraw();
    }


    public void ShowTactical(TacticalSelectionOverlay projection)
    {
        _tactical = projection;
        _selected = projection.Selected;
        _target = projection.HoverTarget;
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var pair in _incoming)
        {
            DrawRect(CellRect(pair.Key).Grow(-2), new Color(new Color("#ffb020"), 0.3f));
            DrawRect(CellRect(pair.Key).Grow(-2), new Color("#ffd479"), false, 1);
            DrawString(ThemeDB.FallbackFont, new Vector2(pair.Key.X * 32 + 11, pair.Key.Y * 32 + 20), pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), fontSize: 11, modulate: new Color("#2a1a00"));
        }
        if (_tactical is { } tactical)
        {
            DrawCells(tactical.Move, new Color("#81c8d1"));
            DrawCells(tactical.Attack, new Color("#ef866b"));
            DrawCells(tactical.Helpful, new Color("#92d58d"));
            DrawCells(tactical.Threat, new Color("#bd8cdb"));
            foreach (var cell in tactical.Controlled)
                DrawRect(CellRect(cell).Grow(-6), new Color("#d7ac65"), false, 1);
            for (var index = 1; index < tactical.Path.Count; index++)
            {
                var start = Center(tactical.Path[index - 1]);
                var end = Center(tactical.Path[index]);
                DrawLine(start, end, new Color("#fff2c5"), 2, true);
                var direction = (end - start).Normalized();
                var cross = new Vector2(-direction.Y, direction.X);
                DrawLine(end, end - direction * 5 + cross * 3, new Color("#fff2c5"), 2, true);
                DrawLine(end, end - direction * 5 - cross * 3, new Color("#fff2c5"), 2, true);
            }
        }
        foreach (var cell in _unseen) DrawRect(CellRect(cell), new Color(new Color("#0b1020"), 0.55f));
        if (_selected is { } selected)
            DrawRect(CellRect(selected).Grow(-1), new Color("#ffe2a0"), false, 2);
        if (_tactical?.Cursor is { } cursor)
            DrawRect(CellRect(cursor).Grow(-1), new Color("#dce7e5"), false, 1);
        if (_target is { } target)
            DrawRect(CellRect(target).Grow(-3), Colors.White, false, 2);
    }

    private void DrawCells(IEnumerable<BoardCell> cells, Color color)
    {
        foreach (var cell in cells)
        {
            DrawRect(CellRect(cell).Grow(-2), new Color(color, 0.18f));
            DrawRect(CellRect(cell).Grow(-2), new Color(color, 0.7f), false, 1);
        }
    }
    private static Vector2 Center(BoardCell cell) => new(cell.X * 32 + 16, cell.Y * 32 + 16);
    private static Rect2 CellRect(BoardCell cell) => new(cell.X * 32, cell.Y * 32, 32, 32);
}
