#nullable enable
using Godot;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

public partial class BattleScreen
{
    private TacticalHit? PickPresentedField(CameraPoint point)
    {
        var size = _miniature is null ? _board.FieldSize : _mapArea.Size;
        if (point.X < 0 || point.Y < 0 || point.X >= size.X || point.Y >= size.Y)
            return null;
        var position = new Vector2((float)point.X, (float)point.Y);
        return _miniature is null ? _board.Pick(position) : _miniature.Pick(position);
    }
}
