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
public partial class C15CandidateChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-15");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            await Check(level, engine.CreateState(level), engine, 22);
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                Require(argument.StartsWith("--branch-save=", StringComparison.Ordinal), "Use explicit --branch-save=/absolute/application.json");
                var path = argument["--branch-save=".Length..];
                Require(Path.IsPathFullyQualified(path), "Branch save must be absolute.");
                var campaign = new GrayFlagCampaign(
                    new CampaignApplicationRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese())),
                    PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json"), levels);
                campaign.Load(EcmaJson.Parse(File.ReadAllText(path)));
                Require(campaign.Battle is not null && campaign.BattleLevel()["id"]!.GetValue<string>() == "c01-15"
                    && campaign.Battle.ActionLog.Count == 0 && campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0,
                    "This check expects explicit detached C15 openings, not invented carry.");
                var state = campaign.Battle!.Snapshot();
                var druid = state["units"]!.AsArray().OfType<JsonObject>().Single(x => x["key"]!.GetValue<string>() == "controlled-druid");
                var gradual = campaign.Snapshot()["flags"]!.AsArray().Any(x => x!.GetValue<string>() == "silverwood_compromise");
                Require(druid["type"]!.GetValue<string>() == "c01.druid" && druid["owner"]!.GetValue<int>() == (gradual ? 3 : 2),
                    "Druid identity/allegiance must follow the authored branch, never become player-owned.");
                await Check(campaign.BattleLevel(), state, engine, 22);
                GD.Print($"C15_BRANCH_OK gradual={gradual}; druid owner={druid["owner"]}; detached, no prior battles");
            }
            GD.Print("C15_CANDIDATE_CHECKS_OK: registered 384 cells, eight site bodies, one dynamic root and twenty-two actors; unchanged canonical state. Not visual quality or campaign victory acceptance.");
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
            .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-15");
        var scene = AuthoredScene.Load("res://data/scenes/c01-15.json");
        // Registration hashes the authored source, not the roster-adjusted battle request.
        var rendering = BoardSceneRendering.Load(scene, registeredLevel, state["map"]!.AsObject());
        var rows = registeredLevel["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Require(rows.Length == 16 && rows.All(x => x.Length == 24), "C15 must retain its 384 canonical cells.");
        Require(rows.Sum(x => x.Count(c => c == 'T')) == 185 && rows.Sum(x => x.Count(c => c == 'R')) == 179
            && rows.Sum(x => x.Count(c => c == 'h')) == 12, "Canonical forest, root and hill counts changed.");
        var document = PresentationJson.Load<AssembledGroundDocument>("res://data/scenes/c01-15-assembled-ground.json");
        document.ValidateLevel(registeredLevel);
        Require(document.Objects.Count(x => x.OwnsMapStructureBody) == 8
            && PresentationJson.Load<JsonObject>("res://data/scenes/c01-15-assembled-ground.json")["objects"]!.AsArray()
                .OfType<JsonObject>().All(x => x["selectionCell"] is not JsonArray cell
                    || cell[0]!.GetValue<int>() != 11 || cell[1]!.GetValue<int>() != 7),
            "Ground cannot create a second selectable/static owner for the central mother-root.");
        var wrongSource = registeredLevel.DeepClone().AsObject();
        wrongSource["terrain"]![0] = "h" + rows[0][1..];
        var rejected = false;
        try
        {
            document.ValidateLevel(wrongSource);
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Registration must reject a changed canonical source.");
        var roots = state["structures"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(roots.Length == 1 && roots[0]["id"]!.GetValue<string>() == "mother-root"
            && roots[0]["type"]!.GetValue<string>() == "c01.mother-root" && roots[0]["owner"]!.GetValue<int>() == 3
            && roots[0]["hp"]!.GetValue<double>() == 500, "Opening must retain the single canonical allied root.");
        var ally = level["players"]!.AsArray().OfType<JsonObject>().Single(x => x["id"]!.GetValue<int>() == 3);
        Require(ally["team"]!.GetValue<int>() == 1 && ally["controller"]!.GetValue<string>() == "ai",
            "Allied owner 3 is not a player-controlled unit owner.");
        var battlefield = new MovementRules(new(engine.Catalog, engine.Grids)).Battlefield(state);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 24; x++)
            {
                var cell = battlefield.Cell(new Coord(x, y));
                if (rows[y][x] == 'T')
                    Require(cell.BlocksVision && cell.MovementCost("foot") == 2 && cell.MovementCost("mounted") == 3,
                        "Real forest must retain opacity and movement costs.");
                if (rows[y][x] == 'R')
                    Require(cell.BlocksVision && cell.MovementCost("foot") == 2 && !cell.Admits("mounted") && !cell.Admits("heavy"),
                        "Real root terrain must retain opacity and mounted/heavy restrictions.");
                if (rows[y][x] == 'h')
                    Require(new[] { 3, 7, 11 }.Contains(y) && new[] { 5, 6, 17, 18 }.Contains(x)
                        && cell.MovementCost("foot") == 2 && cell.MovementCost("mounted") == 3,
                        "Canonical twelve hill positions/costs changed.");
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
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 24; x++)
                {
                    var cell = new Coord(x, y);
                    Require(board.Projection.Pick(board.Projection.CellContact(cell))?.Cell == cell, $"Ground registration lost cell {cell}.");
                }
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            var expectedSites = rows.SelectMany((row, y) => row.Select((glyph, x) => (glyph, x, y)))
                .Where(x => x.glyph is 'v' or 'q').Select(x => new Coord(x.x, x.y)).ToHashSet();
            Require(view.Sites.Count == 8 && expectedSites.SetEquals(view.Sites.Select(x => x.Cell))
                && view.Sites.Select(x => x.Cell).Distinct().Count() == 8 && view.Sites.All(x => x.OwnsMapStructureBody),
                "Eight canonical sites must each have one body owner.");
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
            Require(board.RenderedUnitCount == actorCount && board.RenderedStructureCount == 1, "Canonical actors or structure ownership differ.");
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
                GD.Print($"C15_ACTOR_BODY count={actorCount} key={unit["key"]}: {selectable}/{opaque} opaque samples selectable");
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
                GD.Print($"C15_CONTROL_BODY {id}: {selectable}/{opaque} opaque samples selectable");
                Require(selectable > 0, $"Control edge cannot be picked independently: {id}.");
            }
            Require(actorFailures.Count == 0, string.Join(" | ", actorFailures));
            Require(EcmaJson.Stringify(state) == before, "Rendering and picking mutated canonical state.");
            // Synthetic HP view probes, not legal played damage or a campaign save.
            foreach (var hp in new[] { 500, 250, 1 })
            {
                var fixture = state.DeepClone().AsObject();
                fixture["structures"]![0]!["hp"] = hp;
                var fixtureBefore = EcmaJson.Stringify(fixture);
                board.RenderState(fixture);
                var nodes = board.GetNode<Node2D>("CanonicalProjection/DynamicState").GetChildren().OfType<Node2D>()
                    .Where(n => n.HasMeta("structure_id")).ToArray();
                Require(nodes.Length == 1 && nodes[0].GetMeta("structure_id").AsString() == "mother-root"
                    && nodes[0].GetMeta("hp").AsDouble() == hp && nodes[0].GetMeta("owner").AsInt32() == 3
                    && !nodes[0].GetMeta("rule_derived").AsBool(), "HP view must preserve the single canonical mother-root owner.");
                Require(nodes[0].GetNodeOrNull("HealthGauge") is not null, "Living mother-root needs its canonical health display.");
                Require(PickRoot(board, nodes[0], roots[0]) > 0, $"Root no longer independently selectable at HP {hp}.");
                Require(EcmaJson.Stringify(fixture) == fixtureBefore, "Synthetic rendering mutated its input.");
                GD.Print($"C15_SYNTHETIC_HP_OK hp={hp}; one canonical owner; not real gameplay");
            }
            Require(EcmaJson.Stringify(state) == before, "Synthetic probes mutated original opening.");

        }
        finally
        {
            board.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static int PickRoot(BoardView board, Node2D node, JsonObject structure)
    {
        var sprite = node.GetChildren().OfType<Sprite2D>().Single(x => x.Texture is AtlasTexture);
        var rect = sprite.GetRect();
        var cell = new BoardCell(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
        var hits = 0;
        for (var y = 1; y < 40; y++)
            for (var x = 1; x < 40; x++)
            {
                var local = rect.Position + rect.Size * new Vector2(x / 40f, y / 40f);
                if (sprite.IsPixelOpaque(local) && board.Pick(board.ToLocal(sprite.ToGlobal(local))) == TacticalHit.Object(cell))
                    hits++;
            }
        return hits;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
