#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Detached initial projections only: no dispatcher, action subscription, AI or save writes.</summary>
public partial class InitialHudCoverageChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
            var art = new TacticalHudArt(AssetCatalog.Load(), PortraitCatalogResources.Load(),
                PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json"));
            var filter = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--hud-level="))?[12..];
            var samples = new List<object>();
            if (filter is not null)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GetWindow().Size = new Vector2I(960, 640);
            }
            foreach (var level in PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Take(7))
            {
                var id = level["id"]!.GetValue<string>();
                if (filter is not null && filter != id)
                    continue;
                var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
                var snapshot = session.Snapshot();
                var human = snapshot["players"]!.AsArray().OfType<JsonObject>().Single(player => player["controller"]!.GetValue<string>() == "human")["id"]!.GetValue<int>();
                var before = EcmaJson.Stringify(snapshot);
                var unitArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--hud-unit="))?[11..];
                var selections = new List<TacticalSelection> { unitArgument is null ? new() : new() { Kind = TacticalSelectionKind.Unit, UnitId = int.Parse(unitArgument) } };
                if (filter is null)
                    selections.AddRange(snapshot["units"]!.AsArray().OfType<JsonObject>()
                        .Where(unit => unit["owner"]!.GetValue<int>() == human)
                        .Select(unit => new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = unit["id"]!.GetValue<int>() }));
                foreach (var selection in selections)
                {
                    var projection = session.ProjectHud(new(0, 0), selection, null, selection.UnitId, false);
                    foreach (var size in filter is null ? new[] { new Vector2I(960, 640), new Vector2I(1440, 900) } : new[] { GetWindow().Size })
                    {
                        GetWindow().Size = size;
                        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
                        hud.ConfigureArt(art);
                        AddChild(hud);
                        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                        hud.Apply(projection);
                        for (var frame = 0; frame < 6; frame++)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var labels = hud.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree())
                            .Select(label => new
                            {
                                text = label.Text,
                                x = label.GetGlobalRect().Position.X,
                                y = label.GetGlobalRect().Position.Y,
                                width = label.Size.X,
                                height = label.Size.Y,
                                lines = label.GetLineCount(),
                                visibleLines = label.GetVisibleLineCount(),
                                clipped = label.ClipText,
                                outsideWindow = !new Rect2(Vector2.Zero, size).Encloses(label.GetGlobalRect())
                            }).ToArray();
                        var buttons = hud.FindChildren("*", "Button", true, false).OfType<Button>().Where(button => button.IsVisibleInTree())
                            .Select(button => new
                            {
                                text = button.Text,
                                width = button.Size.X,
                                minimumWidth = button.GetCombinedMinimumSize().X,
                                outsideWindow = !new Rect2(Vector2.Zero, GetWindow().Size).Encloses(button.GetGlobalRect())
                            }).ToArray();
                        if (labels.Any(label => label.outsideWindow || label.visibleLines != label.lines)
                            || buttons.Any(button => button.outsideWindow || button.width + 0.01f < button.minimumWidth))
                            throw new InvalidOperationException($"Initial HUD bounds failed: {id}, {selection.UnitId}, {size}.");
                        samples.Add(new
                        {
                            buttons,
                            level = id,
                            selectedUnit = selection.UnitId,
                            width = size.X,
                            height = size.Y,
                            sections = projection.Sections.Select(section => new { section.Key, section.Title, rows = section.Rows.Select(row => new { row.Label, row.Hint }) }),
                            labels
                        });
                        if (session.Revision != 0 || session.Events.Count != 0 || EcmaJson.Stringify(session.Snapshot()) != before)
                            throw new InvalidOperationException("A static HUD audit changed battle state.");
                        if (filter is not null)
                        {
                            AddChild(new Label { Text = "初始 HUD 静态审查 · 无行动 / 无 AI · 非实玩", Position = new Vector2(18, 130) });
                            GD.Print(JsonSerializer.Serialize(samples));
                            return;
                        }
                        RemoveChild(hud);
                        hud.QueueFree();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                }
            }
            if (samples.Count == 0)
                throw new InvalidOperationException("No initial HUD samples matched the requested level.");
            GD.Print("INITIAL_HUD_COVERAGE " + JsonSerializer.Serialize(samples));
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
