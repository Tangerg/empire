#nullable enable
using System.Reflection;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Localization;
using Empire.Core.Determinism;
using Godot;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Empire.Presentation;

/// <summary>Real scene callbacks against generated, isolated saves. Terminal states are UI fixtures, not victorious routes.</summary>
public partial class CampaignNavigationChecks : Node
{
    private readonly List<string> failures = new();
    private int assertions;
    private string userData = "";
    private string automaticPath = "";
    private string manualPath = "";
    private string manualText = "";
    private string manualStamp = "";
    private JsonObject definition = null!;
    private JsonObject battleNodeState = null!;
    private GrayFlagGame? paintedCaptureGame;

    public override async void _Ready()
    {
        try
        {
            RequireFreshIsolatedData();
            definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
            var runtime = new CampaignRuntime(CampaignRules.Create(), definition);
            while (runtime.Node()["type"]!.GetValue<string>() != "battle")
            {
                if (runtime.Node()["type"]!.GetValue<string>() == "choice")
                    runtime.Choose(runtime.Choices()[0]!["id"]!.GetValue<string>());
                else
                    runtime.Advance();
            }
            battleNodeState = runtime.Snapshot();
            foreach (var (name, test) in new (string, Action)[] {
                ("fresh menu, local beats, choice and autosave", CheckFreshStoryChoice),
                ("startup resume, battle restart, camp and Continue", CheckBattleCampMenu),
                ("story/choice return, rebind and unmount", CheckNarrativeNavigation),
                ("overwrite cancellation, stale dialog and confirmation", CheckOverwrite),
                ("overwrite dialog unmount", CheckUnmountedOverwrite),
                ("invalid save notice and explicit replacement", CheckInvalidSave),
                ("ending active/completed/failed UI fixtures", CheckEndingFixtures),
                ("story, choice, ending and staging write failures", CheckWriteFailures),
                ("victory settlement UI fixture and autosave", CheckVictorySettlement),
                ("read/delete failure preserves menu", CheckReadDeleteFailure),
                ("manual load admission and view activation are atomic", CheckManualLoadAdmission),
                ("formal Redstone entry, carried-roster fixture, resume and settlement", CheckFormalRedstone),
            })
            {
                try
                {
                    test();
                    if (paintedCaptureGame is not null)
                    {
                        // Navigation fixtures suppress processing; this live interaction must run real animation frames.
                        paintedCaptureGame.ProcessMode = ProcessModeEnum.Inherit;
                        var screen = View<BattleScreen>(paintedCaptureGame);
                        var civilian = Campaign(paintedCaptureGame).Battle!.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                            .Single(unit => unit["key"]!.GetValue<string>() == "refugee-2");
                        screen.SelectCell(new(13, 12));
                        Require(screen.SelectedUnitId == civilian["id"]!.GetValue<int>(), "The formal escort actor could not be selected on its open-road starting cell.");
                        screen.SelectCell(new(13, 13));
                        while (screen.IsInputBlocked)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var moved = Campaign(paintedCaptureGame).Battle!.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                            .Single(unit => unit["key"]!.GetValue<string>() == "refugee-2");
                        Require(moved["x"]!.GetValue<int>() == 13 && moved["y"]!.GetValue<int>() == 13 && moved["done"]!.GetValue<bool>(),
                            "The real destination callback did not commit the civilian's first escort move.");
                        CheckFormalSaveLoad(paintedCaptureGame);
                        var captureArgument = OS.GetCmdlineUserArgs().SingleOrDefault(value => value.StartsWith("--capture-c01-02=", StringComparison.Ordinal));
                        if (captureArgument is not null)
                        {
                            var path = captureArgument["--capture-c01-02=".Length..];
                            Require(Path.IsPathFullyQualified(path) && DisplayServer.GetName() != "headless",
                                "Formal battle capture requires an absolute output path and a real display.");
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            using var image = GetViewport().GetTexture().GetImage();
                            Require(image.SavePng(path) == Error.Ok, "Formal battle viewport capture failed.");
                            GD.Print("Formal c01-02 viewport captured: " + path);
                        }
                    }
                    CheckManualUnchanged();
                    GD.Print("Campaign navigation group passed: " + name);
                }
                catch (Exception error) { failures.Add(name + ": " + error); }
                paintedCaptureGame = null;
                foreach (var child in GetChildren())
                    Retire(child);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception error) { failures.Add("Safety/setup: " + error); }
        foreach (var failure in failures)
            GD.PushError(failure);
        GD.Print($"Campaign navigation native checks {(failures.Count == 0 ? "passed" : "failed")}: {assertions} assertions; {failures.Count} failed groups. Ending states are explicit UI fixtures, not campaign outcome parity.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }

    private void CheckFreshStoryChoice()
    {
        var game = MountGame();
        var menu = View<CampaignMenuScreen>(game);
        Require(!menu.Presented!.CanContinue && !Buttons(menu).Any(button => button.Name == "ContinueCampaign"), "Fresh launch offers a nonexistent Continue slot.");
        Require(!File.Exists(automaticPath), "Fresh launch wrote a campaign save.");
        manualText = EcmaJson.Stringify(Campaign(game).Save());
        File.WriteAllText(manualPath, manualText);
        manualStamp = Stamp(manualPath);
        var start = Button(menu, "NewCampaign");
        Press(start);
        Require(View<StoryScreen>(game).BeatIndex == 0 && !File.Exists(automaticPath), "New should open beat zero without an early autosave.");
        Press(start);
        Require(View<StoryScreen>(game).BeatIndex == 0, "Stale New restarted the story.");
        while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
            CompleteStory(game);
        var choices = View<StoryScreen>(game);
        var options = Buttons(choices).Where(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)).ToArray();
        Require(options.Length > 1 && choices.PresentedChoice is not null, "Opening route did not reach its real choice menu.");
        var beforeChoice = State(game);
        var beforeSlot = File.ReadAllText(automaticPath);
        Press(options[0]);
        Require(View<CampaignStagingScreen>(game).Presented is not null && State(game) != beforeChoice, "Choice did not enter staging through canonical runtime.");
        AssertSaved(game);
        Require(File.ReadAllText(automaticPath) != beforeSlot, "Choice failed to write its new campaign state.");
        var committed = State(game);
        var stamp = Stamp(automaticPath);
        var staging = View<Control>(game);
        foreach (var button in options)
        {
            Press(button);
            Press(button);
        }
        Require(State(game) == committed && Stamp(automaticPath) == stamp && ReferenceEquals(View<Control>(game), staging), "Stale choice callback changed campaign or navigation.");
        Retire(game);
    }

    private void CompleteStory(GrayFlagGame game)
    {
        var story = View<StoryScreen>(game);
        var state = State(game);
        var slot = Stamp(automaticPath);
        while (!story.PresentedStory!.LastBeat)
        {
            var old = Button(story, "StoryPrimary");
            var beat = story.BeatIndex;
            Press(old);
            Require(ReferenceEquals(View<Control>(game), story) && story.BeatIndex == beat + 1, "Next beat replaced the campaign screen or skipped a local beat.");
            Require(State(game) == state && Stamp(automaticPath) == slot, "Local paging advanced or persisted the canonical campaign early.");
            Press(old);
            Require(story.BeatIndex == beat + 1, "A removed local-beat button advanced again.");
        }
        var last = Button(story, "StoryPrimary");
        Press(last);
        Require(State(game) != state && !ReferenceEquals(View<Control>(game), story), "Last beat failed to advance the campaign node.");
        AssertSaved(game);
        var next = View<Control>(game);
        var committed = State(game);
        var saved = Stamp(automaticPath);
        Press(last);
        Press(Button(story, "CampaignExit"));
        Require(ReferenceEquals(View<Control>(game), next) && State(game) == committed && Stamp(automaticPath) == saved, "Old story continue/exit remained live after navigation.");
    }

    private void CheckBattleCampMenu()
    {
        WriteFixture(battleNodeState);
        var savedBefore = File.ReadAllText(automaticPath);
        var game = MountGame();
        var menu = View<CampaignMenuScreen>(game);
        Require(menu.Presented!.CanContinue && File.ReadAllText(automaticPath) == savedBefore, "Startup did not offer the intact campaign slot.");
        Press(Button(menu, "ContinueCampaign"));
        var campaign = Campaign(game);
        var staging = View<CampaignStagingScreen>(game);
        Require(!campaign.HasPendingBattle && campaign.Battle is null && !campaign.AtCamp && State(game) == Digest(battleNodeState), "Startup Continue restored a pending battle instead of the clean campaign node.");
        var start = Button(staging, "StartBattle");
        Press(start);
        var battleScreen = View<BattleScreen>(game);
        var battle = campaign.Battle;
        Require(battle is not null && campaign.HasPendingBattle && campaign.Snapshot()["pendingBattle"] is not null, "Staging Start did not open a canonical battle request.");
        var saved = File.ReadAllText(automaticPath);
        var stamp = Stamp(automaticPath);
        Require(Digest(ReadSave()["state"]!.AsObject()) == Digest(battleNodeState) && ReadSave()["state"]!["pendingBattle"] is null, "Prebattle autosave captured pending gameplay.");
        Press(start);
        Require(ReferenceEquals(campaign.Battle, battle), "Stale staging Start restarted the live battle.");
        var restart = Buttons(battleScreen).Single(button => button.Text == "重新开始");
        Press(restart);
        var dialog = Descendants(battleScreen).OfType<ConfirmationDialog>().Single();
        Require(dialog.Visible, "Restart did not request confirmation.");
        dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(ReferenceEquals(campaign.Battle, battle) && ReferenceEquals(View<Control>(game), battleScreen), "Cancelled restart later executed from a stale confirmation.");
        Press(restart);
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        var restarted = campaign.Battle;
        var nextScreen = View<BattleScreen>(game);
        Require(!ReferenceEquals(restarted, battle) && !ReferenceEquals(nextScreen, battleScreen), "Confirmed restart failed to replace the battle session.");
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Press(restart);
        Require(ReferenceEquals(campaign.Battle, restarted), "Stale restart dialog/button replaced the new session.");
        Press(Buttons(nextScreen).Single(button => button.Text == "重新开始"));
        var staleDialog = Descendants(nextScreen).OfType<ConfirmationDialog>().Single();
        Press(Buttons(nextScreen).Single(button => button.Text == "战役营地"));
        var camp = View<CampaignStagingScreen>(game);
        var pending = State(game);
        var opening = campaign.Battle;
        Require(campaign.AtCamp && campaign.HasPendingBattle && opening is not null, "Battle exit failed to retain the request at camp.");
        staleDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(ReferenceEquals(View<Control>(game), camp) && ReferenceEquals(campaign.Battle, opening) && State(game) == pending, "Stale restart escaped to the old battle from camp.");
        Require(File.ReadAllText(automaticPath) == saved && Stamp(automaticPath) == stamp, "Restart/camp exit rewrote the clean campaign slot.");
        var exit = Button(camp, "CampaignExit");
        Press(exit);
        var returnedMenu = View<CampaignMenuScreen>(game);
        Press(exit);
        Press(Button(camp, "StartBattle"));
        Require(ReferenceEquals(View<Control>(game), returnedMenu), "Camp navigation remained live after returning to menu.");
        Press(Button(returnedMenu, "ContinueCampaign"));
        Require(View<Control>(game) is CampaignStagingScreen && !Campaign(game).HasPendingBattle && Campaign(game).Battle is null && !Campaign(game).AtCamp,
            "Menu Continue retained the in-memory pending request instead of loading clean campaign state.");
        Require(State(game) == Digest(battleNodeState) && File.ReadAllText(automaticPath) == saved, "Menu Continue changed the clean saved campaign.");
        Retire(game);
    }

    private void CheckNarrativeNavigation()
    {
        foreach (var target in new[] { "story", "choice" })
        {
            var runtime = new CampaignRuntime(CampaignRules.Create(), definition);
            while (runtime.Node()["type"]!.GetValue<string>() != target)
                runtime.Advance();
            WriteFixture(runtime.Snapshot());
            var game = MountGame();
            Press(Button(View<Control>(game), "ContinueCampaign"));
            var story = View<StoryScreen>(game);
            var state = State(game);
            var saved = File.ReadAllText(automaticPath);
            if (target == "story")
            {
                Require(story.PresentedStory!.BeatCount > 1, "The cursor regression needs a real multi-beat story.");
                Press(Button(story, "StoryPrimary"));
                Require(story.BeatIndex == 1, "Story fixture did not move its local cursor.");
            }
            var oldButtons = Buttons(story).ToArray();
            var exit = Button(story, "CampaignExit");
            Press(exit);
            var menu = View<CampaignMenuScreen>(game);
            foreach (var button in oldButtons)
                Press(button);
            Require(ReferenceEquals(View<Control>(game), menu) && State(game) == state && File.ReadAllText(automaticPath) == saved, "Story/choice exit or stale siblings mutated canonical progress.");
            Press(Button(menu, "ContinueCampaign"));
            var resumed = View<StoryScreen>(game);
            Require(resumed.BeatIndex == 0 && State(game) == state, "Continue should resume the saved node at local beat zero.");
            var bound = Buttons(resumed).ToArray();
            RemoveChild(game);
            try
            {
                foreach (var button in bound)
                    Press(button);
                Require(State(game) == state && File.ReadAllText(automaticPath) == saved, "Unmounted narrative callback changed the campaign or slot.");
                AddChild(game);
                foreach (var button in bound)
                    Press(button);
                Require(ReferenceEquals(View<Control>(game), resumed) && State(game) == state, "Remount revived an obsolete narrative callback.");
            }
            finally { Retire(game); }
        }
    }

    private void CheckOverwrite()
    {
        WriteFixture(battleNodeState);
        var game = MountGame();
        var menu = View<CampaignMenuScreen>(game);
        var original = File.ReadAllText(automaticPath);
        var stamp = Stamp(automaticPath);
        Press(Button(menu, "NewCampaign"));
        var dialog = Descendants(menu).OfType<ConfirmationDialog>().Single();
        Require(dialog.Visible && ReferenceEquals(View<Control>(game), menu) && File.ReadAllText(automaticPath) == original, "Requesting overwrite modified the existing slot before approval.");
        Press(Button(menu, "ContinueCampaign"));
        Require(ReferenceEquals(View<Control>(game), menu), "Sibling Continue escaped the pending overwrite dialog.");
        dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        var rebound = View<CampaignMenuScreen>(game);
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        dialog.EmitSignal(Window.SignalName.CloseRequested);
        Require(!ReferenceEquals(rebound, menu) && ReferenceEquals(View<Control>(game), rebound) && File.ReadAllText(automaticPath) == original && Stamp(automaticPath) == stamp,
            "Cancelled overwrite or its stale callbacks cleared progress.");
        Press(Button(rebound, "NewCampaign"));
        var closed = Descendants(rebound).OfType<ConfirmationDialog>().Single();
        closed.EmitSignal(Window.SignalName.CloseRequested);
        closed.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(View<Control>(game) is CampaignMenuScreen && File.ReadAllText(automaticPath) == original, "Closing overwrite failed to cancel it.");
        var freshMenu = View<CampaignMenuScreen>(game);
        Press(Button(freshMenu, "NewCampaign"));
        var confirmed = Descendants(freshMenu).OfType<ConfirmationDialog>().Single();
        confirmed.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        var story = View<StoryScreen>(game);
        var state = State(game);
        Require(!File.Exists(automaticPath) && Campaign(game).Node()["id"]!.GetValue<string>() == definition["start"]!.GetValue<string>(), "Confirmed New did not clear only autosave and start from the canonical opening.");
        confirmed.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        confirmed.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        Press(Button(freshMenu, "ContinueCampaign"));
        Require(ReferenceEquals(View<Control>(game), story) && State(game) == state, "Handled overwrite dialog or old Continue navigated again.");
        Retire(game);
    }

    private void CheckUnmountedOverwrite()
    {
        foreach (var confirmWhileUnmounted in new[] { true, false })
        {
            WriteFixture(battleNodeState);
            var game = MountGame();
            var menu = View<CampaignMenuScreen>(game);
            var saved = File.ReadAllText(automaticPath);
            Press(Button(menu, "NewCampaign"));
            var dialog = Descendants(menu).OfType<ConfirmationDialog>().Single();
            RemoveChild(game);
            try
            {
                if (confirmWhileUnmounted)
                    dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
                Require(File.Exists(automaticPath) && File.ReadAllText(automaticPath) == saved && ReferenceEquals(View<Control>(game), menu), "Unmounted overwrite dialog cleared saved progress.");
                AddChild(game);
                dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
                Require(File.Exists(automaticPath) && File.ReadAllText(automaticPath) == saved && ReferenceEquals(View<Control>(game), menu), "Remount revived stale overwrite confirmation.");
            }
            finally { Retire(game); }
        }
    }

    private void CheckInvalidSave()
    {
        foreach (var rejected in new[] { "{not a campaign save", "{\"schema\":0}" })
        {
            ResetAutosave();
            File.WriteAllText(automaticPath, rejected);
            var stamp = Stamp(automaticPath);
            var game = MountGame();
            var menu = View<CampaignMenuScreen>(game);
            var notice = Field<GameText>(game, "text").Format("error.campaign.save_rejected");
            Require(!menu.Presented!.CanContinue && menu.Presented.RejectedNotice == notice && Labels(menu).Any(label => label == notice), "Malformed or unsupported slot did not produce a visible localized non-destructive notice.");
            Require(File.ReadAllText(automaticPath) == rejected && Stamp(automaticPath) == stamp, "Reading a malformed or unsupported save changed or deleted it.");
            Press(Button(menu, "NewCampaign"));
            Require(View<Control>(game) is StoryScreen && !File.Exists(automaticPath), "Explicit New did not replace the rejected slot.");
            Retire(game);
        }
    }

    private void CheckEndingFixtures()
    {
        // This deliberately constructs valid terminal-screen fixtures. It does not assert that gameplay reached these nodes.
        foreach (var (node, status) in new[] { ("chapter-three-ending", "active"), ("chapter-three-ending", "completed"), ("campaign-failed", "active"), ("campaign-failed", "failed") })
        {
            var fixture = new CampaignRuntime(CampaignRules.Create(), definition).Snapshot();
            fixture["currentNode"] = node;
            fixture["status"] = status;
            WriteFixture(fixture);
            var game = MountGame();
            Press(Button(View<Control>(game), "ContinueCampaign"));
            var story = View<StoryScreen>(game);
            var finish = Button(story, "StoryPrimary");
            var saved = File.ReadAllText(automaticPath);
            Require(story.BeatIndex == 0 && story.PresentedStory!.Ending, "Ending must render beat zero with finish, not page the multi-beat document.");
            var text = Field<GameText>(game, "text");
            Require(finish.Text == (status == "active" ? text.Format(NarrativePresentationDocument.Load(text, PortraitCatalogResources.Load()).CompletionLabelKey) : text.Format("ui.campaign.exit")), "Ending finish/return label does not reflect canonical status.");
            Press(finish);
            if (status == "active")
            {
                var terminal = View<StoryScreen>(game);
                var expected = node == "campaign-failed" ? "failed" : "completed";
                Require(Campaign(game).Snapshot()["status"]!.GetValue<string>() == expected && terminal.BeatIndex == 0 && !ReferenceEquals(terminal, story), "First active finish did not advance, persist and rerender the same ending at beat zero.");
                AssertSaved(game);
                var committed = State(game);
                var after = File.ReadAllText(automaticPath);
                Press(finish);
                Require(State(game) == committed && ReferenceEquals(View<Control>(game), terminal), "Stale active ending finish exited or advanced twice.");
                Press(Button(terminal, "StoryPrimary"));
                Require(View<Control>(game) is CampaignMenuScreen && File.ReadAllText(automaticPath) == after, "Second finish must only exit completed/failed ending.");
            }
            else
                Require(View<Control>(game) is CampaignMenuScreen && File.ReadAllText(automaticPath) == saved, "Terminal ending exit mutated saved campaign progress.");
            Retire(game);
        }
    }

    private void CheckWriteFailures()
    {
        foreach (var target in new[] { "story", "choice", "ending", "battle" })
        {
            ResetAutosave();
            var runtime = new CampaignRuntime(CampaignRules.Create(), definition);
            if (target == "ending")
            {
                var ending = runtime.Snapshot();
                ending["currentNode"] = "chapter-three-ending";
                runtime = new CampaignRuntime(CampaignRules.Create(), definition, ending);
            }
            else if (target != "story")
                while (runtime.Node()["type"]!.GetValue<string>() != target)
                {
                    if (runtime.Node()["type"]!.GetValue<string>() == "choice")
                        runtime.Choose(runtime.Choices()[0]!["id"]!.GetValue<string>());
                    else
                        runtime.Advance();
                }
            WriteFixture(runtime.Snapshot());
            var game = MountGame();
            Press(Button(View<Control>(game), "ContinueCampaign"));
            var before = State(game);
            var previous = File.ReadAllText(automaticPath);
            File.Delete(automaticPath);
            Directory.CreateDirectory(automaticPath);
            try
            {
                if (target is "story" or "ending")
                {
                    var story = View<StoryScreen>(game);
                    while (target != "ending" && !story.PresentedStory!.LastBeat)
                        Press(Button(story, "StoryPrimary"));
                    Press(Button(story, "StoryPrimary"));
                }
                else if (target == "choice")
                    Press(Buttons(View<Control>(game)).First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
                else
                    Press(Button(View<Control>(game), "StartBattle"));
                Require(Descendants(game).OfType<AcceptDialog>().Any(dialog => dialog.DialogText.Contains("自动保存失败", StringComparison.Ordinal)), "Failed campaign write was not surfaced to the player.");
                if (target == "battle")
                    Require(State(game) == before && Campaign(game).Battle is null && !Campaign(game).HasPendingBattle && View<Control>(game) is CampaignStagingScreen,
                    "Failed prebattle write opened a battle or changed the campaign.");
                else
                    Require(State(game) != before, "Failed post-transition autosave lost the current in-memory progress.");
                Require(Directory.Exists(automaticPath) && !Directory.EnumerateFiles(userData, ".grayflag-campaign-autosave.json.*.tmp").Any(), "Failed atomic write removed the obstruction or leaked temporary data.");
            }
            finally { Directory.Delete(automaticPath); File.WriteAllText(automaticPath, previous); Retire(game); }
        }
    }

    private void CheckVictorySettlement()
    {
        // Explicit current-native terminal fixtures drive UI only, not a claimed victorious gameplay route.
        foreach (var action in new[] { "Proceed", "CampaignExit", "failed-write" })
        {
            WriteFixture(battleNodeState);
            var game = MountGame();
            Press(Button(View<Control>(game), "ContinueCampaign"));
            Press(Button(View<Control>(game), "StartBattle"));
            var campaign = Campaign(game);
            var document = campaign.Save();
            var terminal = campaign.Battle!.Snapshot();
            var conclusion = BattleConclusion.LastTeamRemaining(1);
            foreach (var player in terminal["players"]!.AsArray().OfType<JsonObject>())
                player["alive"] = player["team"]!.GetValue<int>() == conclusion.Team;
            terminal["phase"] = "over";
            terminal["conclusion"] = conclusion.ToJson();
            terminal["scenario"]!["eventCounts"]!["gameOver"] = 1;
            document["battle"] = Field<BattleEngine>(game, "engine").SaveBattle(terminal);
            document["events"] = new JsonArray(new JsonObject { ["type"] = "gameOver", ["conclusion"] = conclusion.ToJson() });
            campaign.Load(document);
            SetField(game, "session", campaign.Battle);
            SetField(game, "screen", null);
            SetField(game, "resultOffered", false);
            game._Process(0);
            var completion = View<BattleResultScreen>(game);
            var previous = File.ReadAllText(automaticPath);
            var before = State(game);
            if (action == "failed-write")
            {
                File.Delete(automaticPath);
                Directory.CreateDirectory(automaticPath);
            }
            try
            {
                var settle = Button(completion, "Settle");
                Press(settle);
                var summary = View<BattleResultScreen>(game);
                Require(!ReferenceEquals(summary, completion) && !campaign.HasPendingBattle && campaign.Battle is null && State(game) != before,
                    "Victory UI fixture was not settled by the canonical campaign owner.");
                var committed = State(game);
                var actions = Buttons(summary).ToArray();
                Require(actions.Any(button => button.Name == "Proceed") && actions.Any(button => button.Name == "CampaignExit"), "Settled summary must expose both aftermath and shared-shell menu exit.");
                Press(settle);
                Require(State(game) == committed && ReferenceEquals(View<Control>(game), summary), "Stale Settle committed twice.");
                if (action == "failed-write")
                {
                    Require(Descendants(game).OfType<AcceptDialog>().Any(dialog => dialog.DialogText.Contains("自动保存失败", StringComparison.Ordinal)), "Failed settlement autosave was not reported.");
                    Require(Directory.Exists(automaticPath) && State(game) == committed, "Failed settlement persistence lost in-memory results or changed the blocked slot.");
                }
                else
                {
                    AssertSaved(game);
                    var saved = File.ReadAllText(automaticPath);
                    Press(Button(summary, action));
                    var next = View<Control>(game);
                    Require(action == "Proceed" ? next is StoryScreen : next is CampaignMenuScreen, "Summary navigation reached the wrong destination.");
                    foreach (var button in actions)
                    {
                        Press(button);
                        Press(button);
                    }
                    Require(ReferenceEquals(View<Control>(game), next) && State(game) == committed && File.ReadAllText(automaticPath) == saved, "Stale summary siblings navigated or rewrote committed progress.");
                    if (action == "Proceed")
                    {
                        CheckFormalPaintedBattle(game);
                        paintedCaptureGame = game;
                    }
                }
            }
            finally
            {
                if (action == "failed-write")
                {
                    Directory.Delete(automaticPath);
                    File.WriteAllText(automaticPath, previous);
                }
                if (!ReferenceEquals(game, paintedCaptureGame))
                    Retire(game);
            }
        }
    }

    private void CheckFormalPaintedBattle(GrayFlagGame game)
    {
        var campaign = Campaign(game);
        foreach (var node in new[] { "aftermath-01", "brief-02" })
        {
            Require(campaign.Node()["id"]!.GetValue<string>() == node, "First-level settlement skipped the authored bridge approach.");
            CompleteStory(game);
        }
        Require(campaign.Node()["id"]!.GetValue<string>() == "choice-bridge-truce", "Bridge approach did not expose its actual truce choice.");
        var relation = EcmaJson.ToElement(campaign.Snapshot()["relations"]!["cain"]!).GetDouble();
        Press(Button(View<StoryScreen>(game), "Choice_accept-truce"));
        Require(campaign.Node()["id"]!.GetValue<string>() == "battle-02"
            && EcmaJson.ToElement(campaign.Snapshot()["relations"]!["cain"]!).GetDouble() == relation + 1,
            "Accepting the bridge truce did not reach second-level staging with its relation effect.");
        var roster = campaign.Snapshot()["roster"]!.DeepClone().AsObject();
        Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
        var battle = campaign.Battle!.Snapshot();
        Require(battle["levelId"]!.GetValue<string>() == "c01-02" && battle["units"]!.AsArray().Count == 20,
            "Formal second-level staging did not launch all twenty canonical actors.");
        var bindings = campaign.RequestSnapshot()!["rosterBindings"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(bindings.Length == 3, "Formal bridge battle lost the three campaign roster bindings.");
        var engine = Field<BattleEngine>(game, "engine");
        foreach (var binding in bindings)
        {
            var persistent = roster[binding["campaignUnit"]!.GetValue<string>()]!;
            var unit = battle["units"]!.AsArray().OfType<JsonObject>().Single(value => value["key"]!.GetValue<string>() == binding["levelUnitKey"]!.GetValue<string>());
            var type = persistent["unitType"]!.GetValue<string>();
            var maximum = engine.Catalog.Definition("units", type)!.Value.GetProperty("maxHp").GetDouble();
            var expectedHp = Math.Max(1, Math.Floor(maximum * EcmaJson.ToElement(persistent["hpRatio"]!).GetDouble() + .5));
            Require(unit["type"]!.GetValue<string>() == type && EcmaJson.ToElement(unit["hp"]!).GetDouble() == expectedHp
                && EcmaJson.ToElement(unit["rank"]!).GetDouble() == EcmaJson.ToElement(persistent["rank"]!).GetDouble(),
                "Formal bridge launch did not inherit roster type, health and rank.");
        }
        CheckPaintedBoard(View<BattleScreen>(game).Board);
    }

    private void CheckFormalSaveLoad(GrayFlagGame game)
    {
        var campaign = Campaign(game);
        var battle = campaign.Battle!.Snapshot();
        var originalManual = File.ReadAllText(manualPath);
        var originalStamp = File.GetLastWriteTimeUtc(manualPath);
        try
        {
            var before = Digest(battle);
            var campaignBefore = State(game);
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F5 });
            Require(File.ReadAllText(manualPath) != originalManual, "Formal bridge F5 did not write the live battle.");
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(!ReferenceEquals(Campaign(game), campaign) && State(game) == campaignBefore
                && Digest(Campaign(game).Battle!.Snapshot()) == before,
                "Formal bridge F9 changed canonical state or failed to replace the loaded session atomically.");
            CheckPaintedBoard(View<BattleScreen>(game).Board);
        }
        finally
        {
            File.WriteAllText(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalStamp);
        }
    }

    private void CheckPaintedBoard(BoardView board)
    {
        var ground = Descendants(board).OfType<PaintedGroundView>().Single();
        Require(!Descendants(board).OfType<TerrainView>().Any() && board.RenderedUnitCount == 20 && board.RenderingIssueCount == 0,
            "Formal bridge rendering must use its painted ground and all twenty actors without rendering errors.");
        var houses = ground.DepthEntries.Where(entry => entry.SelectionCell is not null).ToArray();
        Require(houses.Length == 4 && houses.All(entry => ground.OwnsMapStructureBody(entry.SelectionCell!.Value)),
            "The four painted houses must own their selectable map-structure bodies.");
        Require(!Descendants(board).Any(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)),
            "Formal bridge houses were duplicated by dynamic map-structure sprites.");
        var banners = Descendants(board).Where(node => node.HasMeta("owner_color")).ToArray();
        Require(banners.Length == 2 && banners.Select(node => node.GetMeta("owner_color").AsString()).Distinct(StringComparer.Ordinal).Count() == 2,
            "The two owned outposts must retain exactly two dynamic ownership flags.");
    }

    private void CheckFormalRedstone()
    {
        // Preserve the existing two-victory trace's campaign facts, not its obsolete battle-save envelope.
        // This is historical carried-state admission, not a fresh c02 playthrough on the current engine.
        using var archive = new System.IO.Compression.GZipStream(File.OpenRead(
            ProjectSettings.GlobalizePath("res://tests/oracle/campaign-victory-opening.jsonl.gz")), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new System.IO.StreamReader(archive);
        JsonObject? carried = null;
        while (reader.ReadLine() is { } line)
        {
            var record = JsonNode.Parse(line)!.AsObject();
            if (record["kind"]!.GetValue<string>() == "final")
                carried = record["final"]!.DeepClone().AsObject();
        }
        Require(carried is not null && carried["currentNode"]?.GetValue<string>() == "aftermath-02"
            && carried["roster"]!["elin"]!["disposition"]!.GetValue<string>() == "fallen",
            "The historical opening must retain its actual aftermath and casualty, not a full-health sample.");
        WriteFixture(carried!);
        var originalManual = File.ReadAllText(manualPath);
        var originalStamp = File.GetLastWriteTimeUtc(manualPath);
        var game = MountGame();
        try
        {
            Press(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
            foreach (var node in new[] { "aftermath-02", "brief-03" })
            {
                Require(Campaign(game).Node()["id"]!.GetValue<string>() == node, "Redstone entry skipped the formal story chain.");
                CompleteStory(game);
            }
            Require(Campaign(game).Node()["id"]!.GetValue<string>() == "battle-03", "Redstone briefing did not lead to its actual staging node.");
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            var campaign = Campaign(game);
            var engine = Field<BattleEngine>(game, "engine");
            var opening = campaign.Battle!.Snapshot();
            Require(opening["levelId"]!.GetValue<string>() == "c01-03"
                && opening["map"]!["width"]!.GetValue<int>() == 101 && opening["map"]!["height"]!.GetValue<int>() == 52,
                "Formal Redstone entry did not select the canonical large map.");
            var units = opening["units"]!.AsArray().OfType<JsonObject>().ToArray();
            Require(units.Length == 18 && units.All(unit => unit["key"]!.GetValue<string>() != "campaign-elin")
                && campaign.RequestSnapshot()!["rosterBindings"]!.AsArray().Count == 2,
                "The fallen archer was silently replaced by the sample roster.");
            foreach (var key in new[] { "laiya", "torren" })
            {
                var persistent = carried!["roster"]![key]!;
                var unit = units.Single(value => value["key"]!.GetValue<string>() == "campaign-" + key);
                var maximum = engine.Catalog.Definition("units", persistent["unitType"]!.GetValue<string>())!.Value.GetProperty("maxHp").GetDouble();
                Require(unit["hp"]!.GetValue<double>() == Math.Max(1, Math.Floor(maximum * persistent["hpRatio"]!.GetValue<double>() + .5))
                    && EcmaJson.DeepEquals(unit["rank"], persistent["rank"])
                    && EcmaJson.DeepEquals(unit["rankProgress"], persistent["rankProgress"])
                    && EcmaJson.DeepEquals(unit["resources"], persistent["resources"]), "Redstone did not carry the existing roster health/rank/progress/resources.");
            }
            void Painted()
            {
                var board = View<BattleScreen>(game).Board;
                var ground = Descendants(board).OfType<PaintedGroundView>().Single();
                Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == 18
                    && !Descendants(board).OfType<TerrainView>().Any(), "Formal Redstone did not render its carried roster on its only painted ground.");
                Require(ground.DepthEntries.Count(entry => entry.SelectionCell is not null) == 8
                    && !Descendants(board).Any(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)),
                    "Redstone's eight source-image site bodies must not acquire duplicate sprites.");
            }
            Painted();
            // Exercise the existing dimension boundary without retaining a retired gameplay definition.
            var incompatible = campaign.Save();
            var smallLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
                .OfType<JsonObject>().Single(level => level["id"]!.GetValue<string>() == "c01-01").DeepClone().AsObject();
            smallLevel["id"] = "c01-03";
            incompatible["battle"] = engine.SaveBattle(engine.CreateState(smallLevel));
            var liveBeforeRejection = campaign.Battle;
            try
            {
                campaign.Load(incompatible);
                throw new InvalidOperationException("A differently sized c03 battle save was admitted.");
            }
            catch (StoredDocumentException error)
            {
                Require(error.Message == "Saved battle does not match the campaign level.", "The foreign-map fixture failed before the intended identity boundary.");
            }
            Require(ReferenceEquals(liveBeforeRejection, campaign.Battle) && Digest(campaign.Battle!.Snapshot()) == Digest(opening),
                "Rejecting the retired-size battle contract replaced the active session.");
            var session = campaign.Battle!;
            var actor = units.Single(unit => unit["key"]!.GetValue<string>() == "siege-engineer");
            var path = engine.Space.PathTo(engine.Space.ComputeMoveField(opening, actor), opening, new(65, 23))!;
            var action = new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = actor["id"]!.DeepClone(),
                ["path"] = new JsonArray(path.Select(at => (JsonNode)Empire.Core.Tactics.TacticalQueries.CoordinateJson(at)).ToArray()),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            };
            Require(session.Dispatch(EcmaJson.ToElement(action), session.GetSnapshot().Revision).Accepted,
                "Formal Redstone rejected the unchanged legal main-road move.");
            var played = Digest(session.Snapshot());
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F5 });
            Require(File.ReadAllText(manualPath) != originalManual, "Redstone F5 did not write the live battle.");
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(!ReferenceEquals(Campaign(game), campaign) && Digest(Campaign(game).Battle!.Snapshot()) == played,
                "Redstone F9 did not replace the live session with the same played state.");
            Painted();
            Press(Buttons(View<BattleScreen>(game)).Single(button => button.Text == "战役营地"));
            Require(Campaign(game).AtCamp, "Redstone exit did not return to the normal camp.");
            Press(Button(View<CampaignStagingScreen>(game), "CampaignExit"));
            Press(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            Require(Digest(Campaign(game).Battle!.Snapshot()) == Digest(opening), "Redstone Continue did not rebuild the same carried opening.");
            Painted();

            // A positioned terminal fixture verifies real command -> objective -> application settlement.
            campaign = Campaign(game);
            var document = campaign.Save();
            var nearNode = campaign.Battle!.Snapshot();
            var engineer = nearNode["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "siege-engineer");
            engineer["x"] = 79;
            engineer["y"] = 24;
            document["battle"] = engine.SaveBattle(nearNode);
            campaign.Load(document);
            var finishing = new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = engineer["id"]!.DeepClone(),
                ["path"] = new JsonArray(new JsonObject { ["x"] = 79, ["y"] = 24 }, new JsonObject { ["x"] = 80, ["y"] = 24 }),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            };
            Require(campaign.Battle!.Dispatch(EcmaJson.ToElement(finishing), campaign.Battle.GetSnapshot().Revision).Accepted,
                "Positioned Redstone fixture did not finish via the real close-node command.");
            var settledState = Digest(campaign.Battle.Snapshot());
            var save = campaign.Save();
            campaign.Load(save);
            Require(Digest(campaign.Battle!.Snapshot()) == settledState, "Redstone terminal save lost its objectives or release facts.");
            SetField(game, "session", campaign.Battle);
            SetField(game, "screen", null);
            SetField(game, "resultOffered", false);
            game._Process(0);
            Press(Button(View<BattleResultScreen>(game), "Settle"));
            Require(campaign.Snapshot()["roster"]!["elin"]!["disposition"]!.GetValue<string>() == "fallen",
                "Redstone settlement revived a roster casualty absent from the battle.");
            Press(Button(View<BattleResultScreen>(game), "Proceed"));
            Require(campaign.Node()["id"]!.GetValue<string>() == "aftermath-03" && View<Control>(game) is StoryScreen,
                "Redstone settlement did not reach the formal aftermath.");
            AssertSaved(game);
        }
        finally
        {
            File.WriteAllText(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalStamp);
            Retire(game);
        }
    }

    private void CheckReadDeleteFailure()
    {
        ResetAutosave();
        Directory.CreateDirectory(automaticPath);
        try
        {
            var game = MountGame();
            var menu = View<CampaignMenuScreen>(game);
            Require(menu.Presented!.RejectedNotice is not null && !menu.Presented.CanContinue, "Unreadable slot must expose a rejection notice.");
            Press(Button(menu, "NewCampaign"));
            Require(View<Control>(game) is CampaignMenuScreen && Directory.Exists(automaticPath), "Failed New deletion entered a campaign or removed unreadable data.");
            Require(Descendants(game).OfType<AcceptDialog>().Any(dialog => dialog.DialogText.Contains("无法开始战役", StringComparison.Ordinal)), "Failed explicit New did not report the storage blocker.");
            Retire(game);
        }
        finally { Directory.Delete(automaticPath); }
    }

    private void CheckManualLoadAdmission()
    {
        WriteFixture(battleNodeState);
        var originalManual = File.ReadAllText(manualPath);
        var originalStamp = File.GetLastWriteTimeUtc(manualPath);
        var game = MountGame();
        Press(Button(View<Control>(game), "ContinueCampaign"));
        Press(Button(View<Control>(game), "StartBattle"));
        var activeCampaign = Campaign(game);
        var activeSession = activeCampaign.Battle ?? throw new InvalidOperationException("Manual admission fixture did not enter battle.");
        var battleBefore = Digest(activeSession.Snapshot());
        var activeView = View<Control>(game);
        var before = State(game);
        var stories = Field<StoryDocument>(game, "stories");
        try
        {
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            var engine = new BattleEngine(packs);
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentationText = BattleTextChecks.Session(engine, text);
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var candidate = new GrayFlagCampaign(new(engine, presentationText), definition, levels, battleNodeState);
            candidate.BeginBattle();
            var invalid = candidate.Save();
            var obsoleteRequest = candidate.RequestSnapshot()!;
            obsoleteRequest.Remove("level");
            invalid["request"] = obsoleteRequest;
            SaveFile.WriteDocument(manualPath, invalid);
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(ReferenceEquals(Campaign(game), activeCampaign) && ReferenceEquals(View<Control>(game), activeView) && State(game) == before,
                "Malformed manual request replaced the campaign or visible screen.");
            Require(Labels(activeView).Contains(text.Format("error.load.failed_unchanged")),
                "Rejected manual document did not report a recoverable input failure.");
            DismissNotices(game);

            var obsolete = candidate.Save();
            obsolete["schema"] = 2;
            SaveFile.WriteDocument(manualPath, obsolete);
            var obsoleteBytes = File.ReadAllBytes(manualPath);
            var obsoleteStamp = Stamp(manualPath);
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(ReferenceEquals(Campaign(game), activeCampaign) && ReferenceEquals(View<Control>(game), activeView) && State(game) == before,
                "Unsupported application save version replaced the active campaign or screen.");
            Require(File.ReadAllBytes(manualPath).SequenceEqual(obsoleteBytes) && Stamp(manualPath) == obsoleteStamp,
                "Rejecting an unsupported application save rewrote or deleted its original bytes.");

            foreach (var obsoletePart in new[] { "schema", "battle", "state" })
            {
                DismissNotices(game);
                var obsoleteBattle = candidate.Save();
                if (obsoletePart == "schema")
                    obsoleteBattle["battle"]!["schema"] = 2;
                else
                    obsoleteBattle["battle"]![obsoletePart]!["levelName"] = "Obsolete persisted title";
                SaveFile.WriteDocument(manualPath, obsoleteBattle);
                var bytes = File.ReadAllBytes(manualPath);
                var stamp = Stamp(manualPath);
                game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
                Require(ReferenceEquals(Campaign(game), activeCampaign) && ReferenceEquals(View<Control>(game), activeView) && State(game) == before,
                    "Obsolete battle title/schema replaced the active campaign or screen.");
                Require(File.ReadAllBytes(manualPath).SequenceEqual(bytes) && Stamp(manualPath) == stamp,
                    "Obsolete battle title/schema rejection changed the original file.");
            }
            var creationOpening = candidate.Save();
            foreach (var kind in new[] { "unitSpawned", "recruit" })
            {
                var complete = creationOpening.DeepClone().AsObject();
                var unit = complete["battle"]!["state"]!["units"]!.AsArray().OfType<JsonObject>().First();
                var creation = new JsonObject
                {
                    ["type"] = kind,
                    ["unit"] = unit["id"]!.DeepClone(),
                    ["unitType"] = unit["type"]!.DeepClone(),
                    ["owner"] = unit["owner"]!.DeepClone(),
                    ["at"] = new JsonObject { ["x"] = unit["x"]!.DeepClone(), ["y"] = unit["y"]!.DeepClone() },
                };
                if (kind == "unitSpawned")
                    creation["reason"] = "reinforcement";
                complete["events"]!.AsArray().Add(creation);
                candidate.Load(complete);
                foreach (var field in new[] { "unitType", "owner" })
                {
                    DismissNotices(game);
                    View<BattleScreen>(game).ShowFeedback("");
                    var malformed = complete.DeepClone().AsObject();
                    malformed["events"]!.AsArray().Last()!.AsObject().Remove(field);
                    SaveFile.WriteDocument(manualPath, malformed);
                    var bytes = File.ReadAllBytes(manualPath);
                    var stamp = Stamp(manualPath);
                    game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
                    Require(ReferenceEquals(Campaign(game), activeCampaign) && ReferenceEquals(activeCampaign.Battle, activeSession) &&
                        ReferenceEquals(View<Control>(game), activeView) && State(game) == before && Digest(activeSession.Snapshot()) == battleBefore,
                        $"Saved {kind} missing {field} changed the active campaign, session, battle or screen.");
                    Require(File.ReadAllBytes(manualPath).SequenceEqual(bytes) && Stamp(manualPath) == stamp,
                        $"Rejecting saved {kind} missing {field} changed the original file.");
                    Require(Labels(activeView).Contains(text.Format("error.load.failed_unchanged")),
                        $"Saved {kind} missing {field} did not report a recoverable input failure.");
                }
            }
            DismissNotices(game);
            var opening = new GrayFlagCampaign(new(engine, presentationText), definition, levels);
            SaveFile.WriteDocument(manualPath, opening.Save());
            var children = game.GetChildCount();
            SetField(game, "stories", new StoryDocument());
            var defectPropagated = false;
            try
            {
                game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            }
            catch (InvalidOperationException) { defectPropagated = true; }
            Require(defectPropagated, "Missing narrative definition became a normal load-error message.");
            Require(ReferenceEquals(Campaign(game), activeCampaign) && ReferenceEquals(View<Control>(game), activeView) && State(game) == before,
                "Failed replacement-view preparation changed the active application.");
            Require(activeView.IsInsideTree() && game.GetChildCount() == children,
                "Failed preparation removed the current screen or leaked a hidden replacement.");
            SetField(game, "stories", stories);
            game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
            Require(!ReferenceEquals(Campaign(game), activeCampaign) && View<Control>(game) is StoryScreen,
                "A valid prepared manual load did not activate its replacement campaign and screen together.");
        }
        finally
        {
            SetField(game, "stories", stories);
            File.WriteAllText(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalStamp);
            Retire(game);
        }
    }

    private void AssertSaved(GrayFlagGame game)
    {
        var saved = ReadSave();
        Require(saved.ContainsKey("state") && saved.ContainsKey("savedAt") && !saved.ContainsKey("request") && !saved.ContainsKey("battle") && !saved.ContainsKey("events") && !saved.ContainsKey("atCamp"), "Autosave is not the canonical campaign-only envelope.");
        Require(Digest(CampaignSave.Load(saved, definition)["state"]!.AsObject()) == State(game) && saved["state"]!["pendingBattle"] is null, "Autosave does not represent the exact clean canonical campaign state.");
    }
    private static void DismissNotices(GrayFlagGame game)
    {
        foreach (var dialog in Descendants(game).OfType<AcceptDialog>().ToArray())
        {
            dialog.Hide();
            dialog.Free();
        }
    }
    private void CheckManualUnchanged()
    {
        Require(manualText.Length > 0 && File.ReadAllText(manualPath) == manualText && Stamp(manualPath) == manualStamp && !File.Exists(manualPath + ".tmp"), "Campaign navigation touched the separate manual application slot.");
    }
    private void RequireFreshIsolatedData()
    {
        var configured = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Require(!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured), "Explicit fresh /tmp/empire-* XDG_DATA_HOME is required before any game or save is opened.");
        var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(configured!).TrimEnd(Path.DirectorySeparatorChar);
        var relative = Path.GetRelativePath(temporary, root);
        Require(!Path.IsPathFullyQualified(relative) && relative.Split(Path.DirectorySeparatorChar)[0].StartsWith("empire-", StringComparison.Ordinal), "Refusing non-generated user data outside /tmp/empire-*.");
        userData = Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).TrimEnd(Path.DirectorySeparatorChar);
        Require(userData.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Godot ignored isolated user data.");
        for (var directory = new System.IO.DirectoryInfo(userData); directory is not null && directory.FullName != temporary; directory = directory.Parent)
            Require(!directory.Exists || (directory.Attributes & System.IO.FileAttributes.ReparsePoint) == 0, "Refusing symlinked user-data ancestors.");
        // Godot creates logs and, with a real renderer, shader caches before _Ready.
        Require(!Directory.Exists(userData) || Directory.EnumerateFileSystemEntries(userData).All(entry =>
            (Path.GetFileName(entry) is "logs" or "shader_cache") && Directory.Exists(entry)
            && (File.GetAttributes(entry) & FileAttributes.ReparsePoint) == 0),
            "Refusing pre-existing user data; use a fresh generated directory.");
        automaticPath = SavePath("CampaignSavePath");
        manualPath = SavePath("SavePath");
        Require(!File.Exists(automaticPath) && !Directory.Exists(automaticPath) && !File.Exists(manualPath) && !File.Exists(manualPath + ".tmp"), "Fixture save paths are not fresh.");
        Directory.CreateDirectory(userData);
    }
    private string SavePath(string field)
    {
        var resource = (string)typeof(GrayFlagGame).GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        var path = Path.GetFullPath(ProjectSettings.GlobalizePath(resource));
        Require(Path.GetDirectoryName(path) == userData, "Save path escaped the isolated data directory.");
        return path;
    }
    private void ResetAutosave()
    {
        if (File.Exists(automaticPath))
            File.Delete(automaticPath);
        Require(!Directory.Exists(automaticPath), "A previous fixture obstruction is still present.");
    }
    private void WriteFixture(JsonObject state)
    {
        ResetAutosave();
        CampaignSlotStorage.Save(definition, state, automaticPath);
    }
    private JsonObject ReadSave() => EcmaJson.Parse(File.ReadAllText(automaticPath))!.AsObject();
    private GrayFlagGame MountGame()
    {
        var game = new GrayFlagGame { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(game);
        return game;
    }
    private static GrayFlagCampaign Campaign(GrayFlagGame game) => Field<GrayFlagCampaign>(game, "campaign");
    private static T View<T>(GrayFlagGame game) where T : Control => Field<Control>(game, "view") as T ?? throw new InvalidOperationException($"Expected {typeof(T).Name}, got {Field<Control>(game, "view").GetType().Name}.");
    private static void SetField(object instance, string name, object? value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static string State(GrayFlagGame game) => Digest(Campaign(game).Snapshot());
    private static string Digest(JsonObject state) => StateDigest.Hash(state);
    private static string Stamp(string path)
    {
        var info = new System.IO.FileInfo(path);
        return info.Exists ? $"{info.Length}:{info.LastWriteTimeUtc.Ticks}" : "<absent>";
    }
    private static Button Button(Node root, string name) => Buttons(root).SingleOrDefault(button => button.Name == name) ?? throw new InvalidOperationException($"Missing actual navigation button {name} on {root.GetType().Name}.");
    private static IEnumerable<Button> Buttons(Node root) => Descendants(root).OfType<Button>();
    private static IEnumerable<string> Labels(Node root) => Descendants(root).OfType<Label>().Select(label => label.Text);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
    private static void Press(Button button) => button.EmitSignal(Godot.Button.SignalName.Pressed);
    private static void Retire(Node node)
    {
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
