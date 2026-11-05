#nullable enable
using Empire.Application;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckNativeCampaignRoute()
    {
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1280, 800);
        Require(Input.EmulateMouseFromTouch && !Input.EmulateTouchFromMouse,
            "Campaign route regression uses the shipped native touch-to-mouse policy.");
        try
        {
            foreach (var mode in new[] { "quick-touch", "split-touch", "mouse", "keyboard" })
            {
                ResetAutosave();
                var game = MountGame();
                game.ProcessMode = ProcessModeEnum.Inherit;
                try
                {
                    async Task Settle()
                    {
                        for (var frame = 0; frame < 8; frame++)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    async Task KeyPress(Key code, bool reverse = false)
                    {
                        foreach (var pressed in new[] { true, false })
                        {
                            using var input = new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = pressed, ShiftPressed = reverse };
                            GetViewport().PushInput(input, true);
                        }
                        await Settle();
                    }
                    async Task Activate(Button button)
                    {
                        Require(button.IsInsideTree() && button.IsVisibleInTree() && !button.Disabled,
                            mode + " targets an enabled visible native campaign button.");
                        if (mode == "keyboard")
                        {
                            Require(GetViewport().GuiGetFocusOwner() is not null,
                                "The actual campaign view must admit keyboard focus without test-injected GrabFocus.");
                            for (var step = 0; step < 64 && !button.HasFocus(); step++)
                                await KeyPress(Key.Tab);
                            Require(button.HasFocus(), "Natural Tab traversal must reach " + button.Name);
                            Require(GetViewport().GetVisibleRect().HasPoint(button.GetGlobalRect().GetCenter()),
                                "Native focus must reveal the keyboard action inside the viewport.");
                            await KeyPress(Key.Enter);
                            return;
                        }
                        var at = button.GetGlobalRect().GetCenter();
                        Require(GetViewport().GetVisibleRect().HasPoint(at),
                            mode + " campaign action is reachable inside the actual viewport.");
                        foreach (var pressed in new[] { true, false })
                        {
                            using InputEvent input = mode == "mouse"
                                ? new InputEventMouseButton
                                {
                                    ButtonIndex = MouseButton.Left,
                                    Pressed = pressed,
                                    Position = at,
                                    GlobalPosition = at
                                }
                                : new InputEventScreenTouch { Index = 0, Position = at, Pressed = pressed };
                            Input.ParseInputEvent(input);
                            if (mode == "split-touch")
                                await Settle();
                        }
                        await Settle();
                    }
                    await Settle();
                    var initial = EcmaJson.Stringify(Campaign(game).Snapshot());
                    await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
                    Require(View<StoryScreen>(game).BeatIndex == 0 && EcmaJson.Stringify(Campaign(game).Snapshot()) == initial,
                        mode + " New Campaign opens the first original beat without advancing canonical progress.");
                    await Activate(Button(View<StoryScreen>(game), "CampaignExit"));
                    Require(View<CampaignMenuScreen>(game).Presented?.CanContinue == false,
                        mode + " leaving the initial uncommitted story does not invent an autosave.");
                    await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
                    Require(View<StoryScreen>(game).BeatIndex == 0, mode + " a fresh New Campaign restarts at beat zero.");
                    var beats = 0;
                    while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                    {
                        var story = View<StoryScreen>(game);
                        var before = EcmaJson.Stringify(Campaign(game).Snapshot());
                        var expected = new CampaignRuntime(CampaignRules.Create(), definition, Campaign(game).Snapshot());
                        var beat = story.BeatIndex;
                        var last = story.PresentedStory!.LastBeat;
                        if (last)
                            expected.Advance();
                        await Activate(Button(story, "StoryPrimary"));
                        if (last)
                            Require(EcmaJson.Stringify(Campaign(game).Snapshot()) == EcmaJson.Stringify(expected.Snapshot())
                                && !ReferenceEquals(View<Control>(game), story),
                                mode + " final beat performs exactly the canonical campaign advance.");
                        else
                            Require(ReferenceEquals(View<Control>(game), story) && story.BeatIndex == beat + 1
                                && EcmaJson.Stringify(Campaign(game).Snapshot()) == before,
                                mode + " each native press reveals exactly one local beat without committing progress.");
                        Require(++beats <= 40, "The unchanged opening story must reach its authored choice.");
                    }
                    var choices = View<StoryScreen>(game);
                    if (mode == "keyboard")
                    {
                        var options = choices.PresentedChoice!.Options;
                        var first = Button(choices, "Choice_" + options[0].Id);
                        Require(options.Count > 1 && first.HasFocus(),
                            "Normal story completion must focus the first available choice without committing it.");
                        var choiceState = State(game);
                        var choiceSlot = System.IO.File.ReadAllText(automaticPath);
                        var choiceStamp = Stamp(automaticPath);
                        await KeyPress(Key.Tab);
                        Require(Button(choices, "Choice_" + options[1].Id).HasFocus(),
                            "Native Tab reaches the other authored choice.");
                        await KeyPress(Key.Tab, true);
                        Require(first.HasFocus() && State(game) == choiceState
                            && System.IO.File.ReadAllText(automaticPath) == choiceSlot && Stamp(automaticPath) == choiceStamp,
                            "Forward and reverse choice focus cannot select or save an option.");
                        await KeyPress(Key.Escape);
                        Require(View<CampaignMenuScreen>(game).Presented?.CanContinue == true,
                            "Keyboard exit preserves the unchosen campaign checkpoint.");
                        await Activate(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
                        choices = View<StoryScreen>(game);
                        Require(State(game) == choiceState && Button(choices, "Choice_" + options[0].Id).HasFocus(),
                            "Natural Continue restores the same unchosen state and usable choice focus.");
                    }
                    var choiceId = mode == "keyboard" ? choices.PresentedChoice!.Options.Last().Id : "rush-north-hill";
                    var expectedChoice = new CampaignRuntime(CampaignRules.Create(), definition, Campaign(game).Snapshot());
                    expectedChoice.Choose(choiceId);
                    await Activate(Button(choices, "Choice_" + choiceId));
                    Require(View<CampaignStagingScreen>(game).Presented is not null
                        && EcmaJson.Stringify(Campaign(game).Snapshot()) == EcmaJson.Stringify(expectedChoice.Snapshot()),
                        mode + " route choice commits the exact canonical consequences once.");
                    AssertSaved(game);
                    var stagingState = EcmaJson.Stringify(Campaign(game).Snapshot());
                    await Activate(Button(View<CampaignStagingScreen>(game), "CampaignExit"));
                    Require(View<CampaignMenuScreen>(game).Presented?.CanContinue == true,
                        mode + " leaving staging exposes the original Continue route.");
                    await Activate(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
                    Require(View<CampaignStagingScreen>(game).Presented is not null
                        && EcmaJson.Stringify(Campaign(game).Snapshot()) == stagingState,
                        mode + " Continue restores the exact staged campaign progress.");
                    await Activate(Button(View<CampaignStagingScreen>(game), "StartBattle"));
                    var session = Campaign(game).Battle!;
                    Require(View<BattleScreen>(game).IsInsideTree() && !Campaign(game).AtCamp
                        && session.Snapshot()["levelId"]!.GetValue<string>() == "c01-01"
                        && session.Revision == 0 && session.ActionLog.Count == 0,
                        mode + " Start Battle enters the original first battle once without dispatching an order.");
                    GD.Print($"NATIVE_CAMPAIGN_ROUTE_OK mode={mode}; beats={beats}; initial battle={StateDigest.Hash(session.Snapshot())}; no battle actions");
                }
                finally
                {
                    Retire(game);
                    await Layout();
                }
            }
        }
        finally
        {
            // Native input marks the pointer inside the shared viewport; retire that test-owned state too.
            GetViewport().NotifyMouseExited();
            GetWindow().Size = originalSize;
        }
    }
}
