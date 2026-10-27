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
    private CampaignShell? shell;
    public System.Collections.Generic.IReadOnlyList<string> RenderingDiagnostics => shell?.RenderingDiagnostics ?? Array.Empty<string>();
    public override void _ExitTree()
    {
        generation++;
        handled = true;
    }

    public void ShowCompletion(GameText text, string heading, string reason, bool victory, Action settle, Action retry, Action exit)
    {
        var column = Begin();
        Line(column, heading, 36);
        Line(column, reason);
        if (victory)
            ActionButton(column, "Settle", text.Format("ui.result.settle"), settle);
        ActionButton(column, "Retry", text.Format("ui.result.retry"), retry);
        ActionButton(column, "Exit", text.Format("ui.result.camp"), exit);
    }

    private VBoxContainer Begin(CampaignShellView? shellView = null, Action? onExit = null)
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
            shell.Show(shellView, () =>
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
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 22);
        var card = new DispatchPanel { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        scroll.AddChild(card);
        card.AddChild(column);
        return column;
    }

    private static void Line(VBoxContainer column, string text, int size = 20)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("#eee1bd"));
        column.AddChild(label);
    }

    private void ActionButton(VBoxContainer column, string name, string text, Action action)
    {
        var token = generation;
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 48) };
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
        if (name is "Settle" or "Proceed" or "Retry") CampaignUiStyle.InitialFocus(button);
    }

    public void ShowResult(string outcome, CampaignBattleSummary summary, Action proceed, CampaignShellView? shellView = null, Action? onExit = null)
    {
        if (outcome != "victory")
            throw new ArgumentException("Only committed victories have a campaign result summary.", nameof(outcome));
        var column = Begin(shellView, onExit);
        void Line(string text, int size = 20) => BattleResultScreen.Line(column, text, size);
        Line(summary.Heading, 24);
        Line(summary.Title, 36);
        Line(summary.Outcome);
        var metrics = new GridContainer { Columns = 2 };
        column.AddChild(metrics);
        foreach (var metric in summary.Metrics)
        {
            metrics.AddChild(new Label { Text = metric.Label, CustomMinimumSize = new Vector2(170, 32) });
            metrics.AddChild(new Label { Text = metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        Line(summary.FallenLabel + "\n" + summary.FallenText);
        Line(summary.SignalsLabel + "\n" + summary.SignalsText);
        ActionButton(column, "Proceed", summary.ContinueLabel, proceed);
    }
}
