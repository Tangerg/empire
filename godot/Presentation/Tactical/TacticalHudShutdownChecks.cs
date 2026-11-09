#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private WeakReference[] resourceOwners = Array.Empty<WeakReference>();

    public override async void _Ready()
    {
        try
        {
            var originalOrphans = NativeNodeChecks.OrphanIds();
            await CheckHudFlow();
            await FinishFrame();
            Require(GetChildCount() == 0, "The HUD suite must release its mounted native fixtures before collection.");
            Require(NativeNodeChecks.OrphanIds().All(originalOrphans.Contains),
                "The HUD suite must not rely on managed collection to free orphan nodes.");
            // The resource-owning async scope must return before finalizing wrappers.
            // Imported shared textures remain owned by Godot, never manually disposed here.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await FinishFrame();
            Require(resourceOwners.Length == 5 && resourceOwners.All(owner => !owner.IsAlive),
                "HUD resource-owning fixture caches must be unreachable before native engine shutdown.");
            GD.Print($"TACTICAL_HUD_CHECKS_OK {assertions} assertions (headless native controls and host teardown, not visual acceptance)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task FinishFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
