using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Localization;
using System.Globalization;
using Godot;

namespace Empire.Presentation;

public partial class CampaignStagingChecks : Node
{
    private int assertions;
    public override void _Ready()
    {
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            if (arguments.Length != 1) throw new ArgumentException("Pass staging source oracle JSON after --");
            var oracle = EcmaJson.Parse(System.IO.File.ReadAllText(arguments[0]))!.AsObject();
            var item = oracle["cases"]!.AsArray().OfType<JsonObject>().First();
            var nativeCatalog = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
            var catalog = nativeCatalog.GetMessageList().ToDictionary(key => key, key => nativeCatalog.GetMessage(key).ToString(), StringComparer.Ordinal);
            foreach (var message in oracle["testMessages"]!.AsObject()) catalog.Add(message.Key, message.Value!.GetValue<string>());
            var messages = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
            JsonObject FixtureMetadata()
            {
                var value = PresentationJson.Load<JsonObject>("res://data/grayflag-staging.json");
                foreach (var (target, fixture) in new[] { ("resourceLabelKeys", "testResourceLabelKeys"), ("relationLabelKeys", "testRelationLabelKeys") })
                    foreach (var label in oracle[fixture]!.AsObject()) value[target]![label.Key] = label.Value!.DeepClone();
                return value;
            }
            var metadata = FixtureMetadata();
            void RemoveArt(JsonNode? value)
            {
                if (value is not JsonObject obj) return;
                if (obj.ContainsKey("source") && obj.ContainsKey("resourcePath")) obj["resourcePath"] = null;
                foreach (var child in obj.ToArray()) RemoveArt(child.Value);
            }
            RemoveArt(metadata);
            string UnitName(string id) => EcmaJson.ReadString(oracle["unitNames"]![id]);
            CampaignStagingView View(bool pending) => CampaignStaging.Create(messages, EcmaJson.ToElement(item["state"]),
                EcmaJson.ToElement(item["node"]), EcmaJson.ToElement(item["level"]), EcmaJson.ToElement(metadata), UnitName, pending);
            var assets = AssetCatalog.Load();
            var view = View(false);
            var screen = new CampaignStagingScreen(); AddChild(screen);
            var starts = 0; var exits = 0;
            screen.ShowStaging(view, assets, () => starts++, () => exits++);
            var text = string.Join("\n", Descendants(screen).OfType<Label>().Select(label => label.Text));
            foreach (var value in new[] { view.Title, view.Description, view.Eyebrow, view.ChapterHeading, view.Progress,
                view.RosterHeading, view.StateHeading, view.ActionNote })
                Require(text.Contains(value, StringComparison.Ordinal), $"Staging omitted source text: {value}");
            foreach (var unit in view.Roster)
            {
                Require(text.Contains(unit.Name, StringComparison.Ordinal) && text.Contains(unit.Status, StringComparison.Ordinal), "Roster omitted source unit status");
                var row = Descendants(screen).OfType<HBoxContainer>().Single(node => node.Name == "Roster_" + unit.Id);
                var picture = row.GetNode<TextureRect>("Avatar");
                Require(picture.Texture is AtlasTexture, "Unavailable portraits must use selected actual unit sprites, not a geometric placeholder");
                var expected = assets.ResolveUnit(unit.UnitType, unit.ActorKey);
                Require(((AtlasTexture)picture.Texture).Atlas.ResourcePath == expected.ResourcePath,
                    "Roster fallback must respect the explicit actor binding and its source texture");
            }
            Require(screen.RenderingDiagnostics.Any(value => value.Contains("campaign backdrop", StringComparison.Ordinal)) &&
                text.Contains(view.ArtworkFailureLabel, StringComparison.Ordinal) &&
                !screen.RenderingDiagnostics.Any(value => text.Contains(value, StringComparison.Ordinal)),
                "Missing staging art must retain precise diagnostics while showing only the localized failure notice");
            foreach (var unit in view.Roster.Where(unit => unit.Portrait is not null))
                Require(screen.RenderingDiagnostics.Any(value => value.Contains($"portrait {unit.Id}", StringComparison.Ordinal)), "Missing authored portrait must be reported");
            var oldButton = NavigationButton(screen, "StartBattle");
            var oldExit = NavigationButton(screen, "CampaignExit");
            Require(oldExit.AccessibilityName == view.ExitLabel, "Shared shell exit must retain the source accessible label");
            Require(oldButton.Text == view.StartLabel, "First entry label must match source");
            oldButton.EmitSignal(Button.SignalName.Pressed); oldButton.EmitSignal(Button.SignalName.Pressed);
            Require(starts == 1 && oldButton.Disabled && oldExit.Disabled, "Start callback must run once and disable sibling exit");
            oldExit.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 0, "Shared-shell exit escaped a submitted Start action");
            var reentry = View(true);
            screen.ShowStaging(reentry, assets, () => starts++, () => exits++);
            oldButton.EmitSignal(Button.SignalName.Pressed); oldExit.EmitSignal(Button.SignalName.Pressed);
            Require(starts == 1 && exits == 0, "Stale Start/Exit callbacks after rebind must not navigate");
            var newButton = NavigationButton(screen, "StartBattle");
            var newExit = NavigationButton(screen, "CampaignExit");
            Require(newButton.Text == "重新进入战斗 →", "Reentry must not be called resume or continue");
            Require(Descendants(screen).OfType<Label>().Any(label => label.Text == "离开战场后将从本关开局重新开始。"), "Reentry must disclose restart semantics");
            var buttonParent = newButton.GetParent();
            buttonParent.RemoveChild(newButton);
            Require(screen.IsInsideTree() && !newButton.IsInsideTree(), "Detached-button regression must leave its screen mounted");
            newButton.EmitSignal(Button.SignalName.Pressed);
            Require(starts == 1, "Detached Start button must not act on campaign state while its screen stays mounted");
            buttonParent.AddChild(newButton);
            var exitParent = newExit.GetParent(); exitParent.RemoveChild(newExit);
            newExit.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 0, "Detached shared-shell Exit must not navigate while its screen stays mounted");
            exitParent.AddChild(newExit);
            RemoveChild(screen); newButton.EmitSignal(Button.SignalName.Pressed); newExit.EmitSignal(Button.SignalName.Pressed);
            Require(starts == 1 && exits == 0, "Unmounted Start/Exit callbacks must not act on campaign state");
            AddChild(screen); newButton.EmitSignal(Button.SignalName.Pressed); newExit.EmitSignal(Button.SignalName.Pressed);
            Require(starts == 1 && exits == 0, "Remount alone must not revive stale Start/Exit callbacks");
            screen.ShowStaging(view, assets, () => starts++, () => exits++);
            var reboundStart = NavigationButton(screen, "StartBattle"); var reboundExit = NavigationButton(screen, "CampaignExit");
            reboundExit.EmitSignal(Button.SignalName.Pressed); reboundExit.EmitSignal(Button.SignalName.Pressed); reboundStart.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 1 && starts == 1 && reboundExit.Disabled && reboundStart.Disabled, "Exit must be single-use and block sibling Start");
            screen.ShowStaging(view, assets, () => starts++, () => exits++);
            reboundExit.EmitSignal(Button.SignalName.Pressed); reboundStart.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 1 && starts == 1, "A new staging binding revived previously submitted navigation");
            NavigationButton(screen, "StartBattle").EmitSignal(Button.SignalName.Pressed);
            Require(starts == 2 && exits == 1, "Remounted and explicitly rebound screen must accept one new action");
            metadata = FixtureMetadata();
            foreach (var sourceCase in oracle["cases"]!.AsArray().OfType<JsonObject>())
            {
                var live = CampaignStaging.Create(messages, EcmaJson.ToElement(sourceCase["state"]), EcmaJson.ToElement(sourceCase["node"]),
                    EcmaJson.ToElement(sourceCase["level"]), EcmaJson.ToElement(metadata), UnitName, sourceCase["pending"]!.GetValue<bool>());
                screen.ShowStaging(live, assets, () => starts++, () => exits++);
                Require(screen.RenderingDiagnostics.Count == 0, $"Real exported staging art is incomplete: {string.Join("; ", screen.RenderingDiagnostics)}");
                foreach (var unit in live.Roster.Where(unit => unit.Portrait is not null))
                {
                    var row = Descendants(screen).OfType<HBoxContainer>().Single(node => node.Name == "Roster_" + unit.Id);
                    Require(row.GetNode<TextureRect>("Avatar").Texture.ResourcePath == unit.Portrait!.ResourcePath,
                        "Exported named portrait must be used instead of the unit-sprite fallback");
                }
            }
            RemoveChild(screen); screen.QueueFree();
            GD.Print($"Campaign staging native checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static Button NavigationButton(Node root, string name) => Descendants(root).OfType<Button>().Single(button => button.Name == name);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
}
