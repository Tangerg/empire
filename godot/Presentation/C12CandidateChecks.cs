#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Source registration and native picking checks; not a substitute for visual or campaign acceptance.</summary>
public partial class C12CandidateChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-12");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            await Check(level, engine.CreateState(level), engine, 19);
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                Require(argument.StartsWith("--branch-save=", StringComparison.Ordinal), "Use explicit --branch-save=/absolute/application.json");
                var path = argument["--branch-save=".Length..];
                Require(Path.IsPathFullyQualified(path), "Branch save must be absolute.");
                var campaign = new GrayFlagCampaign(
                    new CampaignApplicationRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese())),
                    PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json"), levels);
                campaign.Load(EcmaJson.Parse(File.ReadAllText(path)));
                Require(campaign.Battle is not null && campaign.BattleLevel()["id"]!.GetValue<string>() == "c01-12"
                    && campaign.Battle.ActionLog.Count == 0 && campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0,
                    "This check expects explicit detached C12 openings, not invented carry.");
                var state = campaign.Battle!.Snapshot();
                var cain = state["units"]!.AsArray().OfType<JsonObject>().Single(x => x["key"]!.GetValue<string>() == "imperial-cain");
                var joint = campaign.Snapshot()["flags"]!.AsArray().Any(x => x!.GetValue<string>() == "bell_tower_joint_control");
                Require(cain["type"]!.GetValue<string>() == "c01.cain" && cain["owner"]!.GetValue<int>() == (joint ? 1 : 2),
                    "Cain identity/allegiance must follow the authored branch.");
                await Check(campaign.BattleLevel(), state, engine, 19);
                GD.Print($"C12_BRANCH_OK joint={joint}; Cain owner={cain["owner"]}; no prior battles");
            }
            GD.Print("C12_CANDIDATE_CHECKS_OK: registered 345 cells, six site bodies, three dynamic controls and nineteen actors; unchanged canonical state. Not visual quality or campaign victory acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Check(JsonObject level, JsonObject state, BattleEngine engine, int actorCount)
    {
        var before = EcmaJson.Stringify(state);
        var registeredLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-12");
        var scene = AuthoredScene.Load("res://data/scenes/c01-12.json");
        // Registration hashes the authored source, not the roster-adjusted battle request.
        var rendering = BoardSceneRendering.Load(scene, registeredLevel, state["map"]!.AsObject());
        var rows = registeredLevel["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Require(rows.Length == 15 && rows.All(x => x.Length == 23), "C12 must retain its 345 canonical cells.");
        Require(rows.Sum(x => x.Count(c => c == '~')) == 66 && rows.Sum(x => x.Count(c => c == '=')) == 24
            && rows.Sum(x => x.Count(c => c == 'h')) == 6, "Canonical water, bridge and hill counts changed.");
        var water = engine.Catalog.Definition("terrains", "water")!.Value;
        var bridge = engine.Catalog.Definition("terrains", "bridge")!.Value;
        Require(water.GetProperty("cost").GetProperty("foot").ValueKind == System.Text.Json.JsonValueKind.Null
            && water.GetProperty("cost").GetProperty("flying").GetDouble() == 1
            && bridge.GetProperty("cost").GetProperty("foot").GetDouble() == 1, "Production water/bridge movement contract differs.");

        var battlefield = new MovementRules(new(engine.Catalog, engine.Grids)).Battlefield(state);
        for (var y = 0; y < 15; y++)
            for (var x = 0; x < 23; x++)
            {
                var cell = battlefield.Cell(new Coord(x, y));
                if (rows[y][x] == '~')
                    Require(!cell.Admits("foot") && !cell.Admits("mounted") && !cell.Admits("heavy") && cell.Admits("flying"),
                        "Real water cells must reject land movement and admit flight.");
                if (rows[y][x] == '=')
                    Require(cell.Admits("foot") && cell.MovementCost("foot") == 1, "Real bridge cells must retain land passage.");
                if (rows[y][x] == 'h')
                    Require(cell.MovementCost("foot") == 2 && cell.MovementCost("mounted") == 3, "Real hill costs differ from bridges.");
            }

        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.ConfigureDynamicState();
            board.RenderState(state);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var origin = board.Projection.CellContact(new Coord(0, 0));
            Require((board.Projection.CellContact(new Coord(1, 0)) - origin).DistanceTo(new Vector2(38.4f, 0)) < .001f
                && (board.Projection.CellContact(new Coord(0, 1)) - origin).DistanceTo(new Vector2(4.8f, 32.4f)) < .001f,
                "World cell registration must remain 38.4 x 32.4 with 4.8 shear.");
            for (var y = 0; y < 15; y++)
                for (var x = 0; x < 23; x++)
                {
                    var cell = new Coord(x, y);
                    Require(board.Projection.Pick(board.Projection.CellContact(cell))?.Cell == cell, $"Ground registration lost cell {cell}.");
                }
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            var expectedSites = rows.SelectMany((row, y) => row.Select((glyph, x) => (glyph, x, y)))
                .Where(x => x.glyph is 'v' or 'q').Select(x => new Coord(x.x, x.y)).ToHashSet();
            Require(view.Sites.Count == 6 && expectedSites.SetEquals(view.Sites.Select(x => x.Cell))
                && view.Sites.Select(x => x.Cell).Distinct().Count() == 6 && view.Sites.All(x => x.OwnsMapStructureBody),
                "Six canonical sites must each have one body owner.");
            foreach (var site in view.Sites)
            {
                var entry = view.DepthEntries.Single(x => x.SelectionCell == site.Cell);
                var sprite = (Sprite2D)entry.Node;
                var rect = sprite.GetRect();
                Require(Enumerable.Range(1, 19).Any(y => Enumerable.Range(1, 19).Any(x =>
                {
                    var point = sprite.ToGlobal(rect.Position + rect.Size * new Vector2(x / 20f, y / 20f));
                    return entry.Visual!.ContainsPointer(point) && board.Pick(board.ToLocal(point)) == TacticalHit.Object(new(site.Cell.X, site.Cell.Y));
                })), $"Site body cannot be picked: {site.Cell}.");
            }
            Require(board.RenderedUnitCount == actorCount && board.RenderedStructureCount == 3, "Canonical actors or structure ownership differ.");
            var actorFailures = new List<string>();
            foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
            {
                var id = unit["id"]!.GetValue<int>();
                var cell = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
                var sprite = board.GetNode<Sprite2D>($"CanonicalProjection/Unit_{id}/AuthoredSprite");
                var rect = sprite.GetRect();
                var opaque = 0;
                var selectable = 0;
                for (var y = 1; y < 20; y++)
                    for (var x = 1; x < 20; x++)
                    {
                        var local = rect.Position + rect.Size * new Vector2(x / 20f, y / 20f);
                        if (!sprite.IsPixelOpaque(local))
                            continue;
                        opaque++;
                        if (board.Pick(board.ToLocal(sprite.ToGlobal(local))) == TacticalHit.Unit(cell, id))
                            selectable++;
                    }
                GD.Print($"C12_ACTOR_BODY count={actorCount} key={unit["key"]}: {selectable}/{opaque} opaque samples selectable");
                if (opaque == 0 || selectable < opaque * .70f)
                    actorFailures.Add($"Actor body too occluded: {id}, {selectable}/{opaque}.");
            }
            foreach (var structure in state["structures"]!.AsArray().OfType<JsonObject>())
            {
                var id = structure["id"]!.GetValue<string>();
                var cell = new BoardCell(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
                var node = board.GetNode<Node2D>("CanonicalProjection/DynamicState").GetChildren().OfType<Node2D>()
                    .Single(x => x.HasMeta("structure_id") && x.GetMeta("structure_id").AsString() == id);
                var sprite = node.GetChildren().OfType<Sprite2D>().Single(x => x.Texture is AtlasTexture);
                var rect = sprite.GetRect();
                var opaque = 0;
                var selectable = 0;
                for (var y = 1; y < 40; y++)
                    for (var x = 1; x < 40; x++)
                    {
                        var local = rect.Position + rect.Size * new Vector2(x / 40f, y / 40f);
                        if (!sprite.IsPixelOpaque(local))
                            continue;
                        opaque++;
                        if (board.Pick(board.ToLocal(sprite.ToGlobal(local))) == TacticalHit.Object(cell))
                            selectable++;
                    }
                GD.Print($"C12_CONTROL_BODY {id}: {selectable}/{opaque} opaque samples selectable");
                Require(selectable > 0, $"Control edge cannot be picked independently: {id}.");
            }
            Require(actorFailures.Count == 0, string.Join(" | ", actorFailures));
            Require(EcmaJson.Stringify(state) == before, "Rendering and picking mutated canonical state.");
            // Synthetic view-state probes only. Never exported as a campaign save or described as played destruction.
            foreach (var (visual, hp, owner, disabled, frame) in new[]
            {
                ("normal", 180, 0, false, 0), ("damaged", 90, 2, false, 1),
                ("captured", 180, 1, false, 2), ("disabled", 180, 2, true, 1),
                ("destroyed", 0, 2, false, 1)
            })
            {
                var fixture = state.DeepClone().AsObject();
                foreach (var item in fixture["structures"]!.AsArray().OfType<JsonObject>())
                {
                    item["hp"] = hp;
                    item["owner"] = owner;
                    item["disabled"] = disabled;
                }
                var fixtureBefore = EcmaJson.Stringify(fixture);
                board.RenderState(fixture);
                var nodes = board.GetNode<Node2D>("CanonicalProjection/DynamicState").GetChildren().OfType<Node2D>()
                    .Where(n => n.HasMeta("structure_id")).ToArray();
                Require(nodes.Length == 3, "Display refresh must retain exactly three dynamic structure owners.");
                foreach (var node in nodes)
                {
                    Require(node.GetMeta("visual_state").AsString() == visual && node.GetMeta("hp").AsDouble() == hp
                        && node.GetMeta("owner").AsInt32() == owner && !node.GetMeta("rule_derived").AsBool(),
                        $"Canonical structure metadata not synchronized for {visual}.");
                    var sprite = node.GetChildren().OfType<Sprite2D>().Single(x => x.Texture is AtlasTexture);
                    Require(sprite.Texture is AtlasTexture atlas && atlas.Region.Position.X == frame * 724
                        && atlas.Region.Size == new Vector2(724, 724), $"Wrong authored frame for logical state {visual}.");
                    var active = visual is not ("disabled" or "destroyed");
                    Require((node.GetNodeOrNull("HealthGauge") is not null) == active,
                        $"Health decoration lifetime differs for {visual}.");
                }
                Require(EcmaJson.Stringify(fixture) == fixtureBefore, "Synthetic rendering mutated its canonical input.");
                GD.Print($"C12_SYNTHETIC_VISUAL_STATE_OK {visual}; frame={frame}; three canonical owners; not real gameplay");
            }
            Require(EcmaJson.Stringify(state) == before, "Synthetic probes mutated original opening.");

        }
        finally
        {
            board.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
