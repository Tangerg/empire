#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Draws only the target cells supplied by the session's legal-order query.</summary>
public partial class BoardOverlay : Node2D
{
    private BoardCell? _selected;
    private BoardCell? _target;
    private BoardCell[] _targets = [];
    private string _role = "";
    private BoardCell[] _unseen = [];
    private KeyValuePair<BoardCell, double>[] _incoming = [];

    public void ShowPublicState(int width, int height, IReadOnlySet<BoardCell>? visible, IReadOnlyDictionary<BoardCell, double>? incoming)
    {
        _unseen = visible is null ? [] : Enumerable.Range(0, width * height).Select(index => new BoardCell(index % width, index / width)).Where(cell => !visible.Contains(cell)).ToArray();
        _incoming = incoming?.ToArray() ?? [];
        QueueRedraw();
    }


    public void ShowTargets(BoardCell? selected, IEnumerable<BoardCell> targets,
        string role, BoardCell? target)
    {
        _selected = selected;
        _targets = targets.Distinct().ToArray();
        _role = role;
        _target = target;
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
        var color = _role == "attack" ? new Color("#ef866b") : new Color("#81c8d1");
        foreach (var cell in _targets)
        {
            var rect = CellRect(cell).Grow(-2);
            DrawRect(rect, new Color(color, 0.18f));
            DrawRect(rect, new Color(color, 0.7f), false, 1);
        }
        foreach (var cell in _unseen) DrawRect(CellRect(cell), new Color(new Color("#0b1020"), 0.55f));
        if (_selected is { } selected)
            DrawRect(CellRect(selected).Grow(-1), new Color("#ffe2a0"), false, 2);
        if (_target is { } target)
            DrawRect(CellRect(target).Grow(-3), Colors.White, false, 2);
    }

    private static Rect2 CellRect(BoardCell cell) => new(cell.X * 32, cell.Y * 32, 32, 32);
}
