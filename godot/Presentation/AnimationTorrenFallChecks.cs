#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Lifecycle;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckTorrenFallPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds, "campaign-torren");
        var starts = new[] { 0.0, 60, 130, 220, 330, 440 };
        var names = new[] { "fall-buckle", "fall-kneel", "fall-collapse", "fall-contact", "fall-settle", "fall-still" };
        Require(clip.Asset.Identity == "torren-fall-v1" && clip.Asset.FrameOrder.SequenceEqual(names)
            && clip.FrameIndices.SequenceEqual(Enumerable.Range(0, 6)) && clip.Length == 620 && clip.ReleaseMs is null
            && clip.RequireDeath().Mode == ActorDeathMode.AfterClip && clip.RequireDeath().TerminalFadeMs == 160,
            "Torren selects six authored falling poses over 620 ms followed by an explicit 160 ms fade.");
        var pictures = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt(index == 5 ? 619.999 : starts[index + 1] - .001) == index,
                "The production fall samples its six exact half-open pose intervals.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each falling drawing retains its complete padded silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(620) == 5 && clip.FrameAt(10000) == 5,
            "Six distinct fall pictures hold their terminal ground pose without looping or restoring idle.");

        // Production artwork on synthetic receipt data tests ownership, not a lawful battle witness.
        var state = ReleaseState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
        var unit = state["units"]![0]!.AsObject();
        unit["key"] = "campaign-torren";
        var id = Int(unit, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var facing in new[] { "west", "east" })
            {
                unit["facing"] = facing;
                await CheckTorrenFallBoundaries(board, state, id, clip, facing == "west" ? -1 : 1);
            }
            await CheckTorrenFallColor(board, state, id, clip);
            await CheckTorrenFallScope(board, state, id);
            await CheckTorrenFallResult(board, state, id);
            await CheckTorrenFallLifetime(board, state, id);
            await CheckTorrenFallVisibility(board, state, id);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Torren fall: six production poses, 620+160 ms phases, mirrors/contact, event ownership, result wait and lifecycle boundaries passed.");
    }

    private async Task CheckTorrenFallBoundaries(BoardView board, JsonObject state, int id, ActorClip clip, int sign)
    {
        var after = WithoutDeathActor(state, id);
        var unchanged = state.ToJsonString();
        var published = new List<int>();
        var counts = new List<int>();
        void Presented(BattlePresentationTransition _, int index)
        {
            published.Add(index);
            counts.Add(board.RenderedUnitCount);
        }
        board.EventPresented += Presented;
        try
        {
            foreach (var ms in new[] { 0, 59.999, 60, 129.999, 130, 219.999, 220, 329.999, 330, 439.999, 440, 619.999, 620, 620.001, 700, 779.999, 780, 10000 })
            {
                published.Clear();
                counts.Clear();
                var task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, after, IdleAction(), [ReactionDeathEvent(id), ReactionDeathEvent(id)]));
                var actor = Actor(board, id);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var origin = actor.Position;
                var contact = actor.GetMeta("logical_contact").AsVector2();
                var exits = 0;
                actor.TreeExiting += () => exits++;
                await Tick(board, ms / 1000);
                if (ms < 780)
                {
                    var frame = ms < 60 ? 0 : ms < 130 ? 1 : ms < 220 ? 2 : ms < 330 ? 3 : ms < 440 ? 4 : 5;
                    var alpha = ms <= 620 ? 1 : (780 - ms) / 160;
                    CheckClipSprite(sprite, clip.Asset, frame, sign);
                    CheckClipShadow(sprite);
                    Require(!task.IsCompleted && board.IsPresenting && published.Count == 0 && exits == 0
                        && actor.SelfModulate.IsEqualApprox(new Color(1, 1, 1, (float)alpha)),
                        $"Torren's production fall/fade holds the expected pose and opacity without publication at {ms} ms.");
                    Require(actor.Position == origin && actor.Scale == Vector2.One && actor.Rotation == 0 && sprite.Rotation == 0
                        && actor.GetMeta("logical_contact").AsVector2() == contact && board.RenderedUnitCount == 2
                        && actor.GetNodeOrNull("ActionAccent") is null,
                        "Falling anatomy comes from the drawings, never root translation, rotation, scaling, or an attack accent.");
                    board.CancelPresentation();
                    Require(!await task && published.Count == 0,
                        "Every unfinished production death sample cancels without publishing either duplicate event.");
                }
                else
                    Require(task.IsCompleted && await task && !board.IsPresenting && exits == 1
                        && published.SequenceEqual(new[] { 0, 1 }) && counts.SequenceEqual(new[] { 1, 1 })
                        && board.RenderedUnitCount == 1 && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                        "Exact or coarse completion retires Torren once at 780 ms; duplicate death adds no wait or second removal.");
                Require(state.ToJsonString() == unchanged && board.GetNodeOrNull("BattleEffects") is null
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Production death leaves canonical fixture data unchanged and removes transient layers at settlement.");
            }
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckTorrenFallColor(BoardView board, JsonObject state, int id, ActorClip clip)
    {
        var tint = new Color(.2f, .4f, .7f, .6f);
        var scale = new Vector2(.8f, 1.2f);
        var origin = Vector2.Zero;
        void Initialize(BattlePresentationTransition _, int index)
        {
            if (index != 0)
                return;
            var actor = Actor(board, id);
            actor.SelfModulate = tint;
            actor.Scale = scale;
            actor.Position += new Vector2(3, -4);
            origin = actor.Position;
        }
        board.EventPresented += Initialize;
        try
        {
            foreach (var ms in new[] { 0, 60, 130, 220, 330, 440, 620, 700 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(),
                    [new JsonObject { ["type"] = "facingChanged", ["unit"] = id, ["to"] = "east" }, ReactionDeathEvent(id)]));
                var actor = Actor(board, id);
                await Tick(board, ms / 1000.0);
                var expected = new Color(tint.R, tint.G, tint.B, ms <= 620 ? tint.A : tint.A / 2);
                Require(!task.IsCompleted && actor.SelfModulate.IsEqualApprox(expected)
                    && actor.Position == origin && actor.Scale == scale && actor.Rotation == 0,
                    "Every production falling pose preserves captured RGBA and transform; only terminal fade multiplies alpha.");
                CheckClipSprite(actor.GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, clip.FrameAt(ms), 1);
                board.CancelPresentation();
                Require(!await task, "Tinted production falling and fading poses remain cancellable.");
            }
        }
        finally { board.EventPresented -= Initialize; }
    }

    private async Task CheckTorrenFallScope(BoardView board, JsonObject state, int id)
    {
        var idle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var recoil = assets.ResolveUnitClip("c01.swordsman", "hit", ActorClipUnit.Milliseconds, "campaign-torren");
        var source = state["units"]![1]!.AsObject();
        var target = state["units"]![0]!.AsObject();
        var strike = Strike("attack", Int(source, "id"), id);
        strike["killed"] = true;
        var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(),
            MainAttackScope(source, target, "c01.border-blade", strike)));
        await Tick(board, .075001);
        var actor = Actor(board, id);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, recoil.Asset, 0, 1);
        await Tick(board, .320);
        CheckClipSprite(sprite, idle.Asset, 0, 1);
        await Tick(board, .139998);
        Require(!task.IsCompleted && actor.SelfModulate == Colors.White && actor.Scale == Vector2.One
            && sprite.GetMeta("art_identity").AsString() == idle.Asset.Identity,
            "A killed flag and absence from After cannot start falling before an ordered death event exists.");
        await Tick(board, .000002);
        Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
            "Without a death event, snapshot settlement adds no invented fall or fade delay.");

        task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), [ReactionDamage("statusTick", id)]));
        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
        await Tick(board, .421);
        Require(task.IsCompleted && await task, "A status report with hpAfter zero retains the ordinary impact duration.");
        CheckSettledClipActor(board, id, idle.Asset, board.Projection.CellContact(new(12, 16)));
    }

    private async Task CheckTorrenFallResult(BoardView board, JsonObject state, int id)
    {
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(),
                [ReactionDeathEvent(id), CelebrationEvent(BattleConclusion.LastTeamRemaining(1))]));
            await Tick(board, .620);
            Require(published.Count == 0 && !task.IsCompleted && board.IsPresenting
                && Frame(Actor(board, id)) == 5 && Actor(board, id).SelfModulate.A == 1,
                "Torren's ground-pose endpoint cannot release the death or game-over event before terminal fade.");
            await Tick(board, .159);
            Require(published.Count == 0 && !task.IsCompleted && Frame(Actor(board, id)) == 5,
                "The result remains gated throughout Torren's terminal fade.");
            await Tick(board, .002);
            Require(published.SequenceEqual(new[] { 0 }) && !task.IsCompleted && board.IsPresenting
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "Only Torren's terminal retirement releases the result event's own presentation beat.");
            await Tick(board, .218);
            Require(published.SequenceEqual(new[] { 0 }) && !task.IsCompleted,
                "The original result beat still lasts 220 ms after death completes.");
            await Tick(board, .002);
            Require(task.IsCompleted && await task && published.SequenceEqual(new[] { 0, 1 }) && !board.IsPresenting,
                "Death and game-over each publish once, in order, after their independent presentation spans.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckTorrenFallLifetime(BoardView board, JsonObject state, int id)
    {
        var idle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var after = WithoutDeathActor(state, id);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var ms in new[] { 130, 620, 700 })
            {
                foreach (var dead in new[] { false, true })
                {
                    var task = board.PresentTransitionAsync(viewportFeedback,
                        Transition(state, dead ? after : state, IdleAction(), [ReactionDeathEvent(id)]));
                    await Tick(board, ms / 1000.0);
                    board.CancelPresentation();
                    Require(!await task && published.Count == 0 && !board.IsPresenting,
                        "Cancellation during fall, phase boundary or fade cannot publish Torren's unfinished death.");
                    if (!dead)
                        CheckSettledClipActor(board, id, idle.Asset, board.Projection.CellContact(new(12, 16)));
                    await Tick(board, 10);
                    Require(dead ? board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null
                        : Actor(board, id).GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString() == idle.Asset.Identity,
                        "Retired fall callbacks cannot resurrect committed dead Torren or overwrite a living snapshot's breathing.");
                    Require(board.RenderedUnitCount == (dead ? 1 : 2) && board.GetNodeOrNull("BattleEffects") is null
                        && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                        "Cancelled production death settles the exact committed actor count and removes both transient layers.");
                }
                var rebound = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, after, IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                ConfigureActorClipBoard(board, assets);
                board.RenderState(state);
                Require(!await rebound && published.Count == 0 && !board.IsPresenting,
                    "Rebinding in either production death phase cancels the old presentation owner.");
                CheckSettledClipActor(board, id, idle.Asset, board.Projection.CellContact(new(12, 16)));
                await Tick(board, 10);
                Require(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString() == idle.Asset.Identity,
                    "Old production fall/fade callbacks cannot replace the rebound actor's declared idle.");

                var exiting = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                RemoveChild(board);
                Require(!await exiting && published.Count == 0 && !board.IsPresenting,
                    "Tree exit cancels Torren's falling and fading waiters without publishing a stale event.");
                AddChild(board);
                ConfigureActorClipBoard(board, assets);
                board.RenderState(state);
                CheckSettledClipActor(board, id, idle.Asset, board.Projection.CellContact(new(12, 16)));
            }
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, after, IdleAction(), [ReactionDeathEvent(id), ReactionDeathEvent(id)]));
            Require(reduced.IsCompleted && await reduced && published.SequenceEqual(new[] { 0, 1 }) && !board.IsPresenting
                && board.RenderedUnitCount == 1 && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Reduced motion completes both production death phases synchronously with one removal and ordered duplicate publication.");
        }
        finally { board.AnimationsEnabled = true; board.EventPresented -= Presented; }
    }

    private async Task CheckTorrenFallVisibility(BoardView board, JsonObject state, int id)
    {
        var manifest = PresentationJson.Load<AssetManifest>("res://data/selected-assets.json");
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        bindings.Actors["campaign-torren"].UnitArt.ActionClips.Remove("death");
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        var visible = new HashSet<int> { Int(state["units"]![1]!.AsObject(), "id") };
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)], visible), visible);
            Require(task.IsCompleted && await task && published.SequenceEqual(new[] { 0 }) && !board.IsPresenting
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "Hidden Torren skips both death phases before resolving even a missing named death binding.");
            published.Clear();
            var failed = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)]));
            var rejected = false;
            try
            {
                await failed;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && published.Count == 0 && !board.IsPresenting && board.RenderedUnitCount == 1
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Visible missing named death art fails explicitly, settles committed state and releases presentation resources.");
        }
        finally { board.EventPresented -= Presented; ConfigureActorClipBoard(board, assets); }
    }
}
