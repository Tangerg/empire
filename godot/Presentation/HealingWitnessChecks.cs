#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

/// <summary>Interactive native QA over a hash-checked legal healing checkpoint, not a played campaign claim.</summary>
public partial class HealingWitnessChecks : Control
{
    public override void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-12-healing-target.json");
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var battle = engine.CreateBattle(level);
            if (battle.StateHash != fixture["initialStateHash"]!.GetValue<string>())
                throw new InvalidOperationException("Healing witness initial state differs from its legal recording.");
            foreach (var action in fixture["actions"]!.AsArray().Take(fixture["index"]!.GetValue<int>()).OfType<JsonObject>())
                battle.Dispatch(action);
            if (battle.StateHash != fixture["beforeStateHash"]!.GetValue<string>())
                throw new InvalidOperationException("Healing witness checkpoint differs from its legal recording.");
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
            var scene = AuthoredScene.Load("res://data/scenes/c01-12.json");
            var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
            var screen = new BattleScreen();
            AddChild(screen);
            screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)),
                level, scene,
                initialLogMessage: "C12 治疗验收：已恢复合法回放检查点");
            screen.ConfigureGround(rendering.Ground);
            screen.ConfigureDynamicState();
            if (!screen.Board.SurfaceReady || screen.Board.RenderingIssueCount != 0)
                throw new InvalidOperationException("Healing witness requires the production ground renderer without rendering issues.");
            var marker = new Label
            {
                Text = "C12 治疗验收 · 合法回放检查点 · 非连续实玩战绩",
                Position = new Vector2(340, 8),
                ZIndex = 4096,
                MouseFilter = MouseFilterEnum.Ignore
            };
            marker.AddThemeFontSizeOverride("font_size", 13);
            marker.AddThemeColorOverride("font_color", Colors.White);
            marker.AddThemeColorOverride("font_shadow_color", Colors.Black);
            marker.AddThemeConstantOverride("shadow_outline_size", 4);
            AddChild(marker);
            GetWindow().Title = "Empire HEALING WITNESS — legal recorded checkpoint, interactive native UI";
            GD.Print($"HEALING_WITNESS checkpoint={battle.StateHash}; source=4 at(7,4), target=6 at(8,4); command not dispatched.");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
