using System;
using Godot;

namespace Empire.Presentation;

/// <summary>Publishes native campaign button intent after the current input dispatch has finished.</summary>
internal static class CampaignButtonActivation
{
    public static void Connect(Control owner, Button button, Func<bool> current, Action activate)
    {
        var pending = false;
        var treeLifetime = 0;
        button.TreeExiting += () => treeLifetime++;
        bool CanActivate() => GodotObject.IsInstanceValid(owner)
            && owner.IsInsideTree() && !owner.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(button) && button.IsInsideTree()
            && !button.IsQueuedForDeletion() && !button.Disabled && current();
        button.Pressed += () =>
        {
            if (pending || !CanActivate())
                return;
            pending = true;
            var lifetime = treeLifetime;
            // Touch-emulated mouse dispatch still visits the input target after Pressed returns.
            // A navigation callback may synchronously detach that target, so publish it only afterward.
            Callable.From(() =>
            {
                pending = false;
                if (lifetime == treeLifetime && CanActivate())
                    activate();
            }).CallDeferred();
        };
    }
}
