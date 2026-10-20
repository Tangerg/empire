#nullable enable
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    internal CandidateShadowCache LightingResources => _shadows;

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) _shadows.Dispose();
    }
}
