using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire.Localization;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using System.Text.Json.Nodes;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Native campaign input ownership and bounded-window accessibility; no campaign commands or saves.</summary>
public partial class CampaignUiFlowChecks : Node
{
    private int assertions;
    private readonly List<string> failures = new();
    private readonly GameText text = GodotTextCatalog.LoadSimplifiedChinese();
    private static readonly NarrativeArt Art = new() { Source = "fixture", ResourcePath = "res://assets/native/hud/dispatch-board-v1.png" };
    private static CampaignShellView ShellView => new("灰旗战役", "第七章 · 山谷", 6, 12, Art, "返回营地", "进度", "美术缺失");
    public override async void _Ready()
    {
        try
        {
            await CheckEscape();
            await CheckScreens();
            await CheckSystemPopup();
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            GD.Print($"CAMPAIGN_UI_FLOW_CHECKS_OK {assertions} assertions");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) failures.Add(message);
    }
    private async Task Layout()
    {
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var node in Descendants(child)) yield return node;
        }
    }
    private static Button Button(Node root, string name) => Descendants(root).OfType<Button>().Single(button => button.Name == name);
    private void Escape(bool echo = false, bool pressed = true) => GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = pressed, Echo = echo });
    private async Task CheckEscape()
    {
        var shell = new CampaignShell();
        AddChild(shell);
        var exits = 0;
        shell.Show(ShellView, () => exits++);
        await Layout();
        Escape(echo: true); Escape(pressed: false);
        Check(exits == 0, "Esc releases and autorepeat cannot navigate.");
        shell.Hide(); Escape();
        Check(exits == 0, "Hidden campaign shell cannot receive Esc.");
        shell.Show(); Escape(); Escape();
        Check(exits == 1, "Current visible campaign shell exits exactly once.");
        shell.Show(ShellView, () => exits += 10);
        Escape(); Escape();
        Check(exits == 11, "Rebinding routes Esc only to current exit callback.");
        shell.Show(ShellView, () => exits += 100);
        shell.ProcessMode = ProcessModeEnum.Disabled; Escape();
        Check(exits == 11, "Prepared but inactive shell cannot navigate.");
        RemoveChild(shell); shell.QueueFree();
        await Layout();
    }
    private async Task CheckSystemPopup()
    {
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        void BindSession() => screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
            level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        BindSession();
        screen.BindApplicationActions(() => { }, () => { }, () => { }, () => { });
        await Layout();
        screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Space });
        var selection = screen.Selection;
        var revision = session.Revision;
        var popup = Descendants(screen).OfType<PopupPanel>().Single(value => value.Name == "SystemMenu");
        var close = Descendants(popup).OfType<Button>().Single(value => value.Text.EndsWith("  Esc", StringComparison.Ordinal));
        foreach (var size in new[] { new Vector2I(640, 360), new Vector2I(1280, 720) })
        {
            GetWindow().Size = size; await Layout();
            Button(screen, "SystemMenuButton").EmitSignal(Godot.Button.SignalName.Pressed);
            await Layout();
            Check(popup.Visible && popup.Size.X <= size.X && popup.Size.Y <= size.Y,
                $"System popup {size}: contents must fit available window ({popup.Size}).");
            Check(Descendants(popup).OfType<Button>().All(button =>
                new Rect2(Vector2.Zero, popup.Size).Encloses(button.GetGlobalRect())),
                $"System popup {size}: every action and close control remains inside popup.");
            close.EmitSignal(Godot.Button.SignalName.Pressed);
            Check(!popup.Visible && session.Revision == revision && screen.Selection == selection,
                "Explicit system close dismisses without altering selection or battle state.");
        }
        var restarts = 0;
        screen.BindApplicationActions(onRestart: () => restarts++);
        var confirmation = Descendants(screen).OfType<ConfirmationDialog>().Single();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var restart = Descendants(popup).OfType<Button>().Single(button => button.Text == text.Format("ui.battle.restart"));
            restart.EmitSignal(Godot.Button.SignalName.Pressed);
            restart.EmitSignal(Godot.Button.SignalName.Pressed);
            await Layout();
            Check(confirmation.Visible && confirmation.GetCancelButton().HasFocus() && !confirmation.GetOkButton().HasFocus(),
                "Opening or reopening restart confirmation focuses Cancel, never the destructive action.");
            confirmation.GetCancelButton().EmitSignal(Godot.Button.SignalName.Pressed);
            await Layout();
            Check(!confirmation.Visible && restarts == 0,
                "Cancel dismisses restart confirmation without invoking the restart callback.");
            confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(restarts == 0, "Canceled restart requests cannot be revived by a late confirmation signal.");
        }
        var saves = 0;
        var loads = 0;
        Button Action(string key) => Descendants(popup).OfType<Button>().Single(button => button.Text == text.Format(key));
        void Press(string key) => Action(key).EmitSignal(Godot.Button.SignalName.Pressed);
        screen.BindApplicationActions(() => saves++, () => loads++);
        Press("ui.battle.save"); Press("ui.battle.save"); Press("ui.battle.load");
        Check(saves == 0 && loads == 0, "Popup callbacks must not run inside Pressed dispatch.");
        await Layout();
        Check(saves == 1 && loads == 0, "Only the first queued popup action commits in one dispatch cycle.");
        Press("ui.battle.save"); await Layout();
        Check(saves == 2, "A later save remains available after pending dispatch completes.");
        screen.BindApplicationActions(() => saves++);
        Press("ui.battle.save");
        screen.BindApplicationActions(() => saves += 100);
        await Layout();
        Check(saves == 2, "Rebinding cancels queued callbacks from the previous application binding.");
        Press("ui.battle.save"); RemoveChild(screen); AddChild(screen); await Layout();
        Check(saves == 2, "Unmount and remount cannot revive a queued application action.");
        BindSession();
        screen.BindApplicationActions(onExit: () => RemoveChild(screen));
        popup.PopupCentered(); await Layout();
        Press("ui.battle.camp");
        Check(screen.IsInsideTree(), "Popup Pressed dispatch retains its Window until the signal finishes.");
        await Layout();
        Check(!screen.IsInsideTree(), "Deferred popup exit still invokes its existing navigation callback.");
        if (screen.IsInsideTree()) RemoveChild(screen);
        screen.QueueFree(); await Layout();
    }
    private async Task CheckScreens()
    {
        var portraits = PortraitCatalogResources.Load();
        var unitArt = TacticalHudArt.Load(AssetCatalog.Load(), portraits);
        var documents = StoryDocument.Load(text);
        var context = new NarrativeContext(text, portraits, NarrativePresentationDocument.Load(text, portraits), 6, true);
        var stagingView = new CampaignStagingView(text, "c01-07", "灰旗战役", "第七章 · 山谷", "6/12", 6, 12,
            "战前准备", "灰旗山谷", "查看部队与任务，准备进入战场。", "离开战场后将从本关开局重新开始。", "进入战斗 →",
            new("fixture", Art.ResourcePath), new("fixture", Art.ResourcePath),
            Enumerable.Range(0, 13).Select(i => new CampaignStagingUnit(i.ToString(), "archer", "山谷弓箭手", "生命 30 / 30 · 待命", null, null)), [], []);
        var summary = new CampaignBattleSummary(text, "灰旗山谷", 1, "victory", 13, 13, 0, [], [], 8);
        var menu = new CampaignMenuScreen();
        var staging = new CampaignStagingScreen();
        var result = new BattleResultScreen();
        var story = new StoryScreen();
        var menuView = new CampaignMenuView("灰旗战役", "EMPIRE", "从营地整备部队，继续你的战役。", "", "fixture", Art.ResourcePath,
            true, "继续战役", "6 / 12", "开始新的战役", false, "覆盖？", null, "美术缺失");
        var screens = new (Control Screen, Action Bind, string Primary)[]
        {
            (menu, () => menu.ShowMenu(menuView, () => { }, () => { }), "ContinueCampaign"),
            (staging, () => staging.ShowStaging(stagingView, unitArt, () => { }, () => { }), "StartBattle"),
            (result, () => result.ShowResult("victory", summary, () => { }, ShellView, () => { }), "Proceed"),
            (story, () => story.ShowStory(documents.Stories.First(value => value.Beats.Count > 1), documents.IdentityNameKeys,
                context, false, () => { }, () => { }, () => { }), "StoryPrimary")
        };
        foreach (var (screen, bind, primaryName) in screens)
        {
            AddChild(screen);
            bind(); await Layout();
            Check(Button(screen, primaryName).HasFocus(), $"{screen.GetType().Name}: initial primary focus.");
            bind(); var stale = Button(screen, primaryName); bind(); await Layout();
            Check(Button(screen, primaryName).HasFocus() && !IsInstanceValid(stale), $"{screen.GetType().Name}: rebound primary owns deferred focus.");
            bind(); RemoveChild(screen); await Layout();
            Check(GetViewport().GuiGetFocusOwner() is null, $"{screen.GetType().Name}: detached screen cannot claim deferred focus.");
            AddChild(screen); bind(); RemoveChild(screen); AddChild(screen); await Layout();
            Check(GetViewport().GuiGetFocusOwner() is null, $"{screen.GetType().Name}: remount without rebind must not revive invalidated deferred focus.");
            screen.Hide(); bind(); await Layout();
            Check(GetViewport().GuiGetFocusOwner() is null, $"{screen.GetType().Name}: hidden preparation cannot acquire focus.");
            screen.Show(); screen.ProcessMode = ProcessModeEnum.Disabled; bind(); await Layout();
            Check(GetViewport().GuiGetFocusOwner() is null, $"{screen.GetType().Name}: inactive preparation cannot acquire focus.");
            screen.ProcessMode = ProcessModeEnum.Inherit;
            bind(); var alternate = Descendants(screen).OfType<Button>().First(button => button.Name != primaryName);
            alternate.GrabFocus(); await Layout();
            Check(alternate.HasFocus(), $"{screen.GetType().Name}: delayed initial focus cannot steal explicit selection.");
            foreach (var size in new[] { new Vector2I(640, 360), new Vector2I(1280, 720) })
            {
                GetWindow().Size = size; bind(); await Layout();
                var visible = GetViewport().GetVisibleRect();
                var primary = Button(screen, primaryName);
                var scroll = Descendants(screen).OfType<ScrollContainer>().Single();
                if (scroll.IsAncestorOf(primary)) scroll.EnsureControlVisible(primary);
                await Layout();
                var rect = primary.GetGlobalRect();
                Check(rect.Position.X >= -1 && rect.End.X <= visible.End.X + 1,
                    $"{screen.GetType().Name} {size}: primary horizontal bounds {rect} exceed {visible}.");
                Check(rect.Position.Y >= -1 && rect.End.Y <= visible.End.Y + 1,
                    $"{screen.GetType().Name} {size}: primary cannot be reached vertically: {rect} / {visible}.");
                Check(scroll.GetHScrollBar().MaxValue <= scroll.GetHScrollBar().Page + 1,
                    $"{screen.GetType().Name} {size}: disabled horizontal scrollbar hides overflowing content ({scroll.GetHScrollBar().MaxValue}/{scroll.GetHScrollBar().Page}).");
            }
            RemoveChild(screen); screen.QueueFree(); await Layout();
        }
    }
}
