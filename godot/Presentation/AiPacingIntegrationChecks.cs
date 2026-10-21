#nullable enable
using System.Reflection;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

/// <summary>Mounted Game and real AI commits; clock and campaign entry are controlled test fixtures.</summary>
public partial class AiPacingIntegrationChecks : Node
{
    private int assertions;
    public override async void _Ready()
    {
        try
        {
            RequireIsolatedData();
            CheckGamePauseAndCancellation();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"AI pacing native integration passed: {assertions} assertions; actual AI commits with fixture clock/campaign entry.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("AI pacing native integration failed: " + error); GetTree().Quit(1); }
    }

    private void CheckGamePauseAndCancellation()
    {
        var game = new GrayFlagGame { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(game);
        try
        {
            var campaign = Field<GrayFlagCampaign>(game, "campaign");
            while (campaign.Node()["type"]!.GetValue<string>() != "battle")
            {
                if (campaign.Node()["type"]!.GetValue<string>() == "choice")
                    campaign.Choose(campaign.Choices()[0]!["id"]!.GetValue<string>());
                else
                    campaign.Advance();
            }
            campaign.BeginBattle();
            Invoke(game, "ShowBattle", (object?)null);
            Invoke(game, "Save");
            var saved = SaveStamps();
            var session = campaign.Battle!;
            Require(session.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), session.GetSnapshot().Revision).Accepted,
                "Fixture could not hand the real battle to AI.");
            Invoke(game, "ShowBattle", (object?)null);
            var screen = Field<BattleScreen>(game, "screen");
            screen.Board.AdvanceAnimationsAutomatically = false;
            var revision = session.GetSnapshot().Revision;
            Invoke(game, "ProcessBattle", 0d);
            Require(session.GetSnapshot().Revision == revision + 1, "Initial actual AI action was delayed.");
            var pacer = Field<AiTurnPacer>(game, "computerPacer");
            Require(pacer.IsRunning && screen.IsInputBlocked, "Actual AI did not establish application busy.");
            var hadAnimation = screen.IsPresenting;
            for (var i = 0; i < 500 && screen.IsPresenting; i++)
                screen.Board.AdvancePresentation(0.1);
            Require(!screen.IsPresenting && screen.IsInputBlocked, "Settled animation released AI-loop busy too early.");
            var pause = session.LastActionHandsOffTurn == true ? 120 : 90;
            var settlement = hadAnimation ? 1000d : 0d;
            Invoke(game, "ProcessBattle", settlement);
            Require(session.GetSnapshot().Revision == revision + 1 && pacer.IsRunning && !screen.IsPresenting,
                "Settlement did not begin the separate post-animation pause.");
            var messages = screen.RecentMessages.ToArray();
            var feedback = Field<Label>(screen, "_feedback").Text;
            foreach (var name in new[] { "Save", "Load" })
                Invoke(game, name);
            foreach (var key in new[] { Key.F5, Key.F9 })
                game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
            foreach (var button in Descendants(screen).OfType<Button>().Where(button => button.Text is "保存" or "读取"))
                button.EmitSignal(Button.SignalName.Pressed);
            Require(SaveStamps().SequenceEqual(saved), "Save/Load changed files during post-settlement pause.");
            Require(ReferenceEquals(campaign.Battle, session) && ReferenceEquals(Field<BattleScreen>(game, "screen"), screen)
                && session.GetSnapshot().Revision == revision + 1, "Load rebound or AI advanced during busy Save/Load.");
            Require(screen.RecentMessages.SequenceEqual(messages) && Field<Label>(screen, "_feedback").Text == feedback,
                "Busy Save/Load was not silently ignored at application boundary.");
            Invoke(game, "ProcessBattle", settlement + pause - 1);
            Require(session.GetSnapshot().Revision == revision + 1 && pacer.IsRunning, "Actual Game pause ended one millisecond early.");
            Invoke(game, "ProcessBattle", settlement + pause);
            Require(session.GetSnapshot().Revision == revision + 2, "Actual Game failed to commit next AI action at deadline.");

            // Replace the screen/session while the next action is still active, then try stale controls.
            var staleButtons = Descendants(screen).OfType<Button>().Where(button => button.Text is "保存" or "读取" or "战役营地").ToArray();
            var oldRevision = session.GetSnapshot().Revision;
            campaign.RestartBattle();
            Invoke(game, "ShowBattle", (object?)null);
            Require(!pacer.IsRunning && !Field<BattleScreen>(game, "screen").IsInputBlocked, "Rebinding retained previous AI busy/deadline.");
            var replacement = campaign.Battle!;
            foreach (var button in staleButtons)
                button.EmitSignal(Button.SignalName.Pressed);
            Invoke(game, "ProcessBattle", settlement + 10000);
            Require(session.GetSnapshot().Revision == oldRevision && replacement.GetSnapshot().Revision == 0
                && ReferenceEquals(campaign.Battle, replacement) && SaveStamps().SequenceEqual(saved), "Stale AI/view callback affected replacement or saves.");

            // Start another actual AI action, unmount the host, and manually invoke its old process entry.
            Require(replacement.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), 0).Accepted, "Rebound fixture handoff failed.");
            Invoke(game, "ShowBattle", (object?)null);
            var reboundScreen = Field<BattleScreen>(game, "screen");
            reboundScreen.Board.AdvanceAnimationsAutomatically = false;
            Invoke(game, "ProcessBattle", 20000d);
            Require(pacer.IsRunning, "Rebound actual AI loop did not start.");
            var unmountRevision = replacement.GetSnapshot().Revision;
            RemoveChild(game);
            Require(!pacer.IsRunning, "Unmount retained AI work.");
            Invoke(game, "ProcessBattle", 30000d);
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(replacement.GetSnapshot().Revision == unmountRevision && ReferenceEquals(campaign.Battle, replacement),
                "Unmounted Game resumed AI or loaded a stale save.");
        }
        finally { game.GetParent()?.RemoveChild(game); game.QueueFree(); }
    }

    private void RequireIsolatedData()
    {
        var configured = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Require(!string.IsNullOrWhiteSpace(configured), "An explicit temporary XDG_DATA_HOME is required.");
        var path = System.IO.Path.GetFullPath(configured!);
        var temporary = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        var relative = System.IO.Path.GetRelativePath(temporary, path);
        Require(!System.IO.Path.IsPathFullyQualified(relative) && relative.Split(System.IO.Path.DirectorySeparatorChar)[0].StartsWith("empire-", StringComparison.Ordinal),
            "Refusing non-temporary user data.");
        var userData = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        Require(userData.StartsWith(path + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal), "Godot ignored isolated user data.");
        Require(SaveStamps().All(value => value == "absent"), "Refusing existing manual/autosave files.");
    }
    private static string[] SaveStamps() => new[] { "grayflag-campaign-save.json", "grayflag-campaign-autosave.json" }
        .SelectMany(name => new[] { name, name + ".tmp" }).Select(name =>
        {
            var path = ProjectSettings.GlobalizePath("user://" + name);
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) + ":" + System.IO.File.GetLastWriteTimeUtc(path).Ticks : "absent";
        }).ToArray();
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void Invoke(object instance, string name, params object?[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
