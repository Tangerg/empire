#nullable enable
using System;
using Godot;
using Empire.Presentation.Feedback;

namespace Empire.Presentation;

/// <summary>Result facts and one explicit navigation action; no campaign decisions live here.</summary>
public partial class BattleResultScreen : Control
{
    private int generation;
    private bool handled;
    public override void _ExitTree() { generation++; handled = true; }

    public void ShowCompletion(string heading, string reason, bool victory, Action settle, Action retry, Action exit)
    {
        var column = Begin();
        Line(column, heading, 36); Line(column, reason);
        if (victory) ActionButton(column, "Settle", "结算战果", settle);
        ActionButton(column, "Retry", "再来一次", retry);
        ActionButton(column, "Exit", "战役营地", exit);
    }

    private VBoxContainer Begin()
    {
        generation++; handled = false;
        foreach (Node child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#091712"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 48);
        AddChild(margin);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 22); scroll.AddChild(column);
        return column;
    }

    private static void Line(VBoxContainer column, string text, int size = 20)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color("#eee1bd")); column.AddChild(label);
    }

    private void ActionButton(VBoxContainer column, string name, string text, Action action)
    {
        var token = generation;
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 48) };
        button.Pressed += () => {
            if (handled || token != generation || !IsInsideTree() || !button.IsInsideTree()) return;
            handled = true;
            foreach (var child in column.GetChildren()) if (child is Button control) control.Disabled = true;
            action();
        };
        column.AddChild(button);
    }

    public void ShowResult(string outcome, CampaignBattleSummary summary, Action proceed)
    {
        if (outcome != "victory") throw new ArgumentException("Only committed victories have a campaign result summary.", nameof(outcome));
        var column = Begin();
        var victory = true;
        void Line(string text, int size = 20) => BattleResultScreen.Line(column, text, size);
        Line(victory ? CampaignBattleSummary.Heading : "战斗结束", 24);
        Line(summary.Title, 36); Line(summary.Outcome);
        if (victory)
        {
            var metrics = new GridContainer { Columns = 2 }; column.AddChild(metrics);
            foreach (var metric in summary.Metrics)
            {
                metrics.AddChild(new Label { Text = metric.Label, CustomMinimumSize = new Vector2(170, 32) });
                metrics.AddChild(new Label { Text = metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            Line(CampaignBattleSummary.FallenLabel + "\n" + summary.FallenText);
            Line(CampaignBattleSummary.SignalsLabel + "\n" + summary.SignalsText);
        }
        ActionButton(column, "Proceed", CampaignBattleSummary.ContinueLabel, proceed);
    }
}
