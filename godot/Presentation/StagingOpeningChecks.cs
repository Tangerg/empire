#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Godot;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Empire.Presentation;

/// <summary>Actual new-campaign choices, opening battles and camp reentry in isolated native user data.</summary>
public partial class StagingOpeningChecks : Node
{
    private int assertions;
    public override async void _Ready()
    {
        try
        {
            await CheckOpeningFlows();
            // Release completed-flow UI wrappers while Godot is alive; native nodes are checked before collection.
            await Layout();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Layout();
            GD.Print($"Staging opening checks passed: {assertions} assertions; actual native New/story/choice/start/camp/save/load/reentry, not GUI visual acceptance.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task CheckOpeningFlows()
    {
        var root = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "";
        var temporary = Path.GetFullPath(Path.GetTempPath());
        Require(Path.IsPathFullyQualified(root) && Path.GetFullPath(root).StartsWith(temporary + "empire-", StringComparison.Ordinal),
            "Staging checks require an explicit generated temporary user-data namespace.");
        var data = Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        Require(data.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Godot must use the isolated namespace.");
        for (var directory = new System.IO.DirectoryInfo(data); directory is not null && directory.FullName != temporary.TrimEnd(Path.DirectorySeparatorChar); directory = directory.Parent)
            Require(!directory.Exists || (directory.Attributes & System.IO.FileAttributes.ReparsePoint) == 0, "Fixture data cannot traverse a symlink.");
        Require(!Directory.Exists(data) || Directory.EnumerateFileSystemEntries(data).All(entry => Path.GetFileName(entry) is "logs" or "shader_cache"), "Refusing existing player saves.");
        GetWindow().Size = new(1280, 720);
        var mismatches = new List<string>();
        foreach (var choice in new[] { "rush-north-hill", "steady-advance" })
        {
            var automatic = Path.Combine(data, "grayflag-campaign-autosave.json");
            if (File.Exists(automatic))
                File.Delete(automatic);
            var manual = Path.Combine(data, "grayflag-campaign-save.json");
            if (File.Exists(manual))
                File.Delete(manual);
            var originalOrphans = NativeNodeChecks.OrphanIds();
            var game = new GrayFlagGame();
            AddChild(game);
            try
            {
                await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
                while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                    await Activate(Button(View<StoryScreen>(game), "StoryPrimary"));
                await Activate(Button(View<StoryScreen>(game), "Choice_" + choice));
                CheckReadOnlyOpening(Campaign(game), automatic);
                var staging = View<CampaignStagingScreen>(game);
                var status = staging.Presented!.Roster.Single(unit => unit.Id == "torren").Status;
                Require(Descendants(staging).OfType<Label>().Any(label => label.Text == status), "The measured roster status must be mounted native text.");
                await Activate(Button(staging, "StartBattle"));
                var owner = Campaign(game);
                var state = owner.Battle!.Snapshot();
                var torren = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]?.GetValue<string>() == "campaign-torren");
                var hp = torren["hp"]!.GetValue<double>();
                Require(hp == (choice == "rush-north-hill" ? 72 : 98), "The unchanged authored choice must retain its actual opening health.");
                var opening = StateDigest.Hash(state);
                CheckReadOnlyOpening(owner, automatic);
                await Activate(Button(View<BattleScreen>(game), "SystemMenuButton"));
                await Activate(Descendants(View<BattleScreen>(game)).OfType<Button>().Single(button => button.Text == "战役营地"));
                var confirmation = Descendants(game).OfType<ConfirmationDialog>().Single(dialog => dialog.Name == "CampConfirmation");
                await Layout();
                await Activate(confirmation.GetOkButton());
                var camp = View<CampaignStagingScreen>(game);
                var campStatus = camp.Presented!.Roster.Single(unit => unit.Id == "torren").Status;
                var expected = $"生命 {Math.Floor(hp / 98 * 100 + .5)}% · 军衔 0";
                GD.Print($"STAGING_OPENING choice={choice}; before=[{status}]; actualHp={hp}/98; camp=[{campStatus}]; opening={opening}");
                if (status != expected || campStatus != expected)
                    mismatches.Add(choice);
                Require(owner.Snapshot()["roster"]!["torren"]!["hpRatio"]!.GetValue<double>() == 1,
                    "Displaying the injury cannot modify persistent roster health.");
                CheckReadOnlyOpening(owner, automatic);
                Require(!File.Exists(manual), "Ordinary inspection cannot write the manual slot.");
                await KeyPress(Key.F5);
                Require(File.Exists(manual), "Explicit camp F5 must save the existing camp semantics.");
                await Activate(Descendants(game).OfType<AcceptDialog>().Single(dialog => dialog.Visible).GetOkButton());
                var saved = File.ReadAllBytes(manual);
                var savedAt = File.GetLastWriteTimeUtc(manual);
                await KeyPress(Key.F9);
                var restored = Campaign(game);
                Require(!ReferenceEquals(restored, owner) && restored.AtCamp, "Native F9 must restore the saved camp into a fresh campaign.");
                var restoredCamp = View<CampaignStagingScreen>(game);
                Require(restoredCamp.Presented!.Roster.Single(unit => unit.Id == "torren").Status == campStatus,
                    "Restored camp must retain the same opening-health description.");
                CheckReadOnlyOpening(restored, automatic);
                await Activate(Descendants(game).OfType<AcceptDialog>().Single(dialog => dialog.Visible).GetOkButton());
                await Activate(Button(restoredCamp, "StartBattle"));
                Require(StateDigest.Hash(Campaign(game).Battle!.Snapshot()) == opening, "Camp reentry must preserve the exact authored opening.");
                Require(File.ReadAllBytes(manual).SequenceEqual(saved) && File.GetLastWriteTimeUtc(manual) == savedAt,
                    "Preview, camp load and reentry cannot overwrite the explicit manual checkpoint.");
            }
            finally
            {
                var screens = Descendants(game).OfType<BattleScreen>().ToArray();
                var boards = screens.Select(screen => screen.Board).ToArray();
                game.QueueFree();
                await Layout();
                Require(!GodotObject.IsInstanceValid(game), "The native game tree must be released before its fixture ends.");
                Require(screens.All(screen => !GodotObject.IsInstanceValid(screen)) && boards.All(board => !GodotObject.IsInstanceValid(board)),
                    "Queued teardown must release native battle screens and boards before managed collection.");
                Require(NativeNodeChecks.OrphanIds().All(originalOrphans.Contains),
                    "Staging flows must not leave orphan nodes for managed collection to clean up.");
            }
        }
        Require(mismatches.Count == 0, "Staging must describe the actual opening health: " + string.Join(", ", mismatches));
    }
    private void CheckReadOnlyOpening(GrayFlagCampaign owner, string automatic)
    {
        string SavedFacts()
        {
            var saved = owner.Save();
            saved["campaign"]!.AsObject().Remove("savedAt");
            (saved["battle"] as JsonObject)?.Remove("savedAt");
            return EcmaJson.Stringify(saved);
        }
        var before = SavedFacts();
        var request = EcmaJson.Stringify(owner.RequestSnapshot());
        var battle = owner.Battle is null ? null : StateDigest.Hash(owner.Battle.Snapshot());
        var bytes = File.ReadAllBytes(automatic);
        var stamp = File.GetLastWriteTimeUtc(automatic);
        IReadOnlyDictionary<string, double>? previous = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var health = owner.OpeningRosterHealth();
            Require(previous is null || health.OrderBy(entry => entry.Key).SequenceEqual(previous.OrderBy(entry => entry.Key)),
                "Repeated previews must describe the same canonical opening.");
            var immutable = false;
            try
            {
                ((IDictionary<string, double>)health).Clear();
            }
            catch (NotSupportedException) { immutable = true; }
            Require(immutable, "Opening health must be a read-only projection.");
            Require(SavedFacts() == before && EcmaJson.Stringify(owner.RequestSnapshot()) == request
                && (owner.Battle is null ? null : StateDigest.Hash(owner.Battle.Snapshot())) == battle,
                "Preview must not advance campaign sequence, pending request, battle state, RNG or saved application facts.");
            Require(File.ReadAllBytes(automatic).SequenceEqual(bytes) && File.GetLastWriteTimeUtc(automatic) == stamp,
                "Preview must not rewrite the automatic checkpoint.");
            previous = health;
        }
    }
    private async Task KeyPress(Key key)
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
        await Layout();
    }
    private async Task Activate(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Native action must be visible and enabled.");
        button.GrabFocus();
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = pressed };
            button.GetViewport().PushInput(input, true);
        }
        await Layout();
    }
    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    private static GrayFlagCampaign Campaign(GrayFlagGame game) => Field<GrayFlagCampaign>(game, "campaign");
    private static T View<T>(GrayFlagGame game) where T : Control => (T)Field<Control>(game, "view");
    private static Button Button(Node node, string name) => Descendants(node).OfType<Button>().Single(button => button.Name == name);
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
