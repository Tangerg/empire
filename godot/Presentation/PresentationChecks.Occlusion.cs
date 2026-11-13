#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class PresentationChecks
{
    private async Task CheckIdleOcclusionCache()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-10");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var state = engine.CreateState(level);
        var original = state.ToJsonString();
        var scene = AuthoredScene.Load("res://tests/native/c10-occlusion-scene.json");
        var assets = AssetCatalog.Load();
        var environment = EnvironmentCatalog.Load();
        var liveParent = new Node2D();
        var oracleParent = new Node2D();
        AddChild(liveParent);
        AddChild(oracleParent);
        var live = new BoardView { AdvanceAnimationsAutomatically = false };
        var oracle = new BoardView { AdvanceAnimationsAutomatically = false };
        liveParent.AddChild(live);
        oracleParent.AddChild(oracle);
        void Configure(BoardView board)
        {
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
        }
        void Compare(string context)
        {
            var actualLayer = live.GetNode<Node2D>("CanonicalProjection/OccludedUnitCues");
            var expectedLayer = oracle.GetNode<Node2D>("CanonicalProjection/OccludedUnitCues");
            var actual = actualLayer.GetChildren().OfType<Polygon2D>().ToArray();
            var expected = expectedLayer.GetChildren().OfType<Polygon2D>().ToArray();
            Require(actual.Select(node => node.Name).SequenceEqual(expected.Select(node => node.Name)),
                $"{context}: cached and fresh cues must have identical identities and order.");
            foreach (var pair in actual.Zip(expected))
            {
                Require(pair.First.Position == pair.Second.Position && pair.First.Color == pair.Second.Color
                    && pair.First.Polygon.SequenceEqual(pair.Second.Polygon),
                    $"{context}: {pair.First.Name} placement, owner color and shape must match full reconstruction.");
                var liveHit = live.Pick(live.ToLocal(pair.First.ToGlobal(Vector2.Zero)));
                var oracleHit = oracle.Pick(oracle.ToLocal(pair.Second.ToGlobal(Vector2.Zero)));
                Require(liveHit == oracleHit && liveHit?.Kind == TacticalHitKind.Unit,
                    $"{context}: cue centers must still identify the same unit.");
            }
            var actualLeaders = actualLayer.GetChildren().OfType<Line2D>().ToArray();
            var expectedLeaders = expectedLayer.GetChildren().OfType<Line2D>().ToArray();
            Require(actualLeaders.Select(node => node.Name).SequenceEqual(expectedLeaders.Select(node => node.Name)),
                $"{context}: leader identities must match full reconstruction.");
            foreach (var pair in actualLeaders.Zip(expectedLeaders))
                Require(pair.First.Points.SequenceEqual(pair.Second.Points)
                    && pair.First.DefaultColor == pair.Second.DefaultColor,
                    $"{context}: leader points and colors must match full reconstruction.");
            Require(live.Diagnostics.SequenceEqual(oracle.Diagnostics),
                $"{context}: cached diagnostics must match fresh diagnostics without accumulating duplicates.");
            foreach (var point in new[] { new Vector2(300, 300), new Vector2(600, 400), new Vector2(900, 500), new Vector2(700, 650) })
                Require(live.Pick(point) == oracle.Pick(point),
                    $"{context}: original terrain hit at {point} must remain unchanged.");
        }
        async Task CompareProjection(JsonObject snapshot, IReadOnlySet<int>? visible, string context)
        {
            var before = snapshot.ToJsonString();
            live.RenderState(snapshot, visible);
            for (var step = 0; step < 16; step++)
            {
                live.AdvancePresentation(.125);
                oracle.RenderState(snapshot, visible);
                oracle.AdvancePresentation((step + 1) * .125);
                oracle.RefreshOcclusionCues();
                Compare($"{context}, step {step}");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Require(snapshot.ToJsonString() == before, $"{context}: cue projection must not mutate its snapshot.");
        }
        try
        {
            Configure(live);
            Configure(oracle);
            live.RenderState(state);
            for (var step = 0; step < 128; step++)
            {
                live.AdvancePresentation(.125);
                oracle.RenderState(state);
                oracle.AdvancePresentation((step + 1) * .125);
                oracle.RefreshOcclusionCues();
                Compare($"Repeated C10 idle pose {step}");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            // Change only the parent transform: the live projection and its pose history survive.
            liveParent.Transform = oracleParent.Transform = new Transform2D(.17f, new Vector2(1.3f, .75f), .08f, new Vector2(37, -19));
            live.AdvancePresentation(.125);
            oracle.RenderState(state);
            oracle.AdvancePresentation(129 * .125);
            oracle.RefreshOcclusionCues();
            Compare("Parent non-uniform transform without RenderState");
            var units = state["units"]!.AsArray().OfType<JsonObject>().ToArray();
            var laiya = units.Single(unit => unit["key"]!.GetValue<string>() == "campaign-laiya");
            var laiyaId = laiya["id"]!.GetValue<int>();
            var visible = units.Select(unit => unit["id"]!.GetValue<int>()).Where(id => id != laiyaId).ToHashSet();
            await CompareProjection(state, visible, "Fog filter replacement");
            Require(live.GetNodeOrNull<Polygon2D>($"CanonicalProjection/OccludedUnitCues/UnitCue_{laiyaId}") is null,
                "A newly hidden actor must not retain a cached cue.");
            var changedOwner = state.DeepClone().AsObject();
            changedOwner["units"]!.AsArray().OfType<JsonObject>()
                .Single(unit => unit["id"]!.GetValue<int>() == laiyaId)["owner"] = 2;
            await CompareProjection(changedOwner, null, "Owner replacement");
            var movedBattle = engine.CreateBattle(level);
            var beforeMove = movedBattle.Snapshot();
            var movingUnit = beforeMove["units"]!.AsArray().OfType<JsonObject>()
                .Single(unit => unit["key"]!.GetValue<string>() == "campaign-laiya");
            var movement = new MovementRules(new(engine.Catalog, engine.Grids));
            var field = movement.ComputeMoveField(beforeMove, movingUnit);
            var destination = field.Tiles.Values.Where(tile => field.Stops.Contains(tile.Index) && tile.Cost > 0)
                .OrderBy(tile => tile.Cost).ThenBy(tile => tile.Index).First();
            var path = movement.PathTo(field, beforeMove, new Coord(destination.X, destination.Y))!;
            movedBattle.Dispatch(new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = movingUnit["id"]!.GetValue<int>(),
                ["path"] = TacticalQueries.PathJson(path),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            });
            var movedState = movedBattle.Snapshot();
            var movedUnit = movedState["units"]!.AsArray().OfType<JsonObject>()
                .Single(unit => unit["key"]!.GetValue<string>() == "campaign-laiya");
            Require(movedUnit["x"]!.GetValue<int>() == destination.X && movedUnit["y"]!.GetValue<int>() == destination.Y,
                "The cache movement fixture must come from an accepted canonical move-and-wait command.");
            await CompareProjection(movedState, null, "Canonical movement replacement");
            Configure(live);
            Configure(oracle);
            await CompareProjection(state, null, "Rebound board");
            Require(state.ToJsonString() == original, "Idle cue caching and rebinding must leave canonical C10 state unchanged.");
            GD.Print("Idle occlusion cache oracle passed: 128 repeated-pose steps, 1 parent-transform step, and 16 steps each for fog, owner, canonical movement and rebind (193 comparisons).");
        }
        finally
        {
            liveParent.Free();
            oracleParent.Free();
        }
    }
}
