#nullable enable
using System;
using System.Linq;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    public override async void _Ready()
    {
        try
        {
            var stamp = new TacticalStamp(17, 9);
            var hud = new TacticalHud();
            AddChild(hud);
            var fired = 0;
            hud.IntentRequested += intent => { Require(intent.Stamp == stamp, "Exact stamp emitted"); fired++; };
            TacticalHudProjection Projection(bool enabled = true) => new(stamp, "下一步", new[] { new TacticalHudSection("orders", "指令", new[] { new TacticalHudRow("wait", "待命", "原始提示", enabled, Intent: new(stamp, TacticalHudIntentKind.Command, "wait")) }) });
            Button Button() => hud.GetChildren().OfType<VBoxContainer>().Single().GetChildren().OfType<Button>().Single();
            hud.Apply(Projection());
            var old = Button();
            Require(old.TooltipText == "原始提示", "Native reason tooltip");
            old.EmitSignal(BaseButton.SignalName.Pressed);
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "One-shot duplicate signal suppression");
            hud.Apply(Projection());
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Replaced row stale callback");
            var mounted = Button();
            RemoveChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached widget inert");
            AddChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Remount without Apply inert");
            hud.Apply(Projection(false));
            var disabled = Button();
            Require(disabled.Disabled, "Native disabled row");
            disabled.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Disabled emitted signal inert");
            hud.Apply(Projection() with
            {
                Busy = true
            });
            var busy = Button();
            busy.EmitSignal(BaseButton.SignalName.Pressed);
            Require(busy.Disabled && fired == 1, "Busy projection cannot emit tactical input");
            hud.Apply(Projection());
            var detached = Button();
            detached.GetParent().RemoveChild(detached);
            detached.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached child inert");
            detached.QueueFree();
            hud.Apply(Projection());
            Button().GrabFocus();
            hud.Apply(Projection());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(Button().HasFocus(), "Stable row focus restored");
            var metadata = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            foreach (var image in metadata.Icons.Values.Concat(metadata.Portraits.Values.Where(value => value.Card is not null).Select(value => value.Card!)))
            {
                var bytes = Godot.FileAccess.GetFileAsBytes(image.ResourcePath!);
                Require(bytes.Length > 0 && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant() == image.Sha256, "Original HUD resource hash: " + image.ResourcePath);
            }
            var assets = AssetCatalog.Load();
            var art = TacticalHudArt.Load(assets);
            hud.ConfigureArt(art);
            var named = new TacticalHudPortrait("c01.swordsman", "campaign-torren", "#224466");
            var portrait = art.Portrait(named) as AtlasTexture;
            Require(portrait is not null, "Named portrait is cropped source card");
            Require(portrait!.Atlas.ResourcePath.Contains("torren", StringComparison.OrdinalIgnoreCase), "Named card source, not swordsman sprite");
            var generic = art.Portrait(new("c01.swordsman", null, "#224466")) as AtlasTexture;
            Require(generic is not null && generic.Atlas.ResourcePath != portrait.Atlas.ResourcePath, "Generic source frame distinct from named card");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            Require(hud.FindChild("InspectedPortrait", true, false) is PanelContainer, "Source portrait mounted");
            for (var index = 0; index < 100; index++)
                hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            Require(art.OwnedTextureCount == 1, "Repeated projections reuse one owned card crop");
            var replacement = TacticalHudArt.Load(assets);
            hud.ConfigureArt(replacement);
            Require(art.OwnedTextureCount == 0, "Rebind releases owned crops after controls release references");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            RemoveChild(hud);
            Require(replacement.OwnedTextureCount == 0, "Teardown releases owned portrait crop");
            hud.QueueFree();
            var missing = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            var required = missing.SourceIconKeys[0];
            missing.Icons.Remove(required);
            var broken = new TacticalHudArt(assets, missing);
            try
            {
                broken.Icon(required);
                throw new Exception("Missing declared icon silently disappeared");
            }
            catch (MissingArtResourceException) { assertions++; }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"TACTICAL_HUD_CHECKS_OK {assertions} assertions (headless native controls, not visual acceptance)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
