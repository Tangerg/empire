#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Localization;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

/// <summary>Native navigation and save regressions. Generated save fixtures require isolated temporary user data.</summary>
public partial class BattleResultFlowChecks : Node
{
    private int assertions;
    private readonly List<string> failures = new();
    private static readonly CampaignBattleSummary Summary = new("Fixture battle", 1, "Fixture victory", 4, 3, 0,
        new[] { "Fixture ally" }, new[] { "fixture.signal" }, 7);

    public override void _Ready()
    {
        try
        {
            var userData = RequireIsolatedUserData();
            Require(!System.IO.Directory.Exists(userData) || System.IO.Directory.EnumerateFileSystemEntries(userData)
                .All(entry => System.IO.Path.GetFileName(entry) == "logs"),
                "Result-flow checks refuse pre-existing user data before mounting any game; use a fresh generated directory.");
        }
        catch (Exception error) { GD.PushError("Result-flow safety guard: " + error); GetTree().Quit(1); return; }
        Check("isolated clean prebattle autosave", CheckPrebattleAutosave);
        Check("completion one-shot navigation", CheckCompletionButtons);
        Check("result summary and invalid outcomes", CheckSummary);
        Check("stale rebind and unmount callbacks", CheckStaleButtons);
        foreach (var outcome in new[] { "victory", "defeat", "draw" })
            foreach (var action in outcome == "victory" ? new[] { "Settle", "Retry", "Exit" } : new[] { "Retry", "Exit" })
                Check($"game {outcome}/{action}", () => CheckGameFlow(outcome, action));
        Check("busy save/load keyboard and direct entry", CheckBusySaveLoad);
        if (failures.Count == 0)
        {
            GD.Print($"Battle result-flow native checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        else
        {
            foreach (var failure in failures) GD.PushError(failure);
            GD.Print($"Battle result-flow native checks failed: {failures.Count} groups, {assertions} assertions reached.");
            GetTree().Quit(1);
        }
    }

    private void CheckCompletionButtons()
    {
        foreach (var victory in new[] { true, false })
            foreach (var first in victory ? new[] { "Settle", "Retry", "Exit" } : new[] { "Retry", "Exit" })
            {
                var screen = new BattleResultScreen(); AddChild(screen);
                var calls = new List<string>();
                screen.ShowCompletion(victory ? "胜利" : "战败", "Fixture reason", victory,
                    () => calls.Add("Settle"), () => calls.Add("Retry"), () => calls.Add("Exit"));
                var buttons = Buttons(screen).ToDictionary(button => button.Name.ToString());
                Require(buttons.Keys.Order().SequenceEqual((victory ? new[] { "Exit", "Retry", "Settle" } : new[] { "Exit", "Retry" }).Order()), "Completion button family differs by outcome.");
                Require(Labels(screen).Contains("Fixture reason"), "Completion reason is missing.");
                Require(!Descendants(screen).OfType<GridContainer>().Any() && !buttons.ContainsKey("Proceed"), "Completion eagerly displays the settlement summary.");
                Press(buttons[first]);
                foreach (var button in buttons.Values) { Press(button); Press(button); }
                Require(calls.SequenceEqual(new[] { first }), "Completion navigation is not one-shot across sibling buttons.");
                Require(buttons.Values.All(button => button.Disabled), "Every completion button must disable before navigation.");
                Retire(screen);
            }
    }

    private void CheckSummary()
    {
        var screen = new BattleResultScreen(); AddChild(screen);
        var settled = 0; var proceeded = 0;
        screen.ShowCompletion("胜利", "Fixture reason", true, () => {
            settled++; screen.ShowResult("victory", Summary, () => proceeded++);
        }, () => throw new Exception("Unexpected retry"), () => throw new Exception("Unexpected exit"));
        var old = Buttons(screen).ToArray();
        Require(!Labels(screen).Contains(Summary.Title), "Summary title appeared before explicit settle.");
        Press(old.Single(button => button.Name == "Settle"));
        Require(settled == 1 && Labels(screen).Contains(Summary.Title), "Explicit settle did not show the victory summary.");
        Require(Descendants(screen).OfType<GridContainer>().Single().GetChildCount() == Summary.Metrics.Count * 2, "Victory summary metrics are incomplete.");
        Require(Labels(screen).Contains(CampaignBattleSummary.FallenLabel + "\n" + Summary.FallenText) &&
            Labels(screen).Contains(CampaignBattleSummary.SignalsLabel + "\n" + Summary.SignalsText), "Victory summary facts are missing.");
        foreach (var button in old) Press(button);
        Require(settled == 1 && proceeded == 0, "Stale completion button navigated after summary rebind.");
        var proceed = Buttons(screen).Single();
        Require(proceed.Name == "Proceed", "Summary exposes non-proceed navigation.");
        Press(proceed); Press(proceed);
        Require(proceeded == 1 && proceed.Disabled, "Summary proceed is not one-shot.");
        foreach (var invalid in new[] { "defeat", "draw", "", "VICTORY" })
        {
            var before = Labels(screen).ToArray();
            var refused = false;
            try { screen.ShowResult(invalid, Summary, () => proceeded++); }
            catch (ArgumentException) { refused = true; }
            Require(refused && Labels(screen).SequenceEqual(before), "Non-victory summary must reject without rebinding the current view.");
        }
        Retire(screen);
    }

    private void CheckStaleButtons()
    {
        var screen = new BattleResultScreen(); AddChild(screen);
        var oldCalls = 0; var newCalls = 0;
        void BindOld() => screen.ShowCompletion("Old", "Old reason", true,
            () => oldCalls++, () => oldCalls++, () => oldCalls++);
        BindOld(); var old = Buttons(screen).ToArray();
        screen.ShowCompletion("New", "New reason", false, () => newCalls++, () => newCalls++, () => newCalls++);
        foreach (var button in old) Press(button);
        Require(oldCalls == 0 && newCalls == 0, "Old callback survived a completion rebind.");
        Press(Buttons(screen).Single(button => button.Name == "Retry"));
        Require(newCalls == 1, "New binding did not get its own one-shot action.");

        BindOld(); var removed = Buttons(screen).Single(button => button.Name == "Exit");
        var originalParent = removed.GetParent(); originalParent.RemoveChild(removed);
        Press(removed);
        Require(oldCalls == 0, "An individually removed button navigated while the screen remained mounted.");
        removed.QueueFree();

        BindOld(); old = Buttons(screen).ToArray(); RemoveChild(screen);
        foreach (var button in old) Press(button);
        Require(oldCalls == 0, "Unmounted completion callback navigated.");
        AddChild(screen);
        foreach (var button in old) Press(button);
        Require(oldCalls == 0, "Reattachment revived a pre-unmount completion binding.");
        BindOld(); Press(Buttons(screen).Single(button => button.Name == "Exit"));
        Require(oldCalls == 1, "Explicitly rebound screen did not navigate after remount.");

        screen.ShowResult("victory", Summary, () => newCalls++);
        var oldProceed = Buttons(screen).Single(); RemoveChild(screen); Press(oldProceed); AddChild(screen); Press(oldProceed);
        Require(newCalls == 1, "Unmounted or remounted stale summary callback navigated.");
        screen.ShowResult("victory", Summary, () => newCalls++); Press(Buttons(screen).Single());
        Require(newCalls == 2, "Explicit summary rebind did not restore navigation.");
        Retire(screen);
    }

    private void CheckGameFlow(string outcome, string first)
    {
        var game = new GrayFlagGame { ProcessMode = ProcessModeEnum.Disabled }; AddChild(game);
        var campaign = Field<GrayFlagCampaign>(game, "campaign");
        while (campaign.Node()["type"]!.GetValue<string>() != "battle")
        {
            if (campaign.Node()["type"]!.GetValue<string>() == "choice")
                campaign.Choose(campaign.Choices()[0]!["id"]!.GetValue<string>());
            else campaign.Advance();
        }
        campaign.BeginBattle();
        var document = campaign.Save();
        var oracle = PresentationJson.Load<JsonObject>("res://tests/oracle/grayflag-0.58.json");
        document["battle"] = oracle["finalSave"]!.DeepClone();
        document["events"] = new JsonArray(oracle["steps"]!.AsArray().OfType<JsonObject>()
            .SelectMany(step => step["events"]!.AsArray()).Select(value => value!.DeepClone()).ToArray());
        // All three outcomes are terminal UI fixtures, not native gameplay or route-parity claims.
        // Victory reuses an actual original-engine final save; defeat/draw alter its terminal fields.
        if (outcome != "victory")
        {
            document["battle"]!["state"]!["winnerTeam"] = outcome == "draw" ? null : JsonValue.Create(2);
            document["battle"]!["state"]!["endReason"] = "Fixture " + outcome;
        }
        campaign.Load(document);
        SetField(game, "session", campaign.Battle); SetField(game, "screen", null); SetField(game, "resultOffered", false);
        var pending = StateDigest.Hash(campaign.Snapshot());
        game._Process(0);
        var completion = Field<Control>(game, "view") as BattleResultScreen ?? throw new Exception("Game did not show a completion screen.");
        var buttons = Buttons(completion).ToDictionary(button => button.Name.ToString());
        Require(buttons.ContainsKey("Settle") == (outcome == "victory"), "Game offers settlement for a non-victory or omits victory settlement.");
        Require(!buttons.ContainsKey("Proceed") && !Descendants(completion).OfType<GridContainer>().Any(), "Game skips explicit completion and shows summary immediately.");
        Require(campaign.Battle is not null && StateDigest.Hash(campaign.Snapshot()) == pending, "Merely showing completion mutates the campaign.");
        game._Process(1);
        Require(ReferenceEquals(Field<Control>(game, "view"), completion), "Repeated process creates duplicate completion views.");
        Press(buttons[first]);
        if (first == "Settle")
        {
            Require(campaign.Battle is null && !campaign.HasPendingBattle, "Explicit settlement did not commit and close the pending battle.");
            var summary = Field<Control>(game, "view") as BattleResultScreen ?? throw new Exception("Settlement did not show the summary screen.");
            Require(!ReferenceEquals(summary, completion) && Descendants(summary).OfType<GridContainer>().Any(), "Settlement did not create a distinct metric summary.");
            var committed = StateDigest.Hash(campaign.Snapshot());
            foreach (var button in buttons.Values) Press(button);
            Require(StateDigest.Hash(campaign.Snapshot()) == committed && ReferenceEquals(Field<Control>(game, "view"), summary), "Stale completion navigated or settled twice.");
            var proceed = Buttons(summary).Single(button => button.Name == "Proceed"); Press(proceed); Press(proceed);
            Require(!ReferenceEquals(Field<Control>(game, "view"), summary) && StateDigest.Hash(campaign.Snapshot()) == committed,
                "Summary proceed must navigate once without committing the result again.");
        }
        else
        {
            Require(campaign.Battle?.Snapshot()["phase"]?.GetValue<string>() == "playing" && StateDigest.Hash(campaign.Snapshot()) == pending,
                "Retry/exit must restore the opening without completing the campaign battle.");
            Require(campaign.AtCamp == (first == "Exit"), "Retry/exit camp state differs.");
            Require(first == "Exit" ? Field<Control>(game, "view") is CampaignStagingScreen : Field<Control>(game, "view") is BattleScreen,
                "Retry/exit navigated to the wrong screen.");
            var next = Field<Control>(game, "view"); var battle = campaign.Battle;
            foreach (var button in buttons.Values) Press(button);
            Require(ReferenceEquals(Field<Control>(game, "view"), next) && ReferenceEquals(campaign.Battle, battle), "A stale completion button restarted or exited the replacement battle.");
        }
        Retire(game);
    }

    private void CheckBusySaveLoad()
    {
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var refusals = ActionRefusalText.CreateDefault(text, engine.Catalog, engine.Abilities).Seal();
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var session = new GrayFlagSession(engine, level, text, refusals);
        var screen = new BattleScreen(); AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        screen.BindSession(session, AssetCatalog.Load(), level, AuthoredScene.Load(), EnvironmentCatalog.Load(), _ => false);
        TacticalSceneInputChecks.Move(screen, session);
        Require(screen.IsPresenting && screen.Board.IsPresenting, "Busy-save test requires a real pending animation.");
        // The off-tree host deliberately has no campaign. Even if the Save guard
        // regresses, evaluating campaign.Save() fails before WriteAllText executes.
        // Load only targets the isolated user-data directory supplied by the runner.
        var game = new GrayFlagGame(); SetField(game, "screen", screen);
        SetField(game, "session", session);
        var savePaths = typeof(GrayFlagGame).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => field.GetRawConstantValue() as string).Where(value => value?.StartsWith("user://", StringComparison.Ordinal) == true)
            .SelectMany(value => new[] { ProjectSettings.GlobalizePath(value!), ProjectSettings.GlobalizePath(value!) + ".tmp" }).ToArray();
        var stamps = savePaths.Select(FileStamp).ToArray();
        var feedback = Field<Label>(screen, "_feedback").Text;
        var messages = screen.RecentMessages.ToArray();
        var revision = session.GetSnapshot().Revision;
        foreach (var method in new[] { "Save", "Load" })
        {
            Invoke(game, method);
            Require(Field<Label>(screen, "_feedback").Text == feedback && screen.RecentMessages.SequenceEqual(messages), $"Busy direct {method} was not ignored.");
        }
        foreach (var key in new[] { Key.F5, Key.F9 })
        {
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
            Require(Field<Label>(screen, "_feedback").Text == feedback && screen.RecentMessages.SequenceEqual(messages), $"Busy {key} was not ignored.");
        }
        game._Process(1);
        Require(!Field<AiTurnPacer>(game, "computerPacer").IsRunning, "Busy presentation must not start the AI pacing loop.");
        Require(screen.IsPresenting && session.GetSnapshot().Revision == revision, "Busy save/load altered the committed revision or canceled playback.");
        var invoked = 0;
        // Save/load intentionally delegate to the Game guard; restart has an
        // additional screen-level idle gate because it replaces the binding.
        screen.BindApplicationActions(() => Invoke(game, "Save"), () => Invoke(game, "Load"), () => invoked++);
        foreach (var button in Descendants(screen).OfType<Button>().Where(button => new[] { "保存", "读取", "重新开始" }.Contains(button.Text))) Press(button);
        Require(invoked == 0 && !Descendants(screen).OfType<ConfirmationDialog>().Any(dialog => dialog.Visible), "Busy screen restart was not ignored.");
        Require(Field<Label>(screen, "_feedback").Text == feedback && screen.RecentMessages.SequenceEqual(messages), "Busy application save/load button bypassed the Game guard.");
        Require(savePaths.Select(FileStamp).SequenceEqual(stamps), "Busy Save/Load changed manual/autosave files or their temporary files.");
        for (var step = 0; step < 100 && screen.IsPresenting; step++) screen.Board.AdvancePresentation(0.1);
        Require(!screen.IsPresenting && screen.DisplayedRevision == revision, "Busy gates prevented normal timeline settlement.");
        screen.BindApplicationActions(() => invoked++);
        var oldSave = Descendants(screen).OfType<Button>().Single(button => button.Text == "保存");
        Press(oldSave);
        Require(invoked == 1, "Settled screen did not restore application actions.");
        screen.BindApplicationActions(() => invoked += 10);
        Press(oldSave);
        Require(invoked == 1, "Rebinding application buttons in the same session left an old Save callback active.");
        var newSave = Descendants(screen).OfType<Button>().Single(button => button.Text == "保存");
        Press(newSave);
        Require(invoked == 11, "The replacement application button did not use the fresh binding.");
        newSave.GetParent().RemoveChild(newSave); Press(newSave);
        Require(invoked == 11, "An individually detached application button invoked its old callback.");
        newSave.QueueFree();
        game.Free(); Retire(screen);
    }

    private void CheckPrebattleAutosave()
    {
        var userData = RequireIsolatedUserData();
        string SaveFile(string constant)
        {
            var resource = (string)typeof(GrayFlagGame).GetField(constant, BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            var path = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath(resource));
            Require(path.StartsWith(userData + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal), "Save path escapes isolated user data.");
            Require(!System.IO.File.Exists(path) && !System.IO.File.Exists(path + ".tmp"), "Autosave fixture refuses to overwrite an existing save or temporary save.");
            return path;
        }
        var manualPath = SaveFile("SavePath");
        var automaticPath = SaveFile("CampaignSavePath");
        Require(manualPath != automaticPath, "Automatic and manual save paths must be separate.");
        var game = new GrayFlagGame { ProcessMode = ProcessModeEnum.Disabled }; AddChild(game);
        try
        {
            var campaign = Field<GrayFlagCampaign>(game, "campaign");
            // This generated manual-save fixture is intentionally distinct from
            // the later battle-node snapshot. Only isolated temporary files are allowed.
            var manual = EcmaJson.Stringify(campaign.Save());
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manualPath)!);
            System.IO.File.WriteAllText(manualPath, manual);
            var manualStamp = FileStamp(manualPath);
            while (campaign.Node()["type"]!.GetValue<string>() != "battle")
            {
                if (campaign.Node()["type"]!.GetValue<string>() == "choice")
                    campaign.Choose(campaign.Choices()[0]!["id"]!.GetValue<string>());
                else campaign.Advance();
            }
            var clean = campaign.Save()["campaign"]!.AsObject();
            var cleanText = StableSave(clean);
            Require(!campaign.HasPendingBattle && campaign.Battle is null && clean["state"]!["pendingBattle"] is null,
                "Autosave fixture must begin at a clean battle node without a pending request.");
            Invoke(game, "ShowStaging");
            var staging = Field<Control>(game, "view") as CampaignStagingScreen ?? throw new Exception("Clean battle node did not show staging.");
            var start = Buttons(staging).Single(button => button.Name == "StartBattle");
            Require(!System.IO.File.Exists(automaticPath) && StableSave(campaign.Save()["campaign"]!.AsObject()) == cleanText,
                "Merely showing staging must not autosave or begin the battle.");
            var earliestSave = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Press(start);
            var latestSave = DateTimeOffset.UtcNow;
            Require(campaign.HasPendingBattle && campaign.Battle is not null && Field<Control>(game, "view") is BattleScreen,
                "Starting from staging did not begin the pending battle.");
            Require(System.IO.File.Exists(automaticPath) && !System.IO.File.Exists(automaticPath + ".tmp"),
                "Starting did not finish the atomic automatic-save write.");
            var automatic = System.IO.File.ReadAllText(automaticPath);
            var saved = EcmaJson.Parse(automatic)!.AsObject();
            Require(StableSave(saved) == cleanText, "Automatic save is not the exact clean pre-BeginBattle document apart from its save timestamp.");
            Require(DateTimeOffset.TryParse(saved["savedAt"]!.GetValue<string>(), out var savedAt)
                && savedAt >= earliestSave && savedAt <= latestSave, "Automatic save timestamp is not from the explicit Start action.");
            Require(!saved.ContainsKey("request") && !saved.ContainsKey("battle") && saved["state"]!["pendingBattle"] is null
                && !saved.ContainsKey("events") && !saved.ContainsKey("atCamp"), "Automatic save contains a pending request, battle, or battle events.");
            Require(System.IO.File.ReadAllText(manualPath) == manual && FileStamp(manualPath) == manualStamp,
                "Prebattle autosave overwrote the separate manual save.");
            var automaticStamp = FileStamp(automaticPath);
            var active = campaign.Battle; var activeView = Field<Control>(game, "view");
            var activeSave = StableSave(campaign.Save());
            Press(start); Press(start);
            Require(ReferenceEquals(campaign.Battle, active) && ReferenceEquals(Field<Control>(game, "view"), activeView)
                && StableSave(campaign.Save()) == activeSave && FileStamp(automaticPath) == automaticStamp,
                "Repeated stale Start restarted the battle or rewrote the automatic save.");

            Invoke(game, "ExitBattle");
            Require(campaign.AtCamp && campaign.HasPendingBattle && Field<Control>(game, "view") is CampaignStagingScreen,
                "Exiting the battle did not retain its request at camp.");
            var campView = Field<Control>(game, "view");
            var campSave = StableSave(campaign.Save());
            Press(start);
            Require(ReferenceEquals(Field<Control>(game, "view"), campView) && StableSave(campaign.Save()) == campSave,
                "Original stale Start navigated from the new camp binding.");
            var reenter = Buttons(campView).Single(button => button.Name == "StartBattle");
            Press(reenter);
            Require(!campaign.AtCamp && campaign.HasPendingBattle && campaign.Battle is not null && Field<Control>(game, "view") is BattleScreen,
                "Camp reentry did not resume the retained battle request.");
            Require(System.IO.File.ReadAllText(automaticPath) == automatic && FileStamp(automaticPath) == automaticStamp,
                "Camp reentry overwrote the clean automatic save with a pending-battle document.");
            Require(System.IO.File.ReadAllText(manualPath) == manual && FileStamp(manualPath) == manualStamp
                && !System.IO.File.Exists(manualPath + ".tmp") && !System.IO.File.Exists(automaticPath + ".tmp"),
                "Camp reentry changed the manual save or left a temporary save.");
            var reentered = campaign.Battle; var reenteredView = Field<Control>(game, "view");
            Press(reenter); Press(start);
            Require(ReferenceEquals(campaign.Battle, reentered) && ReferenceEquals(Field<Control>(game, "view"), reenteredView)
                && FileStamp(automaticPath) == automaticStamp, "Stale camp Start replayed reentry or rewrote the clean automatic save.");
        }
        finally { Retire(game); }
    }

    private string RequireIsolatedUserData()
    {
        var configured = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Require(!string.IsNullOrWhiteSpace(configured) && System.IO.Path.IsPathFullyQualified(configured),
            "Save regression requires explicit isolated XDG_DATA_HOME; real user saves are never test targets.");
        var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        var isolatedRoot = System.IO.Path.GetFullPath(configured!).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        var relative = System.IO.Path.GetRelativePath(temporaryRoot, isolatedRoot);
        Require(!System.IO.Path.IsPathFullyQualified(relative) && relative.Split(System.IO.Path.DirectorySeparatorChar)[0].StartsWith("empire-", StringComparison.Ordinal),
            "Save regression requires a generated empire-* directory below the system temporary directory.");
        var userData = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        Require(userData.StartsWith(isolatedRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "Godot user data does not honor the isolated XDG_DATA_HOME.");
        for (var directory = new System.IO.DirectoryInfo(userData); directory is not null && directory.FullName != temporaryRoot; directory = directory.Parent)
            Require(!directory.Exists || (directory.Attributes & System.IO.FileAttributes.ReparsePoint) == 0,
                "Save regression refuses symbolic links in the isolated user-data path.");
        return userData;
    }

    private static string StableSave(JsonObject document)
    {
        // Save envelopes timestamp serialization, not campaign/battle mutations.
        var copy = document.DeepClone().AsObject();
        if (copy.ContainsKey("state")) copy["savedAt"] = "2026-10-20T00:00:00.000Z";
        else copy["campaign"]!["savedAt"] = "2026-10-20T00:00:00.000Z";
        if (copy["battle"] is JsonObject battle) battle["savedAt"] = "2026-10-20T00:00:00.000Z";
        return EcmaJson.Stringify(copy);
    }

    private static T Field<T>(object instance, string name) => (T)(typeof(GrayFlagGame).IsInstanceOfType(instance)
        ? typeof(GrayFlagGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!
        : typeof(BattleScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!);
    private static void SetField(object instance, string name, object? value) =>
        typeof(GrayFlagGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    private static void Invoke(object instance, string name) =>
        typeof(GrayFlagGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null);
    private void Check(string name, Action test)
    {
        try { test(); }
        catch (Exception error) { failures.Add(name + ": " + error); }
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string FileStamp(string path)
    {
        var file = new System.IO.FileInfo(path);
        return file.Exists ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}" : "<absent>";
    }
    private static void Press(Button button) => button.EmitSignal(Button.SignalName.Pressed);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static IEnumerable<Button> Buttons(Node root) => Descendants(root).OfType<Button>();
    private static IEnumerable<string> Labels(Node root) => Descendants(root).OfType<Label>().Select(label => label.Text);
    private static void Retire(Node node) { node.GetParent()?.RemoveChild(node); node.QueueFree(); }
}
