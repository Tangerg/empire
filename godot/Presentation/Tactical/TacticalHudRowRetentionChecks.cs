#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private async Task CheckUnitDetailsRowRetention()
    {
        var hud = new TacticalHud();
        AddChild(hud);
        try
        {
            var assets = AssetCatalog.Load();
            var portraits = PortraitCatalogResources.Load();
            hud.ConfigureArt(TacticalHudArt.Load(assets, portraits));
            var stamp = new TacticalStamp(901, 1);
            var rows = Enumerable.Range(0, 19).Select(i => new TacticalHudRow("row" + i, "Value " + i, "Hint " + i)).ToArray();
            TacticalHudProjection Projection(TacticalHudRow[] values, string title = "Dossier") => new(stamp, "", new[] { new TacticalHudSection("unit-details", title, values) });
            VBoxContainer Group() => (VBoxContainer)hud.FindChild("unit-details", true, false);
            VBoxContainer Content() => Group().GetNode<VBoxContainer>("Content");
            Label Text(Node line) => line.GetChildren().OfType<Label>().Single();
            hud.Apply(Projection(rows));
            var group = Group();
            var originals = Content().GetChildren().ToArray();
            var disclosure = group.GetChildren().OfType<Button>().Single();
            disclosure.ButtonPressed = true;
            var shorter = rows.Take(16).Reverse().Select(row => row.Key == "row5" ? row with { Label = "Updated", Hint = "Changed hint" } : row).ToArray();
            hud.Apply(Projection(shorter, "New title"));
            Require(Group() == group && Content().GetChildCount() == 16, "19 to 16 retains group and removes rows");
            for (int i = 0; i < shorter.Length; i++)
            {
                Require(Content().GetChild(i) == originals[15 - i], "Reorders original mounted fragments");
                Require(Text(Content().GetChild(i)).Text == shorter[i].Label && Text(Content().GetChild(i)).TooltipText == shorter[i].Hint, "Updates row text and hints");
            }
            Require(disclosure.ButtonPressed && Content().Visible && disclosure.Text == "−  New title", "Retained disclosure stays expanded with current title");
            Require(group.GetNode<HBoxContainer>("Heading").GetChildren().OfType<Label>().Single().Text == "New title", "Hidden heading updated");
            disclosure.ButtonPressed = false;
            Require(disclosure.Text == "+  New title", "Disclosure callback uses current projection title");
            hud.Apply(Projection(rows));
            Require(Content().GetChildCount() == 19 && Content().GetChild(5) == originals[5], "16 to 19 adds in original order and retains shared row");
            var changed = rows.ToArray();
            foreach (var variant in new[]
            {
                rows[5] with { Selected = true },
                rows[5] with { Gauge = new(2, 10) },
                rows[5] with { Icon = "weapon:c01.border-blade" },
                rows[5] with { Enabled = false }
            })
            {
                var before = Content().GetChild(5);
                changed[5] = variant;
                hud.Apply(Projection(changed.ToArray()));
                Require(Content().GetChild(5) != before && before.IsQueuedForDeletion(), "Non-text row change replaces entire fragment");
                Require(!before.GetChildren().OfType<TextureRect>().Any(texture => texture.Texture is not null), "Removed fragment drops native texture references before deferred destruction");
                if (variant.Icon is not null)
                    Require(Content().GetChild(5).GetChildren().OfType<TextureRect>().Single().Texture is not null, "Icon release test has positive native texture witness");
                Require(Content().GetChildren().OfType<ProgressBar>().Count() == (variant.Gauge is null ? 0 : 1), "Gauge sibling added or removed correctly");
            }
            foreach (var portrait in new[]
            {
                new TacticalHudPortrait("c01.laiya", null, "#ff0000"),
                new TacticalHudPortrait("c01.roderick", null, "#0000ff")
            })
            {
                var previousLine = Content().GetChild(5);
                changed[5] = rows[5] with
                {
                    UnitIcon = portrait
                };
                hud.Apply(Projection(changed.ToArray()));
                Require(Content().GetChild(5) != previousLine, "UnitIcon changes replace row");
                Require(Content().GetChild(5).GetChildren().OfType<TextureRect>().Single().Texture is not null, "UnitIcon has positive native texture witness");
                Require(previousLine.GetChildren().OfType<TextureRect>().All(texture => texture.Texture is null), "UnitIcon replacement releases old texture");
            }
            var iconLine = Content().GetChild(5);
            var oldIcon = iconLine.GetChildren().OfType<TextureRect>().Single();
            changed[5] = rows[5];
            hud.Apply(Projection(changed.ToArray()));
            Require(oldIcon.Texture is null && !Content().GetChild(5).GetChildren().OfType<TextureRect>().Any(), "UnitIcon removal releases texture and removes texture node");
            changed[5] = rows[5] with
            {
                Gauge = new(2, 10)
            };
            hud.Apply(Projection(changed.ToArray()));
            var gaugeLine = Content().GetChild(5);
            var gaugeBar = Content().GetChild<ProgressBar>(6);
            Require(gaugeBar.Value == 2 && gaugeBar.MaxValue == 10 && gaugeBar.HasThemeStyleboxOverride("fill"), "Low gauge value/max and warning style render");
            changed[5] = changed[5] with
            {
                Label = "Gauge text"
            };
            hud.Apply(Projection(changed.ToArray()));
            Require(Content().GetChild(5) == gaugeLine && Content().GetChild(6) == gaugeBar, "Text-only gauge row retains both siblings");
            changed[12] = rows[12] with
            {
                Gauge = new(8, 20)
            };
            hud.Apply(Projection(changed.ToArray()));
            var otherGaugeLine = Content().GetChild(13);
            var otherGaugeBar = Content().GetChild<ProgressBar>(14);
            var reversedGauges = changed.Reverse().ToArray();
            hud.Apply(Projection(reversedGauges));
            Require(Content().GetChild(6) == otherGaugeLine && Content().GetChild(7) == otherGaugeBar
                && Content().GetChild(14) == gaugeLine && Content().GetChild(15) == gaugeBar,
                "Reordered gauge fragments retain line and bar identities with correct adjacency");
            Require(Content().GetChildren().OfType<HBoxContainer>().Select(line => Text(line).Text)
                .SequenceEqual(reversedGauges.Select(row => row.Label)), "Gauge reorder preserves surrounding row order");
            Require(gaugeBar.Value == 2 && gaugeBar.MaxValue == 10 && otherGaugeBar.Value == 8 && otherGaugeBar.MaxValue == 20,
                "Reordered gauge siblings preserve their own values and maxima");
            hud.Apply(Projection(changed.ToArray()));
            Require(Content().GetChild(5) == gaugeLine && Content().GetChild(6) == gaugeBar
                && Content().GetChild(13) == otherGaugeLine && Content().GetChild(14) == otherGaugeBar
                && gaugeBar.Value == 2 && gaugeBar.MaxValue == 10 && otherGaugeBar.Value == 8 && otherGaugeBar.MaxValue == 20,
                "Restored gauge fragments retain identities, adjacency and values");
            Require(Content().GetChildren().OfType<HBoxContainer>().Select(line => Text(line).Text)
                .SequenceEqual(changed.Select(row => row.Label)), "Gauge restore preserves surrounding row order");
            changed[12] = rows[12];
            hud.Apply(Projection(changed.ToArray()));
            changed[5] = changed[5] with
            {
                Gauge = new(7, 10)
            };
            hud.Apply(Projection(changed.ToArray()));
            Require(Content().GetChild(5) != gaugeLine && Content().GetChild(6) != gaugeBar, "Gauge value change replaces both siblings");
            var normalGauge = Content().GetChild<ProgressBar>(6);
            Require(normalGauge.Value == 7 && normalGauge.MaxValue == 10 && !normalGauge.HasThemeStyleboxOverride("fill"), "Gauge replacement clears obsolete low-value fill override");
            var duplicates = new[] { rows[0], rows[0] with { Label = "Second duplicate" } };
            hud.Apply(Projection(duplicates));
            var duplicateGroup = Group();
            hud.Apply(Projection(duplicates.Select(row => row with { Hint = "New" }).ToArray()));
            Require(Group() != duplicateGroup && Content().GetChildCount() == 2, "Duplicate keys use complete original renderer without rejecting rows");
            hud.Apply(Projection(rows));
            Require(Content().GetChildCount() == 19, "Old duplicate keys also take full-render fallback");
            var actionRows = new[] { rows[0] with { Intent = new(stamp, TacticalHudIntentKind.Back) } };
            hud.Apply(Projection(actionRows));
            var actionGroup = Group();
            var button = Content().GetChildren().OfType<Button>().Single();
            hud.Apply(Projection(actionRows.Select(row => row with { Label = "New action" }).ToArray()));
            Require(Group() != actionGroup && Content().GetChildren().OfType<Button>().Single() != button, "Intent rows do not enter row reuse");
            hud.Apply(Projection(rows));
            var priorGroup = Group();
            stamp = new TacticalStamp(901, 2);
            hud.Apply(Projection(rows));
            Require(Group() != priorGroup, "Stamp change rebuilds");
            priorGroup = Group();
            hud.Apply(Projection(rows) with
            {
                Busy = true
            });
            Require(Group() != priorGroup, "Busy change rebuilds");
            priorGroup = Group();
            hud.ConfigureArt(TacticalHudArt.Load(assets, portraits));
            hud.Apply(Projection(rows) with
            {
                Busy = true
            });
            Require(Group() != priorGroup, "Art invalidation rebuilds");
            // Malformed native rows are defects, not grounds for silently rebuilding around corruption.
            var invalid = Content().GetChild(0);
            Content().RemoveChild(invalid);
            invalid.QueueFree();
            bool rejected = false;
            var priorProjection = hud.Projection;
            try
            {
                hud.Apply(Projection(rows.Select(row => row with { Hint = "Changed" }).ToArray()) with
                {
                    Busy = true
                });
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected && ReferenceEquals(hud.Projection, priorProjection), "Structural mismatch fails before advancing projection");
        }
        finally
        {
            RemoveChild(hud);
            hud.Free();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await CheckFormalUnitDetailsRetention();
    }
    private async Task CheckFormalUnitDetailsRetention()
    {
        foreach (var levelId in new[] { "c01-01", "c01-07" })
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == levelId);
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var originalState = EcmaJson.Stringify(session.Snapshot());
            var originalEvents = session.Events.Count;
            var screen = new BattleScreen();
            AddChild(screen);
            try
            {
                screen.Board.AnimationsEnabled = false;
                var scene = AuthoredScene.Load("res://data/scenes/" + levelId + ".json");
                var rendering = BoardSceneRendering.Load(scene, level, session.Snapshot()["map"]!.AsObject());
                screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                    new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
                screen.ConfigureGround(rendering.Ground);
                screen.ConfigureDynamicState();
                screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var actors = session.GetTacticalQueries(0).Actors().Take(2).ToArray();
                Require(actors.Length == 2, levelId + ": formal witness has two actionable actors");
                var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                TacticalHudSection Section() => hud.Projection!.Sections.Single(section => section.Key == "unit-details");
                void Select(int actorId)
                {
                    var actor = session.GetTacticalQueries(0).Unit(actorId)!;
                    screen.SelectHit(TacticalHit.Unit(actor.At, actor.Id));
                }
                Dictionary<string, (HBoxContainer Line, ProgressBar? Bar)> MountedRows(TacticalHudSection section)
                {
                    var group = (VBoxContainer)hud.FindChild("unit-details", true, false);
                    var content = group.GetNode<VBoxContainer>("Content");
                    var result = new Dictionary<string, (HBoxContainer Line, ProgressBar? Bar)>();
                    var index = 0;
                    foreach (var row in section.Rows)
                    {
                        var line = content.GetChild<HBoxContainer>(index++);
                        var label = line.GetChildren().OfType<Label>().Single();
                        Require(label.Text == row.Label && label.TooltipText == row.Hint,
                            levelId + ": mounted text, hint and order match the current projection");
                        ProgressBar? bar = null;
                        if (row.Gauge is { } gauge)
                        {
                            bar = content.GetChild<ProgressBar>(index++);
                            Require(bar.Value == gauge.Value && bar.MaxValue == gauge.Maximum,
                                levelId + ": mounted gauge matches the current projection");
                        }
                        result.Add(row.Key, (line, bar));
                    }
                    Require(index == content.GetChildCount(), levelId + ": no missing or surplus native row fragments");
                    return result;
                }
                Select(actors[0]);
                foreach (var actorId in new[] { actors[1], actors[0] })
                {
                    var previousSection = Section();
                    var previousFragments = MountedRows(previousSection);
                    Select(actorId);
                    var nextSection = Section();
                    var nextFragments = MountedRows(nextSection);
                    Require(previousSection.Rows.Count != nextSection.Rows.Count,
                        levelId + ": formal switch exercises different row counts");
                    var retained = nextSection.Rows.Where(row => previousSection.Rows.Any(previous =>
                        previous with
                        {
                            Label = row.Label,
                            Hint = row.Hint
                        } == row)).ToArray();
                    Require(retained.Any(row => previousSection.Rows.Contains(row)),
                        levelId + ": formal switch includes unchanged shared rows");
                    Require(retained.Any(row => !previousSection.Rows.Contains(row)),
                        levelId + ": formal switch includes text-only shared row changes");
                    foreach (var row in retained)
                        Require(previousFragments[row.Key] == nextFragments[row.Key],
                            levelId + ": shared read-only native fragments remain mounted across actor selection");
                    Require(originalState == EcmaJson.Stringify(session.Snapshot()) && session.Events.Count == originalEvents,
                        levelId + ": normal actor inspection does not mutate canonical battle state or events");
                }
                GD.Print("FORMAL_ROW_RETENTION_OK " + levelId);
            }
            finally
            {
                RemoveChild(screen);
                screen.Free();
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
