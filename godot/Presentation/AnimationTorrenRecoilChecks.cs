#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckTorrenRecoilPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.swordsman", "hit", ActorClipUnit.Milliseconds, "campaign-torren");
        var idle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var starts = new[] { 0.0, 40, 150, 240 };
        var names = new[] { "recoil-onset", "recoil-peak", "recoil-recover", "recoil-ready" };
        Require(clip.Asset.Identity == "torren-recoil-v1" && clip.Asset.FrameOrder.SequenceEqual(names)
            && clip.Length == 320 && clip.ReleaseMs is null && clip.Death is null,
            "Torren alone selects the explicit four-pose 320 ms recoil without an attack release or death policy.");
        var pictures = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt(index == 3 ? 319.999 : starts[index + 1] - .001) == index,
                "The production recoil samples four half-open authored pose intervals.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each recoil drawing retains its complete padded silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 4 && clip.FrameAt(320) == 3 && clip.FrameAt(10000) == 3,
            "Four distinct recoil pictures hold their final finite pose without creating a loop.");

        // Production art on synthetic receipt data: scope/clock checks, not a lawful battle witness.
        var state = ReleaseState();
        var target = state["units"]![0]!.AsObject();
        target["key"] = "campaign-torren";
        var source = state["units"]![1]!.AsObject();
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var facing in new[] { "west", "east" })
            {
                target["facing"] = facing;
                source["x"] = facing == "west" ? 11 : 13;
                var sign = facing == "west" ? -1 : 1;
                var unchanged = state.ToJsonString();
                var transition = Transition(state, state, IdleAction(),
                    MainAttackScope(source, target, "c01.border-blade", Strike("attack", aid, did)));
                var published = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => published.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    var actor = Actor(board, did);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    var contact = actor.GetMeta("logical_contact").AsVector2();
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    await Tick(board, .074999);
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is null,
                        "Recipient recoil and impact cannot begin during the source's anticipation.");
                    await Tick(board, .000002);
                    for (var index = 0; index < starts.Length; index++)
                    {
                        if (index > 0)
                            await Tick(board, (starts[index] - starts[index - 1]) / 1000.0);
                        CheckClipSprite(sprite, clip.Asset, index, sign);
                        CheckClipShadow(sprite);
                        Require(actor.Position == origin && actor.Scale == Vector2.One
                            && actor.SelfModulate == Colors.White
                            && actor.GetMeta("logical_contact").AsVector2() == contact,
                            "Authored recoil articulates the picture without translating, shrinking or fading the recipient root.");
                        Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && actor.GetNodeOrNull("ActionAccent") is null,
                            "Recoil shares the existing impact and never creates an outgoing-action accent.");
                    }
                    await Tick(board, .080);
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    CheckClipShadow(sprite);
                    Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                        "Recoil resumes breathing at 320 ms while the independent 420 ms impact remains alive.");
                    await Tick(board, .100);
                    Require(task.IsCompleted && await task && !board.IsPresenting
                        && board.GetNodeOrNull("BattleEffects") is null
                        && published.SequenceEqual(Enumerable.Range(0, transition.Events.Count))
                        && state.ToJsonString() == unchanged,
                        "The recoil adds no impact delay, publishes each scoped receipt once and changes no battle state.");
                    CheckSettledClipActor(board, did, idle.Asset, origin);
                }
                finally { board.EventPresented -= Presented; }

                foreach (var seconds in new[] { .09, .29 })
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, seconds);
                    var origin = Actor(board, did).Position;
                    board.CancelPresentation();
                    Require(!await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                        "Cancellation during recoil onset or recovery settles without a surviving impact.");
                    CheckSettledClipActor(board, did, idle.Asset, origin);
                    await Tick(board, .450);
                    CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, sign);
                }
                var coarse = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, 1);
                Require(coarse.IsCompleted && await coarse && !board.IsPresenting,
                    "One coarse tick crosses source release, every recoil pose and the independent impact endpoint.");
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, sign);
                board.AnimationsEnabled = false;
                var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
                Require(reduced.IsCompleted && await reduced && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                    "Reduced motion settles the same committed hit synchronously without stale recoil ownership.");
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, sign);
                board.AnimationsEnabled = true;
            }
            await CheckTorrenRecoilNonDamage(board, state, aid, did, idle);
            await CheckTorrenRecoilDeathHandoff(board, state, aid, did, clip, idle);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Torren recoil: four production poses, 320 ms reaction, independent impact, mirrors/contact, idle/death handoffs and cancellation/reduced motion passed.");
    }

    private async Task CheckTorrenRecoilNonDamage(BoardView board, JsonObject state, int aid, int did, ActorClip idle)
    {
        foreach (var healing in new[] { false, true })
        {
            var fixture = (JsonObject)state.DeepClone();
            var source = fixture["units"]![1]!.AsObject();
            var target = fixture["units"]![0]!.AsObject();
            var zero = Strike("attack", aid, did);
            zero["damage"] = 0;
            if (healing)
                source["type"] = "c01.gravekeeper";
            var events = healing ? new[] { HealingEvent(aid, did) }
                : MainAttackScope(source, target, "c01.border-blade", zero);
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(fixture, fixture, IdleAction(), events));
            await Tick(board, .08);
            var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
            var sign = sprite.Scale.X < 0 ? -1 : 1;
            foreach (var step in new[] { 0.0, .04, .11, .09, .08 })
            {
                await Tick(board, step);
                CheckClipSprite(sprite, idle.Asset, 0, sign);
                Require(board.GetNodeOrNull(healing ? "BattleEffects/Heal" : "BattleEffects/Hit") is not null,
                    "Healing and zero-damage feedback remain visible without acquiring recipient recoil ownership.");
            }
            await Drain(board, task);
        }
    }

    private async Task CheckTorrenRecoilDeathHandoff(BoardView board, JsonObject state, int aid, int did, ActorClip recoil, ActorClip idle)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]!.AsArray().RemoveAt(0);
        var source = state["units"]![1]!.AsObject();
        var target = state["units"]![0]!.AsObject();
        var strike = Strike("attack", aid, did);
        strike["killed"] = true;
        var events = MainAttackScope(source, target, "c01.border-blade", strike,
            new JsonObject { ["type"] = "death", ["unit"] = did, ["at"] = At(Int(target, "x"), Int(target, "y")) });
        var transition = Transition(state, after, IdleAction(), events);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .075001);
            var actor = Actor(board, did);
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var sign = sprite.Scale.X < 0 ? -1 : 1;
            CheckClipSprite(sprite, recoil.Asset, 0, sign);
            await Tick(board, .320);
            CheckClipSprite(sprite, idle.Asset, 0, sign);
            await Tick(board, .139998);
            CheckClipSprite(sprite, idle.Asset, 0, sign);
            Require(actor.SelfModulate == Colors.White && actor.Scale == Vector2.One
                && published.SequenceEqual(new[] { 0 }),
                "A fatal flag keeps recoil and the fatal beat separate; it cannot start death before the ordered death event.");
            await Tick(board, .000002);
            var death = assets.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds, "campaign-torren");
            CheckClipSprite(sprite, death.Asset, death.FrameAt(0), sign);
            Require(death.Length == 220 && death.RequireDeath().Mode == ActorDeathMode.DuringClip
                && published.SequenceEqual(new[] { 0, 1 }) && !task.IsCompleted,
                "Only the ordered death event acquires Torren's unchanged 220 ms terminal presentation.");
            await Tick(board, .220);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
                && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                "Fatal recoil hands off to death once and completes every original scoped receipt in order.");
            await Tick(board, 2);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
                "Breathing and recoil cannot resurrect a retired Torren actor.");
        }
        finally { board.EventPresented -= Presented; }
    }
}
