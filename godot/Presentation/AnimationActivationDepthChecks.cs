#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const string ActivationDepthWeapon = "fixture.activation-depth";

    private sealed record ActivationDepthFixture(AssetCatalog Catalog, JsonObject Level, AuthoredScene Scene,
        JsonObject Before, JsonObject After, JsonObject[] Events, int Source, int Target);

    private async Task CheckActivationDepthOwnership()
    {
        foreach (var recovery in new[] { 500.0, 900.0 })
        {
            var fixture = ActivationDepthCase(recovery);
            var board = new BoardView { AdvanceAnimationsAutomatically = false };
            AddChild(board);
            try
            {
                ConfigureActivationDepth(board, fixture);
                board.RenderState(fixture.Before);
                var baselineCuts = ActivationDepthCuts(board);
                var source = Actor(board, fixture.Source);
                var sourceDepth = board.Projection.Depth(source.GetMeta("logical_contact").AsVector2());
                var actionCut = (int)Math.Floor(sourceDepth + 1) + 1;
                var start = Actor(board, fixture.Target).GetMeta("logical_contact").AsVector2();
                var finish = board.Projection.LogicalCellContact(new(6, 3));
                var futureContact = start.Lerp(finish, .75f);
                var futureCut = (int)Math.Floor(board.Projection.Depth(futureContact)) + 1;
                var first = board.Projection.DepthPartitions.Min(partition => partition.StartDepth);
                var end = board.Projection.DepthPartitions.Max(partition => partition.EndDepthExclusive);
                Require(board.Projection.Faces.Count == 0 && board.RenderedCoverPropCount == 0
                    && actionCut > first && actionCut < end && futureCut > first && futureCut < end
                    && !baselineCuts.Contains(actionCut) && !baselineCuts.Contains(futureCut),
                    "The flat activation-depth witness has genuinely absent action and future-path cuts, not cliff or idle-actor coincidences.");

                board.RenderState(fixture.After);
                var settledCuts = ActivationDepthCuts(board);
                Require(!settledCuts.Contains(actionCut) && !settledCuts.Contains(futureCut),
                    "Neither transient witness cut belongs to the committed settled projection.");
                board.RenderState(fixture.Before);
                await CheckActivationDepthOverlap(board, fixture, recovery, baselineCuts, settledCuts,
                    actionCut, futureCut, futureContact);
                await CheckActivationDepthRetirement(board, fixture, settledCuts, actionCut, futureCut);
            }
            finally { RemoveChild(board); board.Free(); }
        }
    }

    private async Task CheckActivationDepthOverlap(BoardView board, ActivationDepthFixture fixture, double recovery,
        HashSet<int> baselineCuts, HashSet<int> settledCuts, int actionCut, int futureCut, Vector2 futureContact)
    {
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        var original = fixture.Before.ToJsonString() + fixture.After.ToJsonString();
        try
        {
            var playing = board.PresentTransitionAsync(viewportFeedback,
                Transition(fixture.Before, fixture.After, IdleAction(), fixture.Events));
            Require(ActivationDepthCuts(board).Contains(actionCut)
                && SpriteGroundContact.Offset(Actor(board, fixture.Source).GetNode<Sprite2D>("AuthoredSprite")).Y == 1,
                "Activation prepares its distinct authored source contact before any recipient movement begins.");

            // Release is 32 ms, impact is 420 ms, then a two-cell push takes 240 ms.
            await Tick(board, .453);
            Require(completed.SequenceEqual(new[] { 0, 1 }) && !playing.IsCompleted
                && ActivationDepthCuts(board).Contains(actionCut) && ActivationDepthCuts(board).Contains(futureCut),
                "Starting victim displacement reserves its full path without discarding the still-active source contact.");
            CheckActorDepthCuts(board);

            await Tick(board, .048);
            var victim = Actor(board, fixture.Target);
            var currentCut = ActivationActorCut(board, victim);
            Require(!baselineCuts.Contains(currentCut) && !settledCuts.Contains(currentCut)
                && currentCut != futureCut && !playing.IsCompleted,
                "The sampled mid-push contact requires its own missing-baseline cut while the receipt is still active.");
            Require(ActivationDepthCuts(board).Contains(currentCut) && ActivationDepthCuts(board).Contains(futureCut),
                "Source recovery completion must retain both the victim's current cut and its remaining path cuts.");
            if (recovery == 500)
                Require(SpriteGroundContact.Offset(Actor(board, fixture.Source).GetNode<Sprite2D>("AuthoredSprite")) == Vector2.Zero,
                    "The short source has actually resumed idle while the victim is still moving.");
            else
                Require(ActivationDepthCuts(board).Contains(actionCut),
                    "The longer source retains its authored action contact throughout victim movement.");
            CheckActorDepthCuts(board);

            await Tick(board, .131);
            victim = Actor(board, fixture.Target);
            Require(victim.GetMeta("logical_contact").AsVector2().IsEqualApprox(futureContact)
                && ActivationActorCut(board, victim) == futureCut && ActivationDepthCuts(board).Contains(futureCut),
                "A reserved future path cut remains present when the displaced victim actually reaches that depth.");
            CheckActorDepthCuts(board);

            await Tick(board, .061);
            if (recovery == 900)
                Require(!playing.IsCompleted && completed.SequenceEqual(new[] { 0, 1, 2 })
                    && ActivationDepthCuts(board).Contains(actionCut)
                    && SpriteGroundContact.Offset(Actor(board, fixture.Source).GetNode<Sprite2D>("AuthoredSprite")).Y == 1,
                    "Finishing the victim's movement cannot retire the longer source action's contact projection.");
            await Drain(board, playing);
            Require(completed.SequenceEqual(new[] { 0, 1, 2, 3 }) && ActivationDepthCuts(board).SetEquals(settledCuts)
                && fixture.Before.ToJsonString() + fixture.After.ToJsonString() == original,
                "Both finite actors retire their transient cuts once, preserving ordered publication and canonical snapshots.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckActivationDepthRetirement(BoardView board, ActivationDepthFixture fixture,
        HashSet<int> settledCuts, int actionCut, int futureCut)
    {
        foreach (var rebind in new[] { false, true })
        {
            var completed = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
            board.EventPresented += Presented;
            try
            {
                var playing = board.PresentTransitionAsync(viewportFeedback,
                    Transition(fixture.Before, fixture.After, IdleAction(), fixture.Events));
                await Tick(board, .480);
                Require(ActivationDepthCuts(board).Contains(actionCut) && ActivationDepthCuts(board).Contains(futureCut)
                    && completed.SequenceEqual(new[] { 0, 1 }) && !playing.IsCompleted,
                    "The retirement witness interrupts two live finite actors with independently reserved depth cuts.");
                if (rebind)
                {
                    ConfigureActivationDepth(board, fixture);
                    board.RenderState(fixture.After);
                }
                else
                    board.CancelPresentation();
                Require(!await playing && !board.IsPresenting && ActivationDepthCuts(board).SetEquals(settledCuts)
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation and rebinding discard both actors' finite depth projections before settling the committed state.");
                await Tick(board, 2);
                Require(completed.SequenceEqual(new[] { 0, 1 }) && ActivationDepthCuts(board).SetEquals(settledCuts)
                    && !ActivationDepthCuts(board).Contains(actionCut) && !ActivationDepthCuts(board).Contains(futureCut),
                    "Retired playback cannot restore transient cuts or publish later displacement/completion events.");
                CheckActorDepthCuts(board);
            }
            finally { board.EventPresented -= Presented; }
        }
    }

    private ActivationDepthFixture ActivationDepthCase(double recovery)
    {
        var fixtureLevel = MaterialSceneFixtureChecks.Level;
        fixtureLevel["id"] = "activation-depth-ownership";
        fixtureLevel["terrain"] = new JsonArray("........", "........", "........", "........", "........");
        fixtureLevel["elevation"] = new JsonArray(Enumerable.Range(0, 40).Select(_ => (JsonNode)JsonValue.Create(0)!).ToArray());
        fixtureLevel["cliffs"] = new JsonArray();
        fixtureLevel["units"]![0]!["x"] = 1;
        fixtureLevel["units"]![0]!["y"] = 1;
        fixtureLevel["units"]![1]!["x"] = 4;
        fixtureLevel["units"]![1]!["y"] = 3;
        var scene = new AuthoredScene
        {
            LevelId = "activation-depth-ownership",
            MapSize = new[] { 8, 5 },
            RuntimeReady = true,
            FramePlacements = new()
        };
        var before = new BattleEngine(packs).CreateState(fixtureLevel);
        var units = before["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var source = Int(units[0], "id");
        var target = Int(units[1], "id");
        var after = (JsonObject)before.DeepClone();
        after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == target)["x"] = 6;

        var (manifest, bindings) = ActorClipFixture();
        var trusted = manifest.Assets.Single(record => record.Id == "torren-stage01");
        // Existing pixels with controlled uniform contact geometry, not per-frame contacts or new artwork.
        manifest.Assets.Add(new AssetRecord
        {
            Id = "fixture.activation-depth-contact",
            TopicId = trusted.TopicId,
            ResourcePath = trusted.ResourcePath,
            Width = trusted.PhysicalWidth,
            Height = trusted.PhysicalHeight,
            Frames = 1,
            FrameOrder = new[] { "attack" },
            PhysicalWidth = trusted.PhysicalWidth,
            PhysicalHeight = trusted.PhysicalHeight,
            PixelDensity = 1,
            Anchor = new[] { trusted.PhysicalWidth / 2.0, 100.0 },
            GroundFront = new[] { trusted.PhysicalWidth / 2.0, 116.0 },
            PresentationScale = 1.0 / 16
        });
        manifest.ActorClips[ClipFixtureId] = new()
        {
            Unit = "milliseconds",
            ReleaseMs = 32,
            AppearanceId = "fixture.activation-depth-contact",
            Frames = new[] { new ActorClipFrameRecord { Frame = "attack", Span = recovery } }
        };
        bindings.WeaponActions[ActivationDepthWeapon] = new()
        {
            Action = "attack",
            Motion = "stationary"
        };
        var events = MainAttackScope(units[0], units[1], ActivationDepthWeapon,
            ReleaseStrike("attack", source, target, ActivationDepthWeapon),
            new JsonObject
            {
                ["type"] = "forcedMove",
                ["unit"] = target,
                ["mode"] = "push",
                ["from"] = At(4, 3),
                ["to"] = At(6, 3),
                ["path"] = new JsonArray(At(4, 3), At(5, 3), At(6, 3))
            });
        return new(new AssetCatalog(manifest, bindings), fixtureLevel, scene, before, after, events, source, target);
    }

    private void ConfigureActivationDepth(BoardView board, ActivationDepthFixture fixture)
    {
        board.Configure(new(fixture.Catalog, EnvironmentCatalog.Load(), new Empire.Core.Geometry.Square4Grid(),
            new Empire.Core.State.ReadOnlyContent(packs), BoardPlane.Dimetric), fixture.Level, fixture.Scene);
        board.ConfigureDynamicState();
    }

    private static HashSet<int> ActivationDepthCuts(BoardView board) => board.Projection.DepthPartitions
        .SelectMany(partition => new[] { partition.StartDepth, partition.EndDepthExclusive }).ToHashSet();

    private static int ActivationActorCut(BoardView board, Node2D actor) => (int)Math.Floor(
        board.Projection.Depth(actor.GetMeta("logical_contact").AsVector2())
        + new SpriteStandingVisual(actor.GetNode<Sprite2D>("AuthoredSprite")).FrontOffset(actor).Y) + 1;
}
