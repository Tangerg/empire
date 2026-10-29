using Godot;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

/// <summary>Opt-in detached visual QA. No engine, saves, settlement, or campaign transitions.</summary>
public partial class CampaignUiPreview : Control
{
    private Control? screen;
    private readonly GameText text = GodotTextCatalog.LoadSimplifiedChinese();
    private const string ArtPath = "res://assets/runtime-hd/narrative-static/arch-three-bridges.png";
    public override void _Ready()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--campaign-ui-fixture"))
            throw new InvalidOperationException("Visual QA requires --campaign-ui-fixture; it is not a campaign victory.");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Show(2);
    }
    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5)
        {
            Show((int)key.Keycode - (int)Key.F1);
            GetViewport().SetInputAsHandled();
        }
    }
    private void Show(int page)
    {
        if (screen is not null)
        {
            RemoveChild(screen);
            screen.QueueFree();
        }
        Action receipt = () => GD.Print("QA_FIXTURE_ACTION_ONLY: no campaign or save changed");
        var shell = new CampaignShellView("界面测试夹具 · 非实际战绩", "第一章 · 边境之火", 1, 16,
            new NarrativeArt { Source = "existing chapter art", ResourcePath = ArtPath }, "返回测试", "测试进度", "素材缺失");
        var longText = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"{i:D2} · 测试记录：山谷部队完成整备，长期战损和战役信号应保持可读，不能被按钮遮挡。"));
        if (page == 0)
        {
            var menu = new CampaignMenuScreen();
            screen = menu;
            AddChild(menu);
            menu.ShowMenu(new("界面测试夹具", "F1 菜单 · F2 备战 · F3 结算 · F4 胜负", longText, "", "existing art", ArtPath,
                true, "继续测试", "1 / 16", "重新开始测试", false, "仅测试", null, "素材缺失", text.Format("ui.campaign.reading_hint")), receipt, receipt);
        }
        else if (page == 1)
        {
            var staging = new CampaignStagingScreen();
            screen = staging;
            AddChild(staging);
            staging.ShowStaging(new(text, "qa-only", shell.Title, shell.ChapterHeading, "1/16", 1, 16, "备战长文测试", "双子丘陵 · 测试夹具",
                "第一条命令之前，请检视部队状态与战役资源。此画面用于验证排布，不会开始战斗。", "这是长操作提示：进入战斗前会保留关前存档，本夹具不读写任何存档。", "开始测试 →",
                new("existing art", ArtPath), new("existing briefing", "res://assets/runtime-hd/narrative-static/ch01-s01.png"),
                Enumerable.Range(0, 13).Select(i => new CampaignStagingUnit(i.ToString(), "archer", $"边境弓手 · 第 {i + 1} 队", "生命 78 / 78 · 待命", null, null)),
                [new("supply", "补给储备", "1250"), new("treasury", "国库", "860")], []),
                TacticalHudArt.Load(AssetCatalog.Load(), PortraitCatalogResources.Load()), receipt, receipt);
        }
        else if (page == 4)
        {
            var story = new StoryScreen();
            screen = story;
            AddChild(story);
            var documents = StoryDocument.Load(text);
            var portraits = PortraitCatalogResources.Load();
            story.ShowStory(documents.Stories.First(value => value.Beats.Count > 1), documents.IdentityNameKeys,
                new(text, portraits, NarrativePresentationDocument.Load(text, portraits), 0, true), false, receipt, receipt, receipt);
        }
        else
        {
            var result = new BattleResultScreen();
            screen = result;
            AddChild(result);
            if (page == 2)
                result.ShowResult("victory", new(text, "测试夹具 · 非实际通关", 1, "测试结果", 10, 9, 0,
                    [longText], [longText], 42), receipt, shell, receipt);
            else
                result.ShowCompletion(text, "测试夹具 · 胜负选项", longText, true, receipt, receipt, receipt);
        }
        GetWindow().Title = "Empire UI QA FIXTURE — F1 menu / F2 staging / F3 result / F4 completion / F5 story";
    }
}
