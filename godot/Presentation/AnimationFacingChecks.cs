#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckFacingPlayback()
    {
        var (manifest, bindings) = ReactionFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            var state = ConfigureFacingBoard(board, catalog, new Square4Grid(), FacingPlane);
            await CheckTemporaryFacing(board, catalog, state);
            await CheckOrderedFacing(board, catalog, state);
            await CheckPathFacing(board, state);
            await CheckFacingLifetime(board, catalog, state);
            await CheckComposedGridFacing(board, catalog);
            await CheckFacingLegalReceipt(board);
            GD.Print("Animation facing checks passed, including the lawful C04 receipt.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private JsonObject ConfigureFacingBoard(BoardView board, AssetCatalog catalog, ITacticalGrid grid, BoardPlaneFactory plane)
    {
        // Synthetic receipts over a small flat surface isolate facing from terrain and battle legality.
        var fixture = MaterialSceneFixtureChecks.Level;
        fixture["terrain"] = new JsonArray("........", "........", "........", "........", "........");
        var engine = new BattleEngine(packs);
        var state = engine.CreateState(fixture);
        state["rules"]!["grid"] = grid.Id;
        foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
            unit["facing"] = grid.RestingFacing;
        state["units"]![0]!["x"] = 3;
        state["units"]![0]!["y"] = 2;
        state["units"]![1]!["x"] = 4;
        state["units"]![1]!["y"] = 2;
        board.Configure(new(catalog, EnvironmentCatalog.Load(), grid, engine.Catalog, plane), fixture, new AuthoredScene
        {
            LevelId = fixture["id"]!.GetValue<string>(),
            MapSize = [8, 5],
            RuntimeReady = true,
            FramePlacements = []
        });
        board.ConfigureDynamicState();
        return state;
    }

    private static BoardPlane FacingPlane(Vector2 size, int minimumHeight, int maximumHeight) =>
        new(Vector2.Right, Vector2.Down, Vector2.Zero, size);

    private static int ActorFacing(BoardView board, int id) =>
        Actor(board, id).GetNode<Sprite2D>("AuthoredSprite").Scale.X < 0 ? -1 : 1;

    private static JsonObject FacingChanged(int id, string from, string to) => new()
    {
        ["type"] = "facingChanged",
        ["unit"] = id,
        ["from"] = from,
        ["to"] = to
    };

    private async Task CheckTemporaryFacing(BoardView board, AssetCatalog catalog, JsonObject source)
    {
        var aid = Int(source["units"]![0]!.AsObject(), "id");
        var did = Int(source["units"]![1]!.AsObject(), "id");
        var clip = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds);
        var idle = catalog.ResolveUnit("c01.swordsman");
        foreach (var initialFacing in new[] { "west", "east" })
            foreach (var (kind, target, expected) in new (string, Coord, int)[]
            {
                ("counter", new(3, 1), 1), ("counter", new(3, 3), 1),
                ("supportAttack", new(2, 2), -1), ("supportAttack", new(4, 2), 1),
                ("partingShot", new(2, 0), 1), ("attack", new(3, 1), 1)
            })
            {
                var state = (JsonObject)source.DeepClone();
                state["units"]![0]!["facing"] = initialFacing;
                state["units"]![1]!["x"] = target.X;
                state["units"]![1]!["y"] = target.Y;
                board.RenderState(state);
                var resting = initialFacing == "west" ? -1 : 1;
                Require(ActorFacing(board, aid) == resting,
                    "Settled facing uses the same projected grid sign that finite playback must restore.");
                var strike = Strike(kind, aid, did);
                if (kind == "partingShot")
                    strike["at"] = At(target.X, target.Y);
                // An activation without a facing receipt also models a delayed outgoing attack.
                var events = kind == "attack"
                    ? ScopedClipAttack(strike, At(3, 2), At(target.X, target.Y)) : new[] { strike };
                var transition = Transition(state, state, IdleAction(), events);
                var original = transition.Before.GetRawText() + transition.After.GetRawText()
                    + string.Concat(transition.Events.Select(item => item.GetRawText()));
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                var actor = Actor(board, aid);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var logical = actor.GetMeta("logical_contact").AsVector2();
                var origin = board.Projection.CellContact(new(3, 2));
                var direction = (board.Projection.CellContact(target) - origin).Normalized();
                CheckClipSprite(sprite, clip.Asset, 1, expected);
                CheckClipShadow(sprite);
                await Tick(board, .064);
                Require(actor.Position.IsEqualApprox(origin + direction * 7)
                    && actor.GetMeta("logical_contact").AsVector2() == logical,
                    "The lunge follows the continuous target vector even when square4 quantizes its sprite facing differently.");
                CheckClipSprite(sprite, clip.Asset, 2, expected);
                await Tick(board, .064);
                CheckClipSprite(sprite, idle, 0, resting);
                CheckClipShadow(sprite);
                CheckActorDepthCuts(board);
                Require(actor.Position == origin && !task.IsCompleted
                    && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "Reactive and delayed source clips restore their captured mirror at clip end, while the released impact is still alive.");
                await Drain(board, task);
                Require(ActorFacing(board, aid) == resting && original == transition.Before.GetRawText()
                    + transition.After.GetRawText() + string.Concat(transition.Events.Select(item => item.GetRawText())),
                    "Temporary aiming changes neither canonical facing nor the detached receipt.");
            }

        var hitState = (JsonObject)source.DeepClone();
        hitState["units"]![0]!["facing"] = "west";
        var laterHit = board.PresentTransitionAsync(viewportFeedback, Transition(hitState, hitState, IdleAction(),
            [Strike("counter", aid, did), ReactionDamage("statusTick", aid)]));
        await Tick(board, .485);
        Require(Frame(Actor(board, aid)) == 1 && ActorFacing(board, aid) == -1 && !laterHit.IsCompleted,
            "A later hit pose inherits restored resting facing, not the completed outgoing action's aim.");
        await Drain(board, laterHit);
    }

    private async Task CheckOrderedFacing(BoardView board, AssetCatalog catalog, JsonObject source)
    {
        var before = (JsonObject)source.DeepClone();
        var aid = Int(before["units"]![0]!.AsObject(), "id");
        var did = Int(before["units"]![1]!.AsObject(), "id");
        before["units"]![0]!["facing"] = "east";
        before["units"]![1]!["x"] = 3;
        before["units"]![1]!["y"] = 1;
        var after = (JsonObject)before.DeepClone();
        after["units"]![0]!["facing"] = "north";
        JsonObject[] events =
        [
            FacingChanged(aid, "east", "west"), ReactionDamage("statusTick", aid),
            FacingChanged(aid, "west", "north"),
            .. ScopedClipAttack(Strike("attack", aid, did), At(3, 2), At(3, 1))
        ];
        var observed = new List<(int Index, int Facing)>();
        void Presented(BattlePresentationTransition _, int index) => observed.Add((index, ActorFacing(board, aid)));
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(before, after, IdleAction(), events));
            Require(ActorFacing(board, aid) == -1 && observed.SequenceEqual(new[] { (0, -1) }),
                "An ordered facing receipt is visible immediately instead of waiting for, or borrowing from, the final snapshot.");
            await Tick(board, .421);
            Require(ActorFacing(board, aid) == 1 && Frame(Actor(board, aid)) == 1
                && observed.SequenceEqual(new[] { (0, -1), (1, -1), (2, 1) }),
                "A north-facing receipt with zero projected X takes effect before the next main activation.");
            await Tick(board, .128);
            CheckClipSprite(Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite"), catalog.ResolveUnit("c01.swordsman"), 0, 1);
            Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "A canonical main attack retains the receipt's facing when its source recovers before impact completion.");
            await Drain(board, task);
            Require(observed.Select(item => item.Index).SequenceEqual(Enumerable.Range(0, events.Length))
                && ActorFacing(board, aid) == 1,
                "Facing receipts preserve exact event publication order through final settlement.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckPathFacing(BoardView board, JsonObject source)
    {
        var before = (JsonObject)source.DeepClone();
        var id = Int(before["units"]![0]!.AsObject(), "id");
        before["units"]![0]!["facing"] = "west";
        var after = (JsonObject)before.DeepClone();
        after["units"]![0]!["x"] = 2;
        after["units"]![0]!["facing"] = "north";
        var path = new JsonArray(At(3, 2), At(3, 3), At(2, 3), At(2, 2));
        var events = new[]
        {
            FacingChanged(id, "west", "south"), FacingChanged(id, "south", "west"), FacingChanged(id, "west", "north"),
            new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path.DeepClone() }
        };
        var observed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => observed.Add(ActorFacing(board, id));
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(before, after, MovementAction(id, path), events));
            Require(ActorFacing(board, id) == 1 && observed.Count == 0,
                "The first zero-X movement segment faces right before its first frame, while command receipts still wait behind the path.");
            await Tick(board, .161);
            Require(ActorFacing(board, id) == -1 && observed.Count == 0,
                "The next leftward segment changes mirror without publishing command events early.");
            await Tick(board, .160);
            Require(ActorFacing(board, id) == 1 && observed.Count == 0,
                "Turning from west onto a zero-X north segment cannot retain the previous left mirror.");
            await Drain(board, task);
            Require(observed.SequenceEqual(new[] { 1, -1, 1, 1 }) && ActorFacing(board, id) == 1,
                "The command path still precedes its facing and move receipts, and final settlement agrees with the last direction.");
        }
        finally { board.EventPresented -= Presented; }

        foreach (var mode in new[] { "push", "pull", "teleport" })
        {
            after = (JsonObject)before.DeepClone();
            after["units"]![0]!["y"] = 3;
            after["units"]![0]!["facing"] = mode == "teleport" ? "west" : "south";
            var forced = new JsonObject
            {
                ["type"] = "forcedMove",
                ["unit"] = id,
                ["mode"] = mode,
                ["from"] = At(3, 2),
                ["to"] = At(3, 3),
                ["path"] = new JsonArray(At(3, 2), At(3, 3))
            };
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, IdleAction(), [forced, ReactionDamage("statusTick", id)]));
            var expected = mode == "teleport" ? -1 : 1;
            Require(ActorFacing(board, id) == expected,
                "Push and pull face along their first segment immediately; teleport preserves the current facing.");
            await Tick(board, .121);
            Require(ActorFacing(board, id) == expected && !task.IsCompleted
                && Actor(board, id).Position == board.Projection.CellContact(new(3, 3)),
                "Forced displacement facing survives into the next reaction without relying on final settlement.");
            await Drain(board, task);
        }
    }

    private async Task CheckFacingLifetime(BoardView board, AssetCatalog catalog, JsonObject source)
    {
        var before = (JsonObject)source.DeepClone();
        var aid = Int(before["units"]![0]!.AsObject(), "id");
        var did = Int(before["units"]![1]!.AsObject(), "id");
        before["units"]![0]!["facing"] = "west";
        var after = (JsonObject)before.DeepClone();
        after["units"]![0]!["facing"] = "east";
        var transition = Transition(before, after, IdleAction(), [Strike("counter", aid, did)]);
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var rebind in new[] { false, true })
                foreach (var phase in new[] { .040, .096, .150 })
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, phase);
                    var clip = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds);
                    var elapsed = phase * 1000;
                    var recovered = elapsed >= clip.Length;
                    var expectedFrame = recovered
                        ? catalog.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds).FrameAt(0)
                        : clip.FrameAt(elapsed);
                    Require(ActorFacing(board, aid) == (recovered ? -1 : 1) && Frame(Actor(board, aid)) == expectedFrame
                        && !task.IsCompleted && (board.GetNodeOrNull("BattleEffects/Hit") is not null) == (elapsed >= clip.RequireRelease()),
                        "Before release, during recovery and after recovery, interruption sees the exact source pose, mirror and impact lifetime.");
                    if (rebind)
                    {
                        ConfigureFacingBoard(board, catalog, new Square4Grid(), FacingPlane);
                        board.RenderState(after);
                    }
                    else
                        board.CancelPresentation();
                    await Tick(board, 1);
                    Require(!await task && completed.Count == 0 && !board.IsPresenting && ActorFacing(board, aid) == 1
                        && board.GetNodeOrNull("BattleEffects") is null,
                        "Cancellation and rebind use committed facing; retired recovery cannot restore a stale captured mirror.");
                }
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && ActorFacing(board, aid) == 1
                && completed.SequenceEqual(new[] { 0 }) && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion settles canonical facing synchronously without retaining temporary aim ownership.");
            board.AnimationsEnabled = true;
            completed.Clear();
            var visible = new HashSet<int> { did };
            var hidden = Transition(before, after, IdleAction(),
                [FacingChanged(aid, "west", "east"), Strike("counter", aid, did)], visible);
            var hiddenTask = board.PresentTransitionAsync(viewportFeedback, hidden, visible);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "A hidden source acquires neither facing nor pose ownership while its visible recipient keeps its impact.");
            await Drain(board, hiddenTask);
            await Tick(board, 1);
            Require(completed.SequenceEqual(new[] { 0, 1 }) && board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null,
                "Hidden facing receipts preserve event order without resurrecting the source after recovery.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckComposedGridFacing(BoardView board, AssetCatalog catalog)
    {
        var grid = new OddRowHexGrid();
        var state = ConfigureFacingBoard(board, catalog, grid, BoardPlane.Dimetric);
        var aid = Int(state["units"]![0]!.AsObject(), "id");
        var did = Int(state["units"]![1]!.AsObject(), "id");
        state["units"]![0]!["facing"] = "hexEast";
        state["units"]![1]!["x"] = 3;
        state["units"]![1]!["y"] = 3;
        board.RenderState(state);
        Require(grid.Toward(new(3, 2), new(3, 3)) == "hexSoutheast"
            && board.Projection.FacingVector("hexSoutheast").X < 0,
            "The composed hex fixture has non-square direction identities and a projected left-facing southeast.");
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), [Strike("counter", aid, did)]));
        Require(ActorFacing(board, aid) == -1,
            "Outgoing facing comes from the supplied hex strategy rather than square direction names or target cell X.");
        await Tick(board, .128);
        Require(ActorFacing(board, aid) == 1 && !task.IsCompleted,
            "A composed grid restores its own canonical pre-action mirror before impact completion.");
        await Drain(board, task);
        var after = (JsonObject)state.DeepClone();
        after["units"]![0]!["y"] = 3;
        after["units"]![0]!["facing"] = "hexSoutheast";
        var path = new JsonArray(At(3, 2), At(3, 3));
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, MovementAction(aid, path),
            [FacingChanged(aid, "hexEast", "hexSoutheast"), new JsonObject { ["type"] = "move", ["unit"] = aid, ["path"] = path }]));
        Require(ActorFacing(board, aid) == -1, "Movement also resolves its initial facing through the composed grid.");
        await Drain(board, task);
        Require(ActorFacing(board, aid) == -1, "Composed movement, facing receipts and settlement agree on the same mirror.");
    }

    private async Task CheckFacingLegalReceipt(BoardView board)
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-04-facing.json");
        var levelId = fixture["levelId"]!.GetValue<string>();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel, fixture["seed"]!.GetValue<uint>());
        var prefix = fixture["prefixActions"]!.AsArray();
        var prefixHashes = fixture["prefixStateHashes"]!.AsArray();
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>()
            && prefix.Count == Int(fixture, "index") && prefix.Count == prefixHashes.Count,
            "The C04 facing witness starts from its exact independent level and seed, not an injected campaign checkpoint.");
        for (var index = 0; index < prefix.Count; index++)
        {
            battle.Dispatch(prefix[index]!.AsObject());
            Require(battle.StateHash == prefixHashes[index]!.GetValue<string>(),
                $"The lawful C04 prefix retains its recorded state after order {index}.");
        }
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(),
            "The original C04 prefix reaches the recorded west-facing gravekeeper without editing battle state.");
        var action = fixture["action"]!.AsObject();
        var receipt = battle.Dispatch(action);
        var before = receipt.Before;
        var after = battle.Snapshot();
        var events = receipt.Events;
        var id = Int(action, "unit");
        var source = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == id);
        var settled = after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == id);
        Require(id == 9 && source["type"]!.GetValue<string>() == "c01.gravekeeper"
            && Int(source, "x") == 25 && Int(source, "y") == 16 && source["facing"]!.GetValue<string>() == "west"
            && Int(settled, "x") == 25 && Int(settled, "y") == 17 && settled["facing"]!.GetValue<string>() == "north"
            && battle.StateHash == fixture["afterStateHash"]!.GetValue<string>()
            && EcmaJson.Sha256(new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>(),
            "The real lantern order retains its exact movement, canonical facing, outcome and ordered event hash.");
        var scene = AuthoredScene.Load($"res://data/scenes/{levelId}.json");
        var rendering = BoardSceneRendering.Load(scene, recordedLevel, engine.CreateState(recordedLevel)["map"]!.AsObject());
        board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get(before["rules"]!["grid"]!.GetValue<string>()),
            engine.Catalog, rendering.Plane), recordedLevel, scene);
        board.ConfigureGround(rendering.Ground);
        board.ConfigureDynamicState();
        board.RenderState(before);
        Require(ActorFacing(board, id) == -1 && board.Projection.FacingVector("south").X == 0
            && board.Projection.FacingVector("north").X == 0,
            "The actual C04 authored registration gives both recorded vertical facings zero screen X, after a left-facing start.");
        var transition = Transition(before, after, action, events);
        var original = transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
            + string.Concat(transition.Events.Select(item => item.GetRawText()));
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index)
        {
            Require(ActorFacing(board, id) == 1, "Every published C04 receipt observes the correct right mirror before final settlement.");
            completed.Add(index);
        }
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, transition);
            var actor = Actor(board, id);
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var start = board.Projection.LogicalCellContact(new(25, 16));
            var end = board.Projection.LogicalCellContact(new(25, 17));
            Require(ActorFacing(board, id) == 1 && actor.GetMeta("logical_contact").AsVector2() == start && completed.Count == 0,
                "The real gravekeeper turns right on the first movement frame without publishing command receipts early.");
            await Tick(board, .080);
            Require(ActorFacing(board, id) == 1 && actor.GetMeta("logical_contact").AsVector2().IsEqualApprox(start.Lerp(end, .5f))
                && actor.Position.IsEqualApprox(board.Projection.Project(start.Lerp(end, .5f))),
                "The C04 move keeps its linear support contact and right mirror halfway through the accepted cell.");
            await Tick(board, .080);
            var policy = assets.ResolveWeaponAction(action["command"]!["weapon"]!.GetValue<string>());
            var clip = assets.ResolveUnitClip("c01.gravekeeper", policy.Action, ActorClipUnit.Milliseconds);
            Require(policy.Motion == ActorMotion.Stationary && completed.SequenceEqual(new[] { 0, 1, 2 })
                && ActorFacing(board, id) == 1 && Frame(actor) == clip.FrameAt(0)
                && actor.GetMeta("logical_contact").AsVector2() == end && actor.Position == board.Projection.Project(end),
                "Both lawful facing receipts precede the stationary lantern activation at the unchanged destination contact.");
            await Tick(board, clip.RequireRelease() / 1000);
            Require(ActorFacing(board, id) == 1 && actor.GetMeta("logical_contact").AsVector2() == end
                && actor.Position == board.Projection.Project(end)
                && board.GetNode<Node2D>("BattleEffects/Hit").Position == board.Projection.CellContact(new(25, 15))
                && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-23",
                "The real lantern release retains source contact and right facing while applying its unchanged target impact.");
            await Tick(board, (clip.Length - clip.RequireRelease()) / 1000);
            CheckClipSprite(sprite, assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds).Asset, 0, 1);
            CheckClipShadow(sprite);
            Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null
                && actor.GetMeta("logical_contact").AsVector2() == end && actor.Position == board.Projection.Project(end),
                "C04 recovery returns to right-facing idle at the same support contact before the impact finishes.");
            await Drain(board, task);
            Require(completed.SequenceEqual(Enumerable.Range(0, events.Count)) && ActorFacing(board, id) == 1
                && Actor(board, id).GetMeta("logical_contact").AsVector2() == end
                && Actor(board, id).Position == board.Projection.Project(end)
                && battle.StateHash == fixture["afterStateHash"]!.GetValue<string>()
                && StateDigest.Hash(before) == fixture["beforeStateHash"]!.GetValue<string>()
                && StateDigest.Hash(after) == battle.StateHash && original == transition.Before.GetRawText() + transition.After.GetRawText()
                    + transition.Action.GetRawText() + string.Concat(transition.Events.Select(item => item.GetRawText())),
                "The complete C04 receipt settles the same facing and contact without changing event order or canonical battle facts.");
        }
        finally { board.EventPresented -= Presented; }
    }
}
