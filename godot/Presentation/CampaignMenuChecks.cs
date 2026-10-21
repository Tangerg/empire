using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Localization;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

public partial class CampaignMenuChecks : Node
{
    private int assertions;
    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var isolatedArtFixture = args.Length == 2 && args[1] == "--isolated-menu-art-fixture";
            if (args.Length != 1 && !isolatedArtFixture)
                throw new ArgumentException("Pass the frozen native menu fixture after --; only the minimal isolated runner may add --isolated-menu-art-fixture.");
            var oracle = EcmaJson.Parse(System.IO.File.ReadAllText(args[0]))!.AsObject();
            var messages = GodotTextCatalog.LoadSimplifiedChinese();
            var sourceMetadata = EcmaJson.ToElement(oracle["metadata"]);
            var missingArtMetadata = (JsonObject)oracle["metadata"]!.DeepClone();
            const string missingArtPath = "res://__campaign_menu_checks__/deliberately_missing_menu_art.png";
            Require(!Godot.FileAccess.FileExists(missingArtPath), "The deliberately missing-art fixture unexpectedly exists");
            missingArtMetadata["menuArt"]!["resourcePath"] = missingArtPath;
            var metadata = EcmaJson.ToElement(missingArtMetadata);
            var screen = new CampaignMenuScreen(); AddChild(screen);
            var continued = 0; var requested = 0;
            foreach (var item in oracle["menuCases"]!.AsArray().OfType<JsonObject>())
            {
                var loaded = new LoadedCampaign(item["state"] as JsonObject, item["rejected"]?.GetValue<string>());
                var view = CampaignMenu.Create(messages, loaded, metadata);
                var sourceView = CampaignMenu.Create(messages, loaded, sourceMetadata);
                Require(view with { ArtResourcePath = sourceView.ArtResourcePath } == sourceView,
                    "Missing-art fixture changed a source projection field other than resource path");
                screen.ShowMenu(view, () => continued++, () => requested++);
                var labels = Descendants(screen).OfType<Label>().Select(label => label.Text).ToArray();
                foreach (var text in new[] { view.Title, view.Kicker, view.Description }) Require(labels.Contains(text), "Native menu omitted source prose");
                Require((Button(screen, "ContinueCampaign") is not null) == view.CanContinue, "Native Continue visibility changed");
                Require(Button(screen, "NewCampaign")!.Text == view.NewLabel, "Native New/Restart label changed");
                Require(view.RejectedNotice is null || labels.Contains(view.RejectedNotice), "Native title must report unusable saved progress");
                Require(screen.RenderingDiagnostics.Count == 1 && screen.RenderingDiagnostics[0].Contains(missingArtPath, StringComparison.Ordinal) &&
                    labels.Contains(view.ArtworkFailureLabel) && !labels.Contains(screen.RenderingDiagnostics[0]),
                    "Missing menu art must retain precise diagnostics while showing only the localized failure notice");
                Require(!Descendants(screen).OfType<TextureRect>().Any(picture => picture.Name == "MenuArt"),
                    "Missing original title art must not be replaced with unrelated imagery");
                var oldNew = Button(screen, "NewCampaign")!;
                var before = requested;
                oldNew.EmitSignal(Godot.Button.SignalName.Pressed); oldNew.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(requested == before + 1 && oldNew.Disabled, "New intent must be single-use while caller confirmation is pending");
                Button(screen, "ContinueCampaign")?.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(continued == 0, "A second menu action must not escape the pending intent guard");
                // Cancelling belongs to the caller; rebinding explicitly makes the menu interactive again.
                screen.ShowMenu(view, () => continued++, () => requested++);
                oldNew.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(requested == before + 1, "Rebound menu accepted a stale callback");
                var fresh = Button(screen, "NewCampaign")!;
                var row = fresh.GetParent(); row.RemoveChild(fresh);
                fresh.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(requested == before + 1, "Detached menu button acted while its screen stayed mounted");
                row.AddChild(fresh);
                RemoveChild(screen); fresh.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(requested == before + 1, "Unmounted menu acted on caller state");
                AddChild(screen); fresh.EmitSignal(Godot.Button.SignalName.Pressed);
                Require(requested == before + 1, "Remount without an explicit new binding must not revive old callbacks");
            }
            var active = CampaignMenu.Create(messages, new(oracle["initial"]!.AsObject(), null), metadata);
            screen.ShowMenu(active, () => continued++, () => requested++);
            var continuation = Button(screen, "ContinueCampaign")!;
            continuation.EmitSignal(Godot.Button.SignalName.Pressed); continuation.EmitSignal(Godot.Button.SignalName.Pressed);
            Require(continued == 1 && continuation.Disabled, "Continue must signal exactly once per binding");
            var snapshot = EcmaJson.Stringify(oracle["initial"]);
            var awaiting = false;
            screen.ShowMenu(active, () => continued++, () => { awaiting = true; requested++; });
            Button(screen, "NewCampaign")!.EmitSignal(Godot.Button.SignalName.Pressed);
            Require(awaiting, "New must report intent to the caller rather than silently enter a campaign");
            Require(EcmaJson.Stringify(oracle["initial"]) == snapshot, "Menu intent must not mutate canonical campaign facts");
            Require(!Descendants(screen).OfType<ConfirmationDialog>().Any(), "Overwrite confirmation belongs to the application caller, not the menu renderer");
            if (!isolatedArtFixture)
            {
                const string exportPath = "res://data/grayflag-staging.json";
                Require(Godot.FileAccess.FileExists(exportPath), "Full-project menu checks require exported presentation metadata");
                var exported = EcmaJson.Parse(Godot.FileAccess.GetFileAsString(exportPath))!.AsObject();
                var loaded = new LoadedCampaign(oracle["initial"]!.AsObject(), null);
                var sourceView = CampaignMenu.Create(messages, loaded, sourceMetadata);
                var exportedView = CampaignMenu.Create(messages, loaded, EcmaJson.ToElement(exported));
                Require(exportedView with { ArtResourcePath = sourceView.ArtResourcePath } == sourceView,
                    "Exported menu prose, source art identity or progress disagrees with the frozen native menu expectations");
                Require(exportedView.ArtResourcePath is not null && Godot.FileAccess.FileExists(exportedView.ArtResourcePath),
                    "The actual exported original menu art is missing");
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    Godot.FileAccess.GetFileAsBytes(exportedView.ArtResourcePath!))).ToLowerInvariant();
                Require(hash == exported["menuArt"]!["sha256"]!.GetValue<string>(), "Exported original menu art bytes disagree with their manifest identity");
                screen.ShowMenu(exportedView, () => continued++, () => requested++);
                Require(screen.RenderingDiagnostics.Count == 0, "Available exported menu art must load without a missing-art diagnostic");
                var picture = Descendants(screen).OfType<TextureRect>().Single(node => node.Name == "MenuArt");
                Require(picture.Texture is not null && picture.Texture.ResourcePath == exportedView.ArtResourcePath,
                    "The actual native menu did not load its exported original texture");
                Require(picture.TooltipText == sourceView.ArtAlt, "The actual native menu lost its source art description");
            }
            RemoveChild(screen); screen.QueueFree();
            GD.Print($"Campaign menu native checks passed: {assertions} assertions ({(isolatedArtFixture ? "explicit isolated missing-art fixture; exported-art integration not run" : "including actual exported-menu art identity")})."); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static Button? Button(Node root, string name) => Descendants(root).OfType<Button>().SingleOrDefault(button => button.Name == name);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private void Require(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
}
