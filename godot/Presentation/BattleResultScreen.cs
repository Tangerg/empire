#nullable enable
using System;
using Godot;
using Empire.Presentation.Feedback;
using Empire.Localization;

namespace Empire.Presentation;

/// <summary>Result facts and one explicit navigation action; no campaign decisions live here.</summary>
public partial class BattleResultScreen : Control
{
    private int generation;
    private bool handled;
    private BoxContainer actions = null!;
    private CampaignShell? shell;
    public System.Collections.Generic.IReadOnlyList<string> RenderingDiagnostics => shell?.RenderingDiagnostics ?? Array.Empty<string>();
    public override void _ExitTree()
    {
        generation++;
        handled = true;
    }

    public void ShowCompletion(GameText text, string heading, string reason, bool victory, Action settle, Action retry, Action exit)
    {
        var column = Begin(readingHint: text.Format("ui.campaign.reading_hint"));
        Line(column, heading, 36);
        Line(column, reason);
        if (victory)
            ActionButton(actions, "Settle", text.Format("ui.result.settle"), settle);
        ActionButton(actions, "Retry", text.Format("ui.result.retry"), retry);
        ActionButton(actions, "Exit", text.Format("ui.result.camp"), exit);
    }

    private VBoxContainer Begin(CampaignShellView? shellView = null, Action? onExit = null, string readingHint = "")
    {
        Theme = CampaignUiStyle.Create();
        generation++;
        handled = false;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shell = null;
        if (shellView is not null)
        {
            ArgumentNullException.ThrowIfNull(onExit);
            shell = new CampaignShell();
            AddChild(shell);
            var token = generation;
            shell.Show(shellView with
            {
                ReadingHint = readingHint
            }, () =>
            {
                if (handled || token != generation || !IsInsideTree())
                    return;
                handled = true;
                shell.DisableNavigation();
                onExit();
            });
            var summaryCard = new DispatchPanel { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            shell.Body.AddChild(summaryCard);
            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 18);
            summaryCard.AddChild(content);
            actions = ActionRail(shell.Footer);
            return content;
        }
        var background = new ColorRect { Color = new Color("#25291f"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{edge}", 48);
        AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 16);
        margin.AddChild(layout);
        var scroll = new CampaignScroll
        {
            Name = "ResultScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 22);
        var card = new DispatchPanel { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        scroll.AddChild(card);
        card.AddChild(column);
        layout.AddChild(scroll.CreateReadingHint(readingHint));
        actions = ActionRail(layout);
        return column;
    }

    private static BoxContainer ActionRail(Node parent)
    {
        var surface = new PanelContainer { Name = "ResultActions", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        surface.AddThemeStyleboxOverride("panel", Empire.Presentation.Tactical.TacticalHudStyle.Panel("#30291f", 16));
        parent.AddChild(surface);
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        surface.AddChild(row);
        return row;
    }

    private static void Line(VBoxContainer column, string text, int size = 20)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("#eee1bd"));
        column.AddChild(label);
    }

    private void ActionButton(BoxContainer column, string name, string text, Action action)
    {
        var token = generation;
        var button = new Button { Name = name, Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48) };
        button.Pressed += () =>
        {
            if (handled || token != generation || !IsInsideTree() || !button.IsInsideTree())
                return;
            handled = true;
            foreach (var child in column.GetChildren())
                if (child is Button control)
                    control.Disabled = true;
            action();
        };
        column.AddChild(button);
        if (name is "Settle" or "Proceed" or "Retry")
            CampaignUiStyle.InitialFocus(button);
    }

    public void ShowResult(string outcome, CampaignBattleSummary summary, Action proceed, CampaignShellView? shellView = null, Action? onExit = null)
    {
        if (outcome != "victory")
            throw new ArgumentException("Only committed victories have a campaign result summary.", nameof(outcome));
        var column = Begin(shellView, onExit, summary.ReadingHint);
        void Line(string text, int size = 20) => BattleResultScreen.Line(column, text, size);
        Line(summary.Heading, 24);
        Line(summary.Title, 36);
        Line(summary.Outcome);
        var metrics = new GridContainer { Name = "ResultMetrics", Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        metrics.AddThemeConstantOverride("h_separation", 24);
        column.AddChild(metrics);
        void FitMetrics() => metrics.CustomMinimumSize = new Vector2(Math.Min(440, column.Size.X), 0);
        column.Resized += FitMetrics;
        FitMetrics();
        foreach (var metric in summary.Metrics)
        {
            metrics.AddChild(new Label { Text = metric.Label, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 32) });
            metrics.AddChild(new Label { Text = metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        Line(summary.FallenLabel + "\n" + summary.FallenText);
        Line(summary.SignalsLabel + "\n" + summary.SignalsText);
        ActionButton(actions, "Proceed", summary.ContinueLabel, proceed);
    }
}
