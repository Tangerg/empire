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
    private GrayFlagSession? session;
    private BattleScreen? screen;
    private Control? view;
    private double computerDelay;
    private bool resultOffered;
    private const string SavePath = "user://grayflag-campaign-save.json";
    private const string CampaignSavePath = "user://grayflag-campaign-autosave.json";

    public override void _Ready()
    {
        packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        engine = new GrayFlagEngine(packs);
        campaign = new GrayFlagCampaign(PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json"),
            PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"), packs, engine);
        stories = StoryDocument.Load();
        ShowNode();
    }

    public override void _Process(double delta)
    {
        if (session is null || screen?.IsPresenting == true) return;
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
                        var summary = new BattleResultScreen(); SetView(summary);
                        summary.ShowResult("victory", report.Summary, () => { if (ReferenceEquals(view, summary)) ShowNode(); });
                    },
                    () => {
                        if (!ReferenceEquals(session, expectedSession) || !ReferenceEquals(view, result)) return;
                        campaign.RestartBattle(); resultOffered = false; ShowBattle("");
                    },
                    () => { if (ReferenceEquals(session, expectedSession) && ReferenceEquals(view, result)) ExitBattle(); });
            }
            return;
        }
        if (!session.IsComputerTurn) { computerDelay = 0; return; }
        computerDelay += delta;
        if (computerDelay < 0.3) return;
        computerDelay = 0;
        if (session.AdvanceComputer()) screen!.Refresh();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F5) Save();
        if (key.Keycode == Key.F9) Load();
    }

    private void SetView(Control next)
    {
        if (view is not null) { RemoveChild(view); view.QueueFree(); }
        view = next; AddChild(next);
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
        if (type == "choice")
            story.ShowChoice(stories.Choices.Single(value => value.Id == presentation),
                campaign.Runtime.Choices().Select(value => value!["id"]!.GetValue<string>()).ToArray(),
                id => { campaign.Runtime.Choose(id); ShowNode(); });
        else
            story.ShowStory(stories.Stories.Single(value => value.Id == presentation), stories.Speakers,
                () => { if (campaign.Runtime.Snapshot()["status"]!.GetValue<string>() == "active") campaign.Runtime.Advance(); if (campaign.Runtime.Snapshot()["status"]!.GetValue<string>() == "active") ShowNode(); else Notify("灰旗战役已结束。F5 保存，F9 读取存档。"); });
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
                try { WriteSave(CampaignSavePath); }
                catch (Exception error) { ShowStaging(); Notify("自动保存失败，尚未进入战斗：" + error.Message); return; }
            }
            campaign.BeginBattle(); ShowBattle();
        });
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
        screen.BindApplicationActions(Save, Load, () => {
                campaign.RestartBattle(); resultOffered = false; ShowBattle("");
        }, ExitBattle);
    }

    private void WriteSave(string resourcePath)
    {
        var path = ProjectSettings.GlobalizePath(resourcePath);
        System.IO.File.WriteAllText(path + ".tmp", Empire.Core.Determinism.EcmaJson.Stringify(campaign.Save()));
        System.IO.File.Move(path + ".tmp", path, true);
    }

    private void Save()
    {
        if (screen?.IsPresenting == true) return;
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
        if (screen?.IsPresenting == true) return;
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
