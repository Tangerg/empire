#nullable enable
using System;
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

/// <summary>Native registration and picking contracts, not visual acceptance or a played valve victory.</summary>
public partial class ForgeDistrictChecks : Node
{
    private int assertions;

    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            await Check();
            GD.Print($"FORGE_DISTRICT_CHECKS_OK {assertions} assertions; canonical C14 registration, ownership and picking; not visual or campaign acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Check()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-14");
        const string levelHash = "2c84925ca44a6e16b4bef255c1099de629850351bdda62e970a08ce08e354a26";
        Require(EcmaJson.Sha256(level) == levelHash,
            "C14 terrain, roster, objectives, after-action valve triggers and victory alternatives must remain canonical.");
        var rows = level["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Require(rows.Length == 16 && rows.All(row => row.Length == 22), "C14 retains all 352 cells.");
        var cells = rows.SelectMany((row, y) => row.Select((glyph, x) => new Coord(x, y))).ToArray();
        var sites = cells.Where(c => "vBq".Contains(rows[c.Y][c.X])).ToHashSet();
        var wallCells = cells.Where(c => rows[c.Y][c.X] == '#').ToArray();
        Require(sites.Count == 8 && rows.Sum(row => row.Count(c => c == 'v')) == 4
            && rows.Sum(row => row.Count(c => c == 'q')) == 4 && wallCells.Length == 20
            && rows.Sum(row => row.Count(c => c == 'f')) == 252
            && rows.Sum(row => row.Count(c => c == 'm')) == 56
            && rows.Sum(row => row.Count(c => c == '=')) == 16, "Canonical forge terrain and site counts changed.");
        var zones = level["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().ToArray();
        var targetCells = zones.SelectMany(z => z["cells"]!.AsArray().OfType<JsonObject>())
            .Select(c => new Coord(c["x"]!.GetValue<int>(), c["y"]!.GetValue<int>())).ToArray();
        Require(zones.Length == 2 && targetCells.Length == 4 && targetCells.Distinct().Count() == 4
            && targetCells.All(c => rows[c.Y][c.X] == '='), "Both valve zones must remain four distinct canonical bridge cells.");
        var scene = AuthoredScene.Load("res://data/scenes/c01-14.json");
        Require(scene.AssembledGroundPath == "res://data/scenes/c01-14-assembled-ground.json" && scene.PaintedGroundPath is null,
            "C14 uses registered ground with independent site and wall bodies.");
        var document = PresentationJson.Load<AssembledGroundDocument>(scene.AssembledGroundPath!);
        document.ValidateLevel(level);
        Require(document.ObjectCatalogPath == "res://data/scenes/c01-14-objects.json", "C14 must use its reviewed independent object catalog.");
        var decorations = document.Objects.Where(o => !o.OwnsMapStructureBody).ToArray();
        Require(decorations.All(o => o.Pointer == "pass-through" && o.SelectionCell is null),
            "Wall and decorative art cannot own capture, movement or pointer semantics.");
        var valves = document.Objects.Where(o => o.Id.StartsWith("Valve-", StringComparison.Ordinal)).ToArray();
        Require(valves.Length == 2 && valves.All(o => !o.OwnsMapStructureBody && o.Pointer == "pass-through" && o.SelectionCell is null),
            "Two valve landmarks are static scenery, never interactive targets or canonical state owners.");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        CheckTerrainMovement(engine, level, rows, cells);
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.RenderState(state);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            Require(board.SurfaceReady && board.RenderingIssueCount == 0, "C14 must mount a complete registered surface without rendering issues.");
            Require(view.Sites.Count == 8 && view.Sites.Select(s => s.Cell).Distinct().Count() == 8
                && sites.SetEquals(view.Sites.Select(s => s.Cell)) && view.Sites.All(s => s.OwnsMapStructureBody),
                "Every canonical site must have exactly one authored body owner.");
            Require(board.RenderedUnitCount == 22 && board.RenderedStructureCount == 0,
                "All 22 canonical actors remain rendered without duplicate generic site bodies.");
            foreach (var cell in cells)
                Require(board.Projection.Pick(board.Projection.CellContact(cell))?.Cell == cell, $"Ground registration lost {cell}.");
            var walls = view.DepthEntries.Where(e => e.Node.Name.ToString().StartsWith("Wall-", StringComparison.Ordinal)).ToArray();
            Require(walls.Length > 0, "Canonical barriers require independent visible wall art.");
            foreach (var cell in wallCells)
                Require(walls.Any(w => w.Visual!.Covers(board.ToGlobal(board.Projection.CellCenter(cell)))),
                    $"Canonical blocked cell {cell} has no visible wall material.");
            foreach (var cell in targetCells)
            {
                var point = board.Projection.CellCenter(cell);
                var hit = board.Pick(point);
                Require(hit == TacticalHit.Ground(new(cell.X, cell.Y)),
                    $"Valve bridge {cell} must select its own canonical cell, not neighboring art.");
                Require(walls.All(w => !w.Visual!.Covers(board.ToGlobal(point))), $"Valve bridge {cell} must not look blocked by wall art.");
            }
            foreach (var site in sites)
            {
                var entry = view.DepthEntries.Single(e => e.SelectionCell == site);
                var sprite = (Sprite2D)entry.Node;
                var rect = sprite.GetRect();
                Require(Enumerable.Range(1, 19).Any(y => Enumerable.Range(1, 19).Any(x =>
                {
                    var point = sprite.ToGlobal(rect.Position + rect.Size * new Vector2(x / 20f, y / 20f));
                    return entry.Visual!.ContainsPointer(point) && board.Pick(board.ToLocal(point)) == TacticalHit.Object(new(site.X, site.Y));
                })), $"Site {site} has no visible clickable facade after all layers and actors mount.");
            }
            CheckActorPicking(board, state);
            Require(EcmaJson.Stringify(state) == before && EcmaJson.Sha256(level) == levelHash,
                "Registration, rendering and picking cannot advance canonical valve or battle state.");
        }
        finally
        {
            board.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void CheckTerrainMovement(BattleEngine engine, JsonObject level, string[] rows, Coord[] cells)
    {
        var battlefield = new MovementRules(new(engine.Catalog, engine.Grids)).Battlefield(engine.CreateState(level));
        var modes = new[] { "foot", "mounted", "heavy", "flying", "naval", "amphibious" };
        foreach (var position in cells)
        {
            // These are canonical movement contracts, not interpretations of the material's color.
            double?[]? expected = rows[position.Y][position.X] switch
            {
                'f' => [1, 2, 1, 1, null, 1],
                'm' => [null, null, null, 1, null, null],
                '=' => [1, 1, 1, 1, null, 1],
                '#' => [null, null, null, null, null, null],
                _ => null
            };
            if (expected is null)
                continue;
            var cell = battlefield.Cell(position);
            for (var index = 0; index < modes.Length; index++)
            {
                var mode = modes[index];
                Require(cell.Admits(mode) == expected[index].HasValue,
                    $"Terrain {rows[position.Y][position.X]} at {position} has wrong admission for {mode}.");
                if (expected[index] is { } cost)
                    Require(cell.MovementCost(mode) == cost, $"Terrain {position} has wrong {mode} cost.");
            }
        }
        var session = new GrayFlagSession(new BattleSessionRules(engine,
            BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()), new JsonArray()), level);
        var queries = session.GetTacticalQueries(0);
        var actors = queries.Actors().ToArray();
        Require(actors.Length > 0, "Movement checks need selectable opening actors.");
        foreach (var actor in actors)
        {
            var stops = queries.MoveStops(actor);
            foreach (var wall in cells.Where(c => rows[c.Y][c.X] == '#'))
                Require(!stops.Contains(new BoardCell(wall.X, wall.Y)), $"Wall {wall} cannot be a movement stop for {actor}.");
        }
    }

    private void CheckActorPicking(BoardView board, JsonObject state)
    {
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
            GD.Print($"C14_ACTOR_BODY key={unit["key"]}: {selectable}/{opaque} opaque samples selectable");
            Require(opaque > 0 && selectable >= opaque * .70f, $"Actor {id} body too occluded: {selectable}/{opaque}.");
        }
    }
}
