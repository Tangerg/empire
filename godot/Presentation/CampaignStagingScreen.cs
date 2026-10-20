using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>Renders supplied staging facts; the caller exclusively owns start/reentry and campaign state.</summary>
public partial class CampaignStagingScreen : Control
{
    private readonly List<string> diagnostics = new();
    private int generation;
    private bool submitted;
    private CampaignShell? shell;
    public CampaignStagingView? Presented { get; private set; }
    public IReadOnlyList<string> RenderingDiagnostics => diagnostics.Concat(shell?.RenderingDiagnostics ?? Array.Empty<string>()).ToArray();

    public override void _ExitTree() { generation++; submitted = true; }

    public void ShowStaging(CampaignStagingView view, AssetCatalog assets, Action startBattle, Action exitCampaign)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(startBattle);
        ArgumentNullException.ThrowIfNull(exitCampaign);
        generation++;
        var currentGeneration = generation;
        submitted = false; Presented = view; diagnostics.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shell = new CampaignShell(); AddChild(shell);
        Button? startButton = null;
        shell.Show(new CampaignShellView(view.CampaignTitle, view.ChapterHeading, view.CompletedBattles, view.BattleTotal,
            new NarrativeArt { Source = view.ChapterArt.Source, ResourcePath = view.ChapterArt.ResourcePath }), () => {
            if (submitted || currentGeneration != generation || !IsInsideTree()) return;
            submitted = true; shell.DisableNavigation();
            if (startButton is not null) startButton.Disabled = true;
            exitCampaign();
        });
        var body = shell.Body;
        var column = body;
        var hero = new HBoxContainer(); hero.AddThemeConstantOverride("separation", 24); body.AddChild(hero);
        if (view.BriefingArt is { } briefing && Texture(briefing, "briefing scene") is { } scene)
        {
            var picture = Picture(scene); picture.CustomMinimumSize = new Vector2(250, 170); picture.SizeFlagsHorizontal = SizeFlags.ExpandFill; hero.AddChild(picture);
        }
        var briefingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; briefingText.AddThemeConstantOverride("separation", 10); hero.AddChild(briefingText);
        briefingText.AddChild(Text(view.Eyebrow, 16, "#d5bc7f")); briefingText.AddChild(Text(view.Title, 32, "#f1e4c0"));
        briefingText.AddChild(Text(view.Description, 19, "#d4d9cd"));
        var panels = new HBoxContainer(); panels.AddThemeConstantOverride("separation", 40); body.AddChild(panels);
        var roster = Panel(panels, CampaignStagingView.RosterHeading);
        foreach (var unit in view.Roster)
        {
            var row = new HBoxContainer { Name = "Roster_" + unit.Id }; row.AddThemeConstantOverride("separation", 14); roster.AddChild(row);
            Texture2D? avatar = unit.Portrait is { } portrait ? Texture(portrait, $"portrait {unit.Id}") : null;
            if (avatar is null)
            {
                try
                {
                    var asset = assets.ResolveUnit(unit.UnitType, unit.ActorKey);
                    avatar = assets.CreateFrameTexture(asset, AssetCatalog.FrameIndex(asset, "idle"));
                }
                catch (MissingArtResourceException error) { diagnostics.Add($"Roster {unit.Id}: {error.Message}"); }
            }
            if (avatar is not null)
            {
                var picture = Picture(avatar); picture.Name = "Avatar"; picture.CustomMinimumSize = new Vector2(56, 56);
                picture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered; picture.TextureFilter = TextureFilterEnum.Nearest;
                row.AddChild(picture);
            }
            var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(labels);
            labels.AddChild(Text(unit.Name, 20, "#eee5cf")); labels.AddChild(Text(unit.Status, 15, "#bbc8b9"));
        }
        var status = Panel(panels, CampaignStagingView.StateHeading);
        if (view.Resources.Count == 0) status.AddChild(Text(CampaignStagingView.EmptyResources, 18, "#bbc8b9"));
        foreach (var resource in view.Resources) status.AddChild(ValueRow(resource.Label, resource.Amount));
        status.AddChild(new HSeparator());
        if (view.Relations.Count == 0) status.AddChild(Text(CampaignStagingView.EmptyRelations, 18, "#bbc8b9"));
        foreach (var relation in view.Relations)
        {
            status.AddChild(ValueRow(relation.Label, relation.Amount));
            status.AddChild(new ProgressBar { Value = relation.Fill * 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) });
        }
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 24); column.AddChild(footer);
        footer.AddChild(Text(view.ActionNote, 16, "#b8c6b6"));
        var button = new Button { Name = "StartBattle", Text = view.StartLabel, CustomMinimumSize = new Vector2(210, 48) };
        startButton = button;
        button.Pressed += () =>
        {
            if (submitted || currentGeneration != generation || !IsInsideTree() || !button.IsInsideTree()) return;
            submitted = true; button.Disabled = true; shell.DisableNavigation(); startBattle();
        };
        footer.AddChild(button);
    }

    private Texture2D? Texture(CampaignStagingArt art, string purpose)
    {
        if (art.ResourcePath is null)
        {
            diagnostics.Add($"Original {purpose} is not exported: {art.Source}");
            return null;
        }
        if (!Godot.FileAccess.FileExists(art.ResourcePath))
        {
            diagnostics.Add($"Exported {purpose} is missing: {art.ResourcePath} (source {art.Source})");
            return null;
        }
        return GD.Load<Texture2D>(art.ResourcePath) ?? throw new InvalidOperationException($"Cannot load {purpose}: {art.ResourcePath}");
    }
    private static VBoxContainer Panel(Node parent, string heading)
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 12); parent.AddChild(panel); panel.AddChild(Text(heading, 23, "#e6d5a5"));
        return panel;
    }
    private static HBoxContainer ValueRow(string label, string value)
    {
        var row = new HBoxContainer(); row.AddChild(Text(label, 18, "#bbc8b9")); row.AddChild(Text(value, 19, "#eeddb1")); return row;
    }
    private static TextureRect Picture(Texture2D texture) => new() { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
    private static Label Text(string value, int size, string color)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); return label;
    }
}
