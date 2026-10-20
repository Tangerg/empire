using Godot;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation;
using Empire.Presentation.Feedback;

namespace Empire.Application;

/// <summary>Thin native application flow. Canonical aggregates own every gameplay transition.</summary>
public partial class GrayFlagGame : Node
{
    private GrayFlagEngine engine = null!;
    private JsonArray packs = null!;
    private GrayFlagCampaign campaign = null!;
    private StoryDocument stories = null!;
    private JsonObject definition = null!;
    private JsonArray levels = null!;
    private JsonObject presentation = null!;
    private NarrativePresentationDocument narrative = null!;
    private GrayFlagSession? session;
    private BattleScreen? screen;
    private Control? view;
    private readonly AiTurnPacer computerPacer = new();
    private bool resultOffered;
    private long lifecycleGeneration;

    public override void _ExitTree()
    {
        lifecycleGeneration++;
        computerPacer.Cancel();
        screen?.SetApplicationBusy(false);
    }
    private const string SavePath = "user://grayflag-campaign-save.json";
    private const string CampaignSavePath = "user://grayflag-campaign-autosave.json";

    public override void _Ready()
    {
        packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        engine = new GrayFlagEngine(packs);
        definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        presentation = PresentationJson.Load<JsonObject>("res://data/grayflag-staging.json");
        narrative = NarrativePresentationDocument.Load();
        campaign = NewCampaign();
        stories = StoryDocument.Load();
        ShowMenu();
    }

    public override void _Process(double delta) => ProcessBattle(Time.GetTicksMsec());

    private void ProcessBattle(double nowMilliseconds)
    {
        if (!IsInsideTree() || session is null) return;
        if (screen is { } currentScreen)
        {
            var currentSession = session;
            computerPacer.Tick(nowMilliseconds, currentSession.IsComputerTurn, currentScreen.IsPresenting,
                () => currentSession.AdvanceComputerStep(currentScreen.AppendMessage)
                    ?? throw new Empire.Core.DomainInvariantException("AI turn changed before its synchronous commit"),
                () => { currentScreen.Refresh(); return currentScreen.IsPresenting; }, currentScreen.SetApplicationBusy);
            if (!ReferenceEquals(session, currentSession) || !ReferenceEquals(screen, currentScreen)) return;
            if (computerPacer.IsRunning || currentScreen.IsPresenting) return;
        }
        if (session.Snapshot()["phase"]!.GetValue<string>() == "over")
        {
            if (!resultOffered)
            {
                resultOffered = true;
                var expectedSession = session;
                var report = campaign.BattleSummary();
                var result = new BattleResultScreen();
                screen = null;
                SetView(result);
                var state = expectedSession.Snapshot();
                result.ShowCompletion(state["winnerTeam"] is null ? "平局" : report.Outcome == "victory" ? "胜利" : "战败",
                    state["endReason"]?.GetValue<string>() ?? "", report.Outcome == "victory",
                    () => {
                        if (!ReferenceEquals(session, expectedSession) || !ReferenceEquals(view, result)) return;
                        campaign.FinishBattle(); session = null;
                        var saved = TryPersistCampaign();
                        var summary = new BattleResultScreen(); SetView(summary);
                        var stateAfter = campaign.Runtime.Snapshot();
                        var summaryShell = CampaignShellView.Create(new NarrativeContext(narrative,
                            stateAfter["battleHistory"]!.AsArray().Count, stateAfter["status"]!.GetValue<string>() == "active"), report.Summary.Chapter);
                        summary.ShowResult("victory", report.Summary, () => { if (ReferenceEquals(view, summary)) ShowNode(); },
                            summaryShell, () => { if (ReferenceEquals(view, summary)) ShowMenu(); });
                        if (!saved) Notify("战果已在当前战役中结算，但自动保存失败。请保留窗口并使用 F5 保存。");
                    },
                    () => {
                        if (!ReferenceEquals(session, expectedSession) || !ReferenceEquals(view, result)) return;
                        campaign.RestartBattle(); resultOffered = false; ShowBattle("");
                    },
                    () => { if (ReferenceEquals(session, expectedSession) && ReferenceEquals(view, result)) ExitBattle(); });
            }
            return;
        }

    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (!IsInsideTree() || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F5) Save();
        if (key.Keycode == Key.F9) Load();
    }

    private void SetView(Control next)
    {
        computerPacer.Cancel();
        if (view is BattleScreen previousBattle) previousBattle.SetApplicationBusy(false);
        if (view is not null) { RemoveChild(view); view.QueueFree(); }
        view = next; AddChild(next);
    }

    private GrayFlagCampaign NewCampaign(JsonObject? state = null) => new(definition, levels, packs, engine, state);

    private bool TryPersistCampaign()
    {
        try { CampaignSlotStorage.Save(definition, campaign.Runtime.Snapshot(), ProjectSettings.GlobalizePath(CampaignSavePath)); return true; }
        catch (Exception error) { GD.PushWarning("Campaign autosave failed: " + error.Message); return false; }
    }

    private void ShowMenu()
    {
        screen = null; session = null; resultOffered = false;
        var loaded = CampaignSlotStorage.Load(definition, ProjectSettings.GlobalizePath(CampaignSavePath));
        var projection = CampaignMenu.Create(loaded, Empire.Core.Determinism.EcmaJson.ToElement(presentation));
        var menu = new CampaignMenuScreen(); SetView(menu);
        var generation = lifecycleGeneration;
        bool CurrentMenu() => generation == lifecycleGeneration && IsInsideTree() && menu.IsInsideTree() && ReferenceEquals(view, menu);
        void StartNew()
        {
            if (!CurrentMenu()) return;
            try { CampaignSlotStorage.ClearForNewCampaign(ProjectSettings.GlobalizePath(CampaignSavePath)); campaign = NewCampaign(); ShowNode(); }
            catch (Exception error) { ShowMenu(); Notify("无法开始战役：" + error.Message); }
        }
        menu.ShowMenu(projection, () => {
            if (!CurrentMenu() || loaded.State is null) return;
            campaign = NewCampaign(loaded.State); ShowNode();
        }, () => {
            if (!CurrentMenu()) return;
            if (loaded.State is null) { StartNew(); return; }
            var dialog = new ConfirmationDialog { Title = "重新开始战役", DialogText = projection.ReplacePrompt };
            var handled = false;
            dialog.Confirmed += () => { if (handled) return; handled = true; dialog.QueueFree(); StartNew(); };
            void Cancel() { if (handled) return; handled = true; dialog.QueueFree(); if (CurrentMenu()) ShowMenu(); }
            dialog.Canceled += Cancel; dialog.CloseRequested += Cancel;
            menu.AddChild(dialog); dialog.PopupCentered();
        });
    }

    private void AdvanceCampaign(Action transition)
    {
        try { transition(); }
        catch (Exception error) { ShowNode(); Notify("战役操作失败：" + error.Message); return; }
        var saved = TryPersistCampaign();
        ShowNode();
        if (!saved) Notify("战役已继续，但自动保存失败。当前进度仍保留在窗口中，请使用 F5 保存。");
    }

    private void ShowNode(string? battleLogMessage = null)
    {
        screen = null; session = null; resultOffered = false;
        var node = campaign.Runtime.Node();
        var type = node["type"]!.GetValue<string>();
        if (type == "battle")
        {
            if (campaign.Battle is null || campaign.AtCamp) ShowStaging();
            else ShowBattle(battleLogMessage);
            return;
        }
        var story = new StoryScreen(); SetView(story);
        var presentation = node["presentation"]?.GetValue<string>();
        var state = campaign.Runtime.Snapshot();
        var context = new NarrativeContext(narrative, state["battleHistory"]!.AsArray().Count, state["status"]!.GetValue<string>() == "active");
        bool Current() => ReferenceEquals(view, story);
        if (type == "choice")
            story.ShowChoice(stories.Choices.Single(value => value.Id == presentation),
                campaign.Runtime.Choices().Select(value => value!["id"]!.GetValue<string>()).ToArray(), context,
                id => { if (Current()) AdvanceCampaign(() => campaign.Runtime.Choose(id)); },
                () => { if (Current()) ShowMenu(); });
        else
            story.ShowStory(stories.Stories.Single(value => value.Id == presentation), stories.Speakers, context, type == "ending",
                () => { if (Current()) AdvanceCampaign(() => campaign.Runtime.Advance()); },
                () => {
                    if (!Current()) return;
                    if (campaign.Runtime.Snapshot()["status"]!.GetValue<string>() == "active") AdvanceCampaign(() => campaign.Runtime.Advance());
                    else ShowMenu();
                }, () => { if (Current()) ShowMenu(); });
    }

    private void ShowStaging()
    {
        screen = null; session = null; resultOffered = false;
        var staging = new CampaignStagingScreen(); SetView(staging);
        var metadata = PresentationJson.Load<JsonObject>("res://data/grayflag-staging.json");
        var projection = CampaignStaging.Create(Empire.Core.Determinism.EcmaJson.ToElement(campaign.Runtime.Snapshot()),
            Empire.Core.Determinism.EcmaJson.ToElement(campaign.Runtime.Node()),
            Empire.Core.Determinism.EcmaJson.ToElement(campaign.BattleLevel()),
            Empire.Core.Determinism.EcmaJson.ToElement(metadata), engine.UnitTypeName, campaign.Request is not null);
        staging.ShowStaging(projection, AssetCatalog.Load(), () => {
            if (!ReferenceEquals(view, staging)) return;
            if (campaign.Request is null)
            {
                try { CampaignSlotStorage.Save(definition, campaign.Runtime.Snapshot(), ProjectSettings.GlobalizePath(CampaignSavePath)); }
                catch (Exception error) { ShowStaging(); Notify("自动保存失败，尚未进入战斗：" + error.Message); return; }
            }
            campaign.BeginBattle(); ShowBattle();
        }, () => { if (ReferenceEquals(view, staging)) ShowMenu(); });
    }

    private void ExitBattle()
    {
        campaign.ExitBattle(); ShowStaging();
    }

    private void ShowBattle(string? initialLogMessage = null)
    {
        session = campaign.Battle!;
        var level = campaign.Request!["level"]!.AsObject();
        var id = level["id"]!.GetValue<string>();
        var scenePath = $"res://data/scenes/{id}.json";
        var scene = Godot.FileAccess.FileExists(scenePath) ? AuthoredScene.Load(scenePath) : new AuthoredScene
        {
            LevelId = id, MapSize = new[] { level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>() },
            RuntimeReady = false, Title = "此关卡尚无恢复的手工场景布局"
        };
        screen = new BattleScreen(); SetView(screen);
        var grid = engine.Grids.Get(level["rules"]?["grid"]?.GetValue<string>() ?? "square4");
        screen.BindSession(session, AssetCatalog.Load(), level, scene, EnvironmentCatalog.Load(),
            facing => facing is not null && grid.Step(new Coord(0, 0), facing).X < 0,
            facing => { if (facing is null) return Vector2.Zero; var origin = grid.Center(new Coord(0, 0)); var point = grid.Center(grid.Step(new Coord(0, 0), facing)); return new Vector2((float)(point.X - origin.X), (float)(point.Y - origin.Y)); }, initialLogMessage);
        screen.ConfigureTerrain(WorldMaterialsManifest.Load());
        screen.ConfigureDynamicState(packs);
        var boundSession = session;
        var boundScreen = screen;
        var boundGeneration = lifecycleGeneration;
        bool CurrentBattle() => IsInsideTree() && boundGeneration == lifecycleGeneration &&
            ReferenceEquals(session, boundSession) && ReferenceEquals(screen, boundScreen);
        screen.BindApplicationActions(() => { if (CurrentBattle()) Save(); }, () => { if (CurrentBattle()) Load(); }, () => {
            if (!CurrentBattle() || boundScreen.IsInputBlocked) return;
            campaign.RestartBattle(); resultOffered = false; ShowBattle("");
        }, () => { if (CurrentBattle() && !boundScreen.IsInputBlocked) ExitBattle(); });
    }

    private void WriteSave(string resourcePath)
    {
        var path = ProjectSettings.GlobalizePath(resourcePath);
        System.IO.File.WriteAllText(path + ".tmp", Empire.Core.Determinism.EcmaJson.Stringify(campaign.Save()));
        System.IO.File.Move(path + ".tmp", path, true);
    }

    private void Save()
    {
        if (!IsInsideTree() || computerPacer.IsRunning || screen?.IsInputBlocked == true) return;
        try
        {
            WriteSave(SavePath);
            if (session is not null) screen?.AppendMessage($"已保存第 {session.Snapshot()["turn"]} 回合的进度");
            Notify("战役和战斗进度已保存。F9 可读取。");
        }
        catch (Exception error) { Notify("保存失败：" + error.Message); }
    }

    private void Load()
    {
        if (!IsInsideTree() || computerPacer.IsRunning || screen?.IsInputBlocked == true) return;
        try
        {
            campaign.Load(Empire.Core.Determinism.EcmaJson.Parse(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(SavePath)))!.AsObject());
        }
        catch (Exception error) { screen?.AppendMessage("无法读取存档：" + error.Message); Notify("读取失败，当前进度未变：" + error.Message); return; }
        try { ShowNode(campaign.Battle is null ? null : $"已读取第 {campaign.Battle.Snapshot()["turn"]} 回合的存档"); Notify("已读取存档。"); }
        catch (Exception error) { Notify("存档已恢复，但画面加载失败：" + error.Message); }
    }

    private void Notify(string message)
    {
        if (screen is not null) { screen.ShowFeedback(message); return; }
        var dialog = new AcceptDialog { Title = "灰旗", DialogText = message };
        dialog.Confirmed += dialog.QueueFree;
        dialog.CloseRequested += dialog.QueueFree;
        AddChild(dialog); dialog.PopupCentered();
    }
}
