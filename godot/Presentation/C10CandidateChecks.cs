#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Source registration and native picking checks; not a substitute for visual or campaign acceptance.</summary>
public partial class C10CandidateChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-10");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            await Check(level, engine.CreateState(level), engine, 21);
            var arguments = OS.GetCmdlineUserArgs();
            if (arguments.Length > 1 || arguments.Length == 1 && !arguments[0].StartsWith("--carried-save=", StringComparison.Ordinal))
                throw new InvalidOperationException("Optional argument: --carried-save=/absolute/application.json");
            if (arguments.Length == 1)
            {
                var path = arguments[0]["--carried-save=".Length..];
                Require(Path.IsPathFullyQualified(path), "Carried save requires an explicit absolute path.");
                var campaign = new GrayFlagCampaign(
                    new CampaignApplicationRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese())),
                    PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json"), levels);
                campaign.Load(EcmaJson.Parse(File.ReadAllText(path)));
                Require(campaign.Battle is not null && campaign.BattleLevel()["id"]!.GetValue<string>() == "c01-10",
                    "The supplied production save must contain an active C10 battle.");
                var state = campaign.Battle!.Snapshot();
                Require(state["turn"]!.GetValue<int>() == 1 && campaign.Battle.ActionLog.Count == 0,
                    "Carried acceptance requires an unplayed opening, not a modified mid-battle sample.");
                var expected = level["units"]!.AsArray().OfType<JsonObject>().Select(x => x["key"]!.GetValue<string>()).ToHashSet();
                var actual = state["units"]!.AsArray().OfType<JsonObject>().Select(x => x["key"]!.GetValue<string>()).ToHashSet();
                Require(expected.Except(actual).SequenceEqual(new[] { "campaign-torren" }) && !actual.Except(expected).Any(),
                    "The carried opening must retain every authored identity except fallen Torren.");
                await Check(campaign.BattleLevel(), state, engine, 20);
            }
            GD.Print("C10_CANDIDATE_CHECKS_OK: registered 350 cells, nine site bodies, thirteen forest cells, actor picking and six exit cells; unchanged canonical state. Not visual quality or campaign victory acceptance.");
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
            .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-10");
        var scene = AuthoredScene.Load("res://data/scenes/c01-10.json");
        var ground = PresentationJson.Load<JsonObject>("res://data/scenes/c01-10-candidate-ground.json");
        // Registration hashes the authored source, not the roster-adjusted battle request.
        var rendering = BoardSceneRendering.Load(scene, registeredLevel, state["map"]!.AsObject());
        var rows = registeredLevel["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Require(rows.Length == 14 && rows.All(x => x.Length == 25), "C10 must retain its 350 canonical cells.");
        var objects = ground["objects"]!.AsArray().OfType<JsonObject>().ToArray();
        var forestCells = rows.SelectMany((row, y) => row.Select((glyph, x) => (glyph, x, y)))
            .Where(x => x.glyph == 'T').Select(x => new Coord(x.x, x.y)).ToHashSet();
        Require(forestCells.Count == 13, "Expected thirteen canonical forest cells.");
        var forests = objects.Where(x => x["id"]!.GetValue<string>().StartsWith("Forest-", StringComparison.Ordinal)).ToArray();
        Require(forestCells.SetEquals(forests.Select(ContactCell)), "Forest art must register exactly to canonical forest cells.");
        foreach (var forest in forests)
            Require(forest["selectionCell"] is null && !forest["ownsMapStructureBody"]!.GetValue<bool>()
                && forest["pointer"]!.GetValue<string>() == "pass-through", "Forest clusters must not impersonate interactive site bodies.");
        Require(!objects.Any(x => x["id"]!.GetValue<string>().StartsWith("Hill-", StringComparison.Ordinal)
            || x["id"]!.GetValue<string>().StartsWith("Mountain-", StringComparison.Ordinal)
            || x["assetId"]!.GetValue<string>() is "low-grassy-knoll" or "rocky-mountain"),
            "Continuous ground terrain must not acquire duplicate standalone hill/mountain bodies.");

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
                && (board.Projection.CellContact(new Coord(0, 1)) - origin).DistanceTo(new Vector2(4.8f, 32)) < .001f,
                "World cell registration must remain 38.4 x 32 with 4.8 shear.");
            for (var y = 0; y < 14; y++)
                for (var x = 0; x < 25; x++)
                {
                    var cell = new Coord(x, y);
                    Require(board.Projection.Pick(board.Projection.CellContact(cell))?.Cell == cell, $"Ground registration lost cell {cell}.");
                }
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            var expectedSites = rows.SelectMany((row, y) => row.Select((glyph, x) => (glyph, x, y)))
                .Where(x => x.glyph is 'v' or 'q').Select(x => new Coord(x.x, x.y)).ToHashSet();
            Require(view.Sites.Count == 9 && expectedSites.SetEquals(view.Sites.Select(x => x.Cell))
                && view.Sites.Select(x => x.Cell).Distinct().Count() == 9 && view.Sites.All(x => x.OwnsMapStructureBody),
                "Nine canonical sites must each have one body owner.");
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
            Require(board.RenderedUnitCount == actorCount && board.RenderedStructureCount == 0, "Canonical actors or structure ownership differ.");
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
                GD.Print($"C10_ACTOR_BODY count={actorCount} key={unit["key"]}: {selectable}/{opaque} opaque samples selectable");
                Require(opaque > 0 && selectable >= opaque * .70f, $"Actor body too occluded: {id}, {selectable}/{opaque}.");
            }
            var exit = registeredLevel["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "east-pass")["cells"]!.AsArray();
            Require(exit.Count == 6, "Exit must retain all six authored cells.");
            foreach (var value in exit)
            {
                var cell = new Coord(value!["x"]!.GetValue<int>(), value["y"]!.GetValue<int>());
                Require(board.Pick(board.Projection.CellContact(cell)) == TacticalHit.Ground(new(cell.X, cell.Y)), $"Exit cell is obstructed in picking: {cell}.");
            }
            Require(EcmaJson.Stringify(state) == before, "Rendering and picking mutated canonical state.");
        }
        finally
        {
            board.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static Coord ContactCell(JsonObject item)
    {
        var contact = item["contact"]!.AsArray();
        return new Coord((int)Math.Floor(contact[0]!.GetValue<double>()), (int)Math.Floor(contact[1]!.GetValue<double>()));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
