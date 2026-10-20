#nullable enable
using Godot;
namespace Empire.Presentation;
/// <summary>Draw plane only. All tactical facts remain in its single overlay owner.</summary>
public partial class BoardRangePlane : Node2D
{
    internal BoardOverlay? OwnerOverlay;
    public override void _Draw() => OwnerOverlay?.DrawRanges(this);
}
