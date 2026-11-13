#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Native registration and picking contracts, not visual acceptance or a played true-name victory.</summary>
public partial class MemorialDistrictChecks : Node
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
            GD.Print($"MEMORIAL_DISTRICT_CHECKS_OK {assertions} assertions; canonical C16 registration, ownership and picking; not visual or campaign acceptance.");
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
            .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-16");
        const string levelHash = "897626d8df6f3303d8c84c06fc4caf793aeb61c017f160f081a5799003bacd23";
        Require(EcmaJson.Sha256(level) == levelHash,
            "C16 terrain, roster, objectives, after-action true-name triggers and victory alternatives must remain canonical.");
        var rows = level["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Require(rows.Length == 17 && rows.All(row => row.Length == 25), "C16 retains all 425 cells.");
        var cells = rows.SelectMany((row, y) => row.Select((glyph, x) => new Coord(x, y))).ToArray();
        var sites = cells.Where(c => rows[c.Y][c.X] == 'q').ToHashSet();
        Require(sites.Count == 9 && rows.Sum(row => row.Count(c => c == 'g')) == 296
            && rows.Sum(row => row.Count(c => c == 'o')) == 95
            && rows.Sum(row => row.Count(c => c == 'T')) == 25, "Canonical memorial terrain and site counts changed.");
        Require(level["rules"]!["turnLimit"]!.GetValue<int>() == 30, "C16 must retain its 30-round deadline.");
        var zones = level["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().ToArray();
        var targetCells = zones.SelectMany(z => z["cells"]!.AsArray().OfType<JsonObject>())
            .Select(c => new Coord(c["x"]!.GetValue<int>(), c["y"]!.GetValue<int>())).ToArray();
        Require(zones.Length == 3 && targetCells.Length == 3
            && targetCells.ToHashSet().SetEquals(new[] { new Coord(6, 3), new Coord(16, 8), new Coord(8, 13) }),
            "Three true-name sites must retain their canonical coordinates.");
        var primary = level["players"]!.AsArray().OfType<JsonObject>().Single(p => p["id"]!.GetValue<int>() == 1)["objectives"]![0]!;
        Require(primary["type"]!.GetValue<string>() == "failOn"
            && primary["condition"]!["selector"]!["keys"]!.AsArray().Select(k => k!.GetValue<string>()).SequenceEqual(new[] { "campaign-laiya" }),
            "Laiya's survival remains the critical objective guard.");
        var eliminate = primary["objective"]!["objectives"]!.AsArray().OfType<JsonObject>()
            .Single(o => o["type"]!.GetValue<string>() == "eliminate");
        Require(eliminate["selector"]!["keys"]!.AsArray().Select(k => k!.GetValue<string>()).SequenceEqual(new[] { "memory-inquisitor" }),
            "The elimination objective selects the inquisitor, not the unflagged colossus sharing its owner.");
        var scene = AuthoredScene.Load("res://data/scenes/c01-16.json");
        Require(scene.AssembledGroundPath == "res://data/scenes/c01-16-assembled-ground.json" && scene.PaintedGroundPath is null,
            "C16 uses registered ground with independent site bodies and low true-name landmarks.");
        var document = PresentationJson.Load<AssembledGroundDocument>(scene.AssembledGroundPath!);
        document.ValidateLevel(level);
        Require(document.ObjectCatalogPath == "res://data/scenes/c01-16-objects.json", "C16 must use its reviewed independent object catalog.");
        var decorations = document.Objects.Where(o => !o.OwnsMapStructureBody).ToArray();
        Require(decorations.All(o => o.Pointer == "pass-through" && o.SelectionCell is null),
            "True-name and decorative art cannot own capture, movement or pointer semantics.");
        var markers = document.Objects.Where(o => o.Id.StartsWith("TrueName-", StringComparison.Ordinal)).ToArray();
        Require(markers.Length == 3
            && markers.Select(o => o.Id).ToHashSet().SetEquals(targetCells.Select(c => $"TrueName-{c.X}-{c.Y}"))
            && markers.All(o => !o.OwnsMapStructureBody && o.Pointer == "pass-through" && o.SelectionCell is null),
            "Three true-name landmarks are static scenery, never interactive targets or canonical state owners.");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        CheckTerrainMovement(engine, level, rows, cells);
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        foreach (var (key, x, y) in new[] { ("unflagged", 12, 8), ("memory-inquisitor", 22, 8) })
        {
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == key);
            Require(unit["owner"]!.GetValue<int>() == 2 && unit["x"]!.GetValue<int>() == x && unit["y"]!.GetValue<int>() == y,
                $"Canonical identity and allegiance must remain distinct for {key}.");
        }
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
            Require(board.SurfaceReady && board.RenderingIssueCount == 0, "C16 must mount a complete registered surface without rendering issues.");
            Require(view.Sites.Count == 9 && view.Sites.Select(s => s.Cell).Distinct().Count() == 9
                && sites.SetEquals(view.Sites.Select(s => s.Cell)) && view.Sites.All(s => s.OwnsMapStructureBody),
                "Every canonical site must have exactly one authored body owner.");
            Require(board.RenderedUnitCount == 25 && board.RenderedStructureCount == 0,
                "All 25 canonical actors remain rendered without duplicate generic site bodies.");
            foreach (var cell in cells)
                Require(board.Projection.Pick(board.Projection.CellContact(cell))?.Cell == cell, $"Ground registration lost {cell}.");
            foreach (var cell in targetCells)
            {
                var point = board.Projection.CellCenter(cell);
                var hit = board.Pick(point);
                Require(hit == TacticalHit.Ground(new(cell.X, cell.Y)),
                    $"True-name site {cell} must select its own canonical cell, not neighboring art.");
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
                "Registration, rendering and picking cannot advance canonical true-name or battle state.");
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
                'g' or 'T' => [2, 3, 3, 1, null, 2],
                'o' => [2, 2, 2, 1, null, 2],
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
            GD.Print($"C16_ACTOR_BODY key={unit["key"]}: {selectable}/{opaque} opaque samples selectable");
            Require(opaque > 0 && selectable >= opaque * .70f, $"Actor {id} body too occluded: {selectable}/{opaque}.");
        }
    }
}
