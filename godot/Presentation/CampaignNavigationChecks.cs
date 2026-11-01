#nullable enable
using Empire.Presentation.Tactical;
using System.Reflection;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Engine;
using Empire.Core.Geometry;
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
            foreach (var (name, test) in new (string, Func<Task>)[] {
                ("fresh menu, local beats, choice and autosave", () => { CheckFreshStoryChoice(); return Task.CompletedTask; }),
                ("battle window minimum follows committed view and lifecycle", CheckBattleWindowMinimum),
                ("root notice leaves with its view", CheckNoticeViewTransition),
                ("startup resume, battle restart, camp and Continue", CheckBattleCampMenu),
                ("story/choice return, rebind and unmount", () => { CheckNarrativeNavigation(); return Task.CompletedTask; }),
                ("overwrite cancellation, stale dialog and confirmation", () => { CheckOverwrite(); return Task.CompletedTask; }),
                ("overwrite dialog unmount", () => { CheckUnmountedOverwrite(); return Task.CompletedTask; }),
                ("invalid save notice and explicit replacement", () => { CheckInvalidSave(); return Task.CompletedTask; }),
                ("ending active/completed/failed UI fixtures", () => { CheckEndingFixtures(); return Task.CompletedTask; }),
                ("story, choice, ending and staging write failures", () => { CheckWriteFailures(); return Task.CompletedTask; }),
                ("victory settlement UI fixture and autosave", () => { CheckVictorySettlement(); return Task.CompletedTask; }),
                ("read/delete failure preserves menu", () => { CheckReadDeleteFailure(); return Task.CompletedTask; }),
                ("manual load admission and view activation are atomic", () => { CheckManualLoadAdmission(); return Task.CompletedTask; }),
                ("formal Redstone entry, carried-roster fixture, resume and settlement", CheckFormalRedstone),
            })
            {
                try
                {
                    await test();
                    if (paintedCaptureGame is not null)
                    {
                        // Navigation fixtures suppress processing; this live interaction must run real animation frames.
                        paintedCaptureGame.ProcessMode = ProcessModeEnum.Inherit;
                        var screen = View<BattleScreen>(paintedCaptureGame);
                        var civilian = Campaign(paintedCaptureGame).Battle!.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                            .Single(unit => unit["key"]!.GetValue<string>() == "refugee-2");
                        screen.SelectHit(TacticalHit.Unit(new(13, 12), civilian["id"]!.GetValue<int>()));
                        Require(screen.SelectedUnitId == civilian["id"]!.GetValue<int>(), "The formal escort actor could not be selected on its open-road starting cell.");
                        screen.SelectHit(TacticalHit.Ground(new(13, 13)));
                        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.W, Pressed = true });
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

    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

    private async Task CheckBattleWindowMinimum()
    {
        var window = GetWindow();
        var originalMinimum = window.MinSize;
        var originalSize = window.Size;
        GrayFlagGame? game = null;
        try
        {
            foreach (var inherited in new[] { Vector2I.Zero, new Vector2I(1000, 500) })
            {
                window.MinSize = inherited;
                window.Size = new(640, 480);
                WriteFixture(battleNodeState);
                game = MountGame();
                Require(window.MinSize == inherited, "Menu changed its host window minimum.");
                Press(Button(View<Control>(game), "ContinueCampaign"));
                Require(window.MinSize == inherited, "Staging imposed battle-only constraints.");
                var beforeSize = window.Size;
                var children = game.GetChildCount();
                var prepare = typeof(GrayFlagGame).GetMethod("PrepareMounted", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(typeof(BattleScreen));
                var propagated = false;
                var unconfigured = new BattleScreen();
                // This deliberate pre-BindSession fault has not transferred these eager nodes
                // into the screen tree; the fixture owns their cleanup.
                var unownedBoard = unconfigured.Board;
                var unownedOverlay = Field<BoardOverlay>(unconfigured, "_overlay");
                try
                {
                    prepare.Invoke(game, new object[] { unconfigured, (Action<BattleScreen>)(_ => throw new InvalidOperationException("intentional preparation fixture")) });
                }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { propagated = true; }
                finally
                {
                    unownedBoard.Free();
                    unownedOverlay.Free();
                }
                Require(propagated && game.GetChildCount() == children && window.MinSize == inherited && window.Size == beforeSize,
                    "Failed hidden battle preparation altered the current window or leaked a view.");
                Press(Button(View<Control>(game), "StartBattle"));
                await Layout();
                var expected = new Vector2I(Math.Max(inherited.X, 960), Math.Max(inherited.Y, 640));
                Require(window.MinSize == expected && window.Size.X >= expected.X && window.Size.Y >= expected.Y,
                    "Committed battle did not preserve the larger host minimum and grow to a readable viewport.");
                var state = State(game);
                var battle = Digest(Campaign(game).Battle!.Snapshot());
                window.Size = new(640, 360);
                await Layout();
                Require(window.Size.X >= expected.X && window.Size.Y >= expected.Y && State(game) == state && Digest(Campaign(game).Battle!.Snapshot()) == battle,
                    "Battle resize escaped the minimum or changed gameplay.");
                Press(Buttons(View<BattleScreen>(game)).Single(button => button.Text == "战役营地"));
                await Layout();
                Require(window.MinSize == inherited, "Leaving battle for camp retained the battle minimum.");
                Press(Button(View<Control>(game), "StartBattle"));
                RemoveChild(game);
                Require(window.MinSize == inherited, "Unmount retained the battle minimum.");
                AddChild(game);
                Require(window.MinSize == expected, "Remount failed to restore the active battle minimum.");
                game.Free();
                game = null;
                Require(window.MinSize == inherited, "Free retained the battle minimum.");
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(game))
                game!.Free();
            window.MinSize = originalMinimum;
            window.Size = originalSize;
        }
    }

    private async Task CheckBattleCampMenu()
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
        await Layout();
        var dialog = Descendants(battleScreen).OfType<ConfirmationDialog>().Single();
        Require(dialog.Visible, "Restart did not request confirmation.");
        dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(ReferenceEquals(campaign.Battle, battle) && ReferenceEquals(View<Control>(game), battleScreen), "Cancelled restart later executed from a stale confirmation.");
        Press(restart);
        await Layout();
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        var restarted = campaign.Battle;
        var nextScreen = View<BattleScreen>(game);
        Require(!ReferenceEquals(restarted, battle) && !ReferenceEquals(nextScreen, battleScreen), "Confirmed restart failed to replace the battle session.");
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Press(restart);
        Require(ReferenceEquals(campaign.Battle, restarted), "Stale restart dialog/button replaced the new session.");
        Press(Buttons(nextScreen).Single(button => button.Text == "重新开始"));
        await Layout();
        var staleDialog = Descendants(nextScreen).OfType<ConfirmationDialog>().Single();
        Press(Buttons(nextScreen).Single(button => button.Text == "战役营地"));
        await Layout();
        var camp = View<CampaignStagingScreen>(game);
        var pending = State(game);
        var opening = campaign.Battle;
        Require(campaign.AtCamp && campaign.HasPendingBattle && opening is not null, "Battle exit failed to retain the request at camp.");
        if (GodotObject.IsInstanceValid(staleDialog))
        {
            Require(!staleDialog.IsInsideTree(), "Retired restart confirmation must no longer receive native input.");
            staleDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        }
        else
            Require(!GodotObject.IsInstanceValid(nextScreen), "The old confirmation is released with its retired battle screen.");
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
        Require(dialog.GetCancelButton().HasFocus() && !dialog.GetOkButton().HasFocus(), "Campaign replacement initially focuses Cancel, never the destructive action.");
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
                var notice = Descendants(game).OfType<AcceptDialog>().Single(dialog => dialog.DialogText.Contains("自动保存失败", StringComparison.Ordinal));
                Require(notice.Theme is not null && notice.GetOkButton().Text == GodotTextCatalog.LoadSimplifiedChinese().Format("ui.common.close") && notice.DialogAutowrap, "Non-battle storage failure uses the shared localized, wrapping notice contract.");
                notice.EmitSignal(AcceptDialog.SignalName.Canceled);
                Require(notice.IsQueuedForDeletion(), "Escape/cancel retires a root-owned notice instead of accumulating hidden windows.");
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
            Require(GetWindow().MinSize == Field<Vector2I>(game, "inheritedWindowMinimum"), "Settlement retained the battle-only window minimum.");
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
        CheckLayeredBridgeBoard(View<BattleScreen>(game).Board);
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
            CheckLayeredBridgeBoard(View<BattleScreen>(game).Board);
        }
        finally
        {
            File.WriteAllText(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalStamp);
        }
    }

    private void CheckLayeredBridgeBoard(BoardView board)
    {
        var ground = Descendants(board).OfType<AssembledGroundView>().Single();
        Require(!Descendants(board).OfType<TerrainView>().Any() && board.RenderedUnitCount == 20 && board.RenderingIssueCount == 0,
            "Formal bridge rendering must use its independently layered ground and all twenty actors without rendering errors.");
        var houses = ground.Sites;
        Require(houses.Count == 4 && houses.All(entry => entry.OwnsMapStructureBody && ground.OwnsMapStructureBody(entry.Cell)),
            "The four independent houses must own their selectable map-structure bodies.");
        Require(!Descendants(board).Any(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)),
            "Formal bridge houses were duplicated by dynamic map-structure sprites.");
        var document = PresentationJson.Load<AssembledGroundDocument>("res://data/scenes/c01-02-layered-ground.json");
        using (var pixels = ResourceLoader.Load<Texture2D>(document.ResourcePath).GetImage())
        {
            // Narrow material regression for manually reviewed dry foot contacts, not a universal terrain classifier.
            foreach (var cell in new[] { new Vector2I(17, 4), new(16, 5), new(17, 5), new(16, 6),
                new(19, 23), new(20, 23), new(20, 22), new(21, 23), new(12, 29), new(13, 28),
                new(13, 29), new(11, 30), new(12, 30), new(11, 31), new(27, 13) })
            {
                var source = new Vector2(document.OriginPixels[0], document.OriginPixels[1])
                    + new Vector2(document.CellXPixels[0], document.CellXPixels[1]) * (cell.X + .5f)
                    + new Vector2(document.CellYPixels[0], document.CellYPixels[1]) * (cell.Y + .5f);
                foreach (var dx in new[] { -6, 0, 6 })
                    foreach (var dy in new[] { -6, 0, 6 })
                    {
                        var color = pixels.GetPixel((int)source.X + dx, (int)source.Y + dy);
                        Require(!(color.B > color.R + .12f && color.G > color.R + .08f && color.B > color.G + .03f),
                            $"C02 reviewed dry foot contact regressed to the source painting's blue water: {cell}, offset {dx},{dy}.");
                    }
            }
        }
        var catalog = PresentationJson.Load<SceneObjectCatalog>(document.ObjectCatalogPath);
        foreach (var placement in document.Objects)
        {
            var sprite = ground.GetNode<Sprite2D>(placement.Id);
            var asset = catalog.Objects[placement.AssetId];
            var visual = ground.DepthEntries.Single(entry => entry.Node == sprite).Visual!;
            Vector2 At(Vector2 source) => sprite.ToGlobal(source - new Vector2((float)asset.AnchorPixels[0], (float)asset.AnchorPixels[1]));
            using var pixels = sprite.Texture.GetImage();
            Require(pixels.HasMipmaps(), $"C02 independent minified material lacks mipmaps: {placement.Id}.");
            Require(!visual.Covers(At(new Vector2(10, 10))), $"C02 transparent padding became opaque: {placement.Id}.");
            if (placement.SelectionCell is { } cell)
            {
                var door = placement.AssetId switch
                {
                    "southwest-house" => new Vector2(920, 800),
                    "east-village" => new Vector2(600, 700),
                    _ => new Vector2(620, 860)
                };
                var roof = new Vector2(800, placement.AssetId == "east-village" ? 350 : 400);
                Require(visual.Covers(At(door)) && visual.ContainsPointer(At(door)), $"C02 actual door pixels lost the facade target: {placement.Id}.");
                var hit = board.Pick(board.ToLocal(At(door)));
                Require(hit is { Kind: TacticalHitKind.Object } && hit.Cell.X == cell[0] && hit.Cell.Y == cell[1],
                    $"C02 actual door picked another object or neighboring actor: {placement.Id} ({hit}).");
                Require(visual.Covers(At(roof)) && !visual.ContainsPointer(At(roof)),
                    $"C02 opaque roof must draw without redirecting clicks to the facade: {placement.Id}.");
            }
            else if (placement.AssetId is "wood-rail" or "stone-rail")
            {
                var opaque = placement.AssetId == "wood-rail" ? new Vector2(445, 400) : new Vector2(550, 500);
                Require(visual.Covers(At(opaque)) && !visual.ContainsPointer(At(opaque)),
                    $"C02 bridge fronts must occlude by depth but never steal ground input: {placement.Id}.");
                if (placement.AssetId == "wood-rail")
                    Require(!visual.Covers(At(new Vector2(750, 540))), $"C02 wooden rail opening must remain real transparent space: {placement.Id}.");
            }
        }
        var banners = Descendants(board).Where(node => node.HasMeta("owner_color")).ToArray();
        Require(banners.Length == 2 && banners.Select(node => node.GetMeta("owner_color").AsString()).Distinct(StringComparer.Ordinal).Count() == 2,
            "The two owned outposts must retain exactly two dynamic ownership flags.");
    }

    private void CheckLayeredRedstoneBoard(BoardView board)
    {
        var ground = Descendants(board).OfType<AssembledGroundView>().Single();
        Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == 18
            && !Descendants(board).OfType<TerrainView>().Any(), "Formal Redstone renders its carried roster on independent layered ground.");
        Require(ground.Sites.Count == 8
            && !Descendants(board).Any(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)),
            "Redstone's eight independent site bodies must not acquire duplicate sprites.");
        var document = PresentationJson.Load<AssembledGroundDocument>("res://data/scenes/c01-03-layered-ground.json");
        var catalog = PresentationJson.Load<SceneObjectCatalog>(document.ObjectCatalogPath);
        foreach (var placement in document.Objects)
        {
            var sprite = ground.GetNode<Sprite2D>(placement.Id);
            var asset = catalog.Objects[placement.AssetId];
            var visual = ground.DepthEntries.Single(entry => entry.Node == sprite).Visual!;
            Vector2 At(Vector2 source) => sprite.ToGlobal(source - new Vector2((float)asset.AnchorPixels[0], (float)asset.AnchorPixels[1]));
            using var pixels = sprite.Texture.GetImage();
            Require(pixels.HasMipmaps(), $"C03 independent minified material lacks mipmaps: {placement.Id}.");
            Require(!visual.Covers(At(new Vector2(10, 10))), $"C03 transparent padding became opaque: {placement.Id}.");
            if (placement.SelectionCell is { } cell)
            {
                var door = placement.AssetId switch
                {
                    "grounded-cabin" => new Vector2(930, 720),
                    "shed" => new Vector2(775, 760),
                    "southwest-house" => new Vector2(920, 800),
                    _ => new Vector2(620, 860)
                };
                var roof = new Vector2(800, 400);
                Require(visual.Covers(At(door)) && visual.ContainsPointer(At(door)), $"C03 actual door pixels lost facade input: {placement.Id}.");
                var hit = board.Pick(board.ToLocal(At(door)));
                // The headquarters starts occupied: existing actor precedence must remain intact at that same doorway cell.
                Require(hit is { } && (hit.Kind == TacticalHitKind.Object
                    || placement.Id == "Headquarters" && hit.Kind == TacticalHitKind.Unit)
                    && hit.Cell.X == cell[0] && hit.Cell.Y == cell[1],
                    $"C03 doorway selected a neighboring tile or object instead of its site/occupant: {placement.Id} ({hit}).");
                Require(visual.Covers(At(roof)) && !visual.ContainsPointer(At(roof)),
                    $"C03 opaque roof must draw without redirecting ground clicks: {placement.Id}.");
            }
        }
        var foundationReview = PresentationJson.Load<JsonObject>("res://data/scenes/c01-03-foundation-review.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var rejectedPlacementExposesCells = 0;
        foreach (var site in foundationReview["sites"]!.AsArray().OfType<JsonObject>())
        {
            var id = site["id"]!.GetValue<string>();
            var sprite = ground.GetNode<Sprite2D>(id);
            var visual = ground.DepthEntries.Single(entry => entry.Node == sprite).Visual!;
            var previous = site["negativeControlPlacement"]!.AsObject();
            var previousAsset = catalog.Objects[previous["assetId"]!.GetValue<string>()];
            using var previousPixels = ResourceLoader.Load<Texture2D>(previousAsset.ResourcePath).GetImage();
            var previousSourceContact = new Vector2(13 + 16 * previous["contact"]![0]!.GetValue<float>(),
                17 + 18 * previous["contact"]![1]!.GetValue<float>());
            var previousRatio = 1.5f / previous["scale"]!.GetValue<float>();
            foreach (var cell in site["foundationCells"]!.AsArray().OfType<JsonArray>())
            {
                var x = cell[0]!.GetValue<int>();
                var y = cell[1]!.GetValue<int>();
                Require(level["terrain"]![y]!.GetValue<string>()[x] == '#', $"C03 reviewed foundation changed its canonical terrain: {id} ({x},{y}).");
                var overlapsMaterial = false;
                var previousOverlapsMaterial = false;
                // A boundary cell need not have an opaque center: actual material must
                // overlap its reviewed footprint, not hide the entire tile with a roof.
                foreach (var dx in new[] { -6, 0, 6 })
                    foreach (var dy in new[] { -7, 0, 7 })
                    {
                        var source = new Vector2(21 + 16 * x + dx, 26 + 18 * y + dy);
                        overlapsMaterial |= visual.Covers(board.ToGlobal(source * 1.5f));
                        var oldPixel = (source - previousSourceContact) * previousRatio
                            + new Vector2((float)previousAsset.AnchorPixels[0], (float)previousAsset.AnchorPixels[1]);
                        var px = Mathf.RoundToInt(oldPixel.X);
                        var py = Mathf.RoundToInt(oldPixel.Y);
                        previousOverlapsMaterial |= px >= 0 && py >= 0 && px < previousPixels.GetWidth()
                            && py < previousPixels.GetHeight() && previousPixels.GetPixel(px, py).A > .1f;
                    }
                if (!previousOverlapsMaterial)
                    rejectedPlacementExposesCells++;
                Require(overlapsMaterial,
                    $"C03 physical building leaves reviewed impassable foundation as bare ground: {id} ({x},{y}).");
            }
        }
        Require(rejectedPlacementExposesCells > 0, "C03 foundation guard must reject the previous doorway-snapped assembly, not silently bless its exposed walls.");
        var defenseReview = PresentationJson.Load<JsonObject>("res://data/scenes/c01-03-city-defense-review.json");
        var walls = document.Objects.Where(value => value.AssetId.StartsWith("wall-", StringComparison.Ordinal))
            .Select(value => ground.DepthEntries.Single(entry => entry.Node.Name.ToString() == value.Id).Visual!).ToArray();
        foreach (var group in defenseReview["groups"]!.AsArray().OfType<JsonObject>())
            foreach (var cell in group["cells"]!.AsArray().OfType<JsonArray>())
            {
                var x = cell[0]!.GetValue<int>();
                var y = cell[1]!.GetValue<int>();
                Require(level["terrain"]![y]!.GetValue<string>()[x] == '#', "C03 wall-support review must not invent blocked terrain.");
                var supported = false;
                foreach (var dx in new[] { -6, 0, 6 })
                    foreach (var dy in new[] { -7, 0, 7 })
                        supported |= walls.Any(visual => visual.Covers(board.ToGlobal(new Vector2(21 + 16 * x + dx, 26 + 18 * y + dy) * 1.5f)));
                Require(supported, $"C03 city defense leaves reviewed wall support as bare ground: {group["id"]} ({x},{y}).");
            }
        var rejectedWing = defenseReview["negativeControlGarrisonWing"]!.AsObject();
        var wingAsset = catalog.Objects[rejectedWing["assetId"]!.GetValue<string>()];
        using (var pixels = ResourceLoader.Load<Texture2D>(wingAsset.ResourcePath).GetImage())
        {
            var oldContact = new Vector2(13 + 16 * rejectedWing["contact"]![0]!.GetValue<float>(),
                17 + 18 * rejectedWing["contact"]![1]!.GetValue<float>());
            var oldCoversBase = false;
            foreach (var dx in new[] { -6, 0, 6 })
                foreach (var dy in new[] { -7, 0, 7 })
                {
                    var source = new Vector2(21 + 16 * 82 + dx, 26 + 18 * 18 + dy);
                    var pixel = (source - oldContact) * (1.5f / rejectedWing["scale"]!.GetValue<float>())
                        + new Vector2((float)wingAsset.AnchorPixels[0], (float)wingAsset.AnchorPixels[1]);
                    var px = Mathf.RoundToInt(pixel.X);
                    var py = Mathf.RoundToInt(pixel.Y);
                    oldCoversBase |= px >= 0 && py >= 0 && px < pixels.GetWidth() && py < pixels.GetHeight() && pixels.GetPixel(px, py).A > .1f;
                }
            Require(!oldCoversBase, "C03 defense regression control must expose the original 25px-high garrison wing registration.");
        }
        foreach (var cell in new[] { new Coord(65, 12), new(66, 12), new(65, 23), new(66, 23), new(65, 24), new(66, 24),
            new(75, 34), new(76, 34), new(85, 27), new(86, 27), new(87, 29), new(87, 30) })
        {
            var x = cell.X;
            var y = cell.Y;
            Require(level["terrain"]![y]!.GetValue<string>()[x] != '#', "C03 protected access cell must remain canonically passable.");
            var point = board.Projection.CellContact(cell);
            foreach (var wall in document.Objects.Where(value => value.AssetId.StartsWith("wall-", StringComparison.Ordinal)))
            {
                var sprite = ground.GetNode<Sprite2D>(wall.Id);
                var visual = ground.DepthEntries.Single(entry => entry.Node == sprite).Visual!;
                Require(!visual.Covers(board.ToGlobal(point)), $"C03 real wall pixels cover an open doorway cell ({x},{y}): {wall.Id}.");
            }
            var hit = board.Pick(point);
            Require(hit is not null && hit.Cell.X == x && hit.Cell.Y == y,
                $"C03 protected gate/service road cannot be picked at its physical contact ({x},{y}): {hit}.");
        }
    }

    private async Task CheckFormalRedstone()
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
            void Layered() => CheckLayeredRedstoneBoard(View<BattleScreen>(game).Board);
            Layered();
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
            Layered();
            Press(Buttons(View<BattleScreen>(game)).Single(button => button.Text == "战役营地"));
            await Layout();
            Require(Campaign(game).AtCamp, "Redstone exit did not return to the normal camp.");
            Press(Button(View<CampaignStagingScreen>(game), "CampaignExit"));
            Press(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            Require(Digest(Campaign(game).Battle!.Snapshot()) == Digest(opening), "Redstone Continue did not rebuild the same carried opening.");
            Layered();

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
            var windowMinimum = GetWindow().MinSize;
            var windowSize = GetWindow().Size;
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
            Require(GetWindow().MinSize == windowMinimum && GetWindow().Size == windowSize, "Failed manual-load preparation altered the active battle window.");
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
    private async Task CheckNoticeViewTransition()
    {
        foreach (var transition in new[] { "SetView", "ShowNode" })
        {
            var game = MountGame();
            typeof(GrayFlagGame).GetMethod("Notify", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, ["UI fixture notice"]);
            var notice = game.GetChildren().OfType<AcceptDialog>().Single();
            var shade = game.GetChildren().OfType<CanvasLayer>().Single();
            Require(notice.Visible && shade.Visible, "Root notice and its shade must be open before changing views.");
            object?[] args = transition == "SetView" ? [new Control()] : [null];
            typeof(GrayFlagGame).GetMethod(transition, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, args);
            Require(!notice.Visible && !notice.IsInsideTree() && !shade.Visible,
                "Both direct and prepared view transitions immediately dismiss outgoing root notices.");
            for (var i = 0; i < 4; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!game.GetChildren().OfType<CanvasLayer>().Any(), "Deferred view cleanup leaves no modal shade behind.");
            game.Free();
        }
    }

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
