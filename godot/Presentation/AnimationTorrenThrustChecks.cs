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
    private async Task CheckTorrenThrustPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds, "campaign-torren");
        var idle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var starts = new[] { 0.0, 65, 115, 160, 225, 295 };
        var pixels = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt(index == 5 ? 359.999 : starts[index + 1] - .001) == index,
                "The shipped Torren thrust samples six distinct half-open pose intervals.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each extracted pose retains transparent padding around its complete silhouette.");
            pixels.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pixels.Count == 6 && clip.FrameAt(360) == 5 && clip.RequireRelease() == starts[3] && !clip.ShowAccent,
            "Six different source poses release at full extension without an accent covering the authored arm and sword.");
        var state = ReleaseState();
        var source = state["units"]![0]!.AsObject();
        source["type"] = "c01.swordsman";
        source["key"] = "campaign-torren";
        var target = state["units"]![1]!.AsObject();
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var targetX in new[] { 11, 13 })
            {
                target["x"] = targetX;
                var unchanged = state.ToJsonString();
                var transition = Transition(state, state, IdleAction(),
                    ScopedClipAttack(Strike("attack", aid, did), At(12, 16), At(targetX, 16)));
                var observed = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    var actor = Actor(board, aid);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var facing = sprite.Scale.X < 0 ? -1 : 1;
                    Require(facing == (targetX < 12 ? -1 : 1),
                        "The authored thrust mirrors toward either side of the actor.");
                    var logical = actor.GetMeta("logical_contact").AsVector2();
                    for (var index = 0; index < starts.Length; index++)
                    {
                        if (index > 0)
                            await Tick(board, (starts[index] - starts[index - 1]) / 1000.0);
                        CheckClipSprite(sprite, clip.Asset, index, facing);
                        CheckClipShadow(sprite);
                        Require(actor.GetNodeOrNull("ActionAccent") is null,
                            "An authored clip's explicit accent choice prevents occlusion throughout the action.");
                        Require(actor.GetMeta("logical_contact").AsVector2() == logical,
                            "Authored limb motion never changes the canonical standing contact.");
                        Require((board.GetNodeOrNull("BattleEffects/Hit") is not null) == (index >= 3),
                            "The impact begins at the authored full-extension release, never during anticipation.");
                    }
                    await Tick(board, .066);
                    CheckClipSprite(sprite, idle.Asset, idle.FrameAt(0), sprite.Scale.X < 0 ? -1 : 1);
                    CheckClipShadow(sprite);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && !task.IsCompleted,
                        "The actor restores its declared first idle pose while the released impact finishes.");
                    await Drain(board, task);
                    Require(observed.SequenceEqual(Enumerable.Range(0, transition.Events.Count))
                        && state.ToJsonString() == unchanged,
                        "The authored clip presents every receipt once and never changes battle state.");
                }
                finally { board.EventPresented -= Presented; }

                foreach (var seconds in new[] { .09, .24 })
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, seconds);
                    board.CancelPresentation();
                    Require(!await task && !board.IsPresenting, "Cancellation stops Torren during wind-up or recovery.");
                    var sprite = Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite");
                    CheckClipSprite(sprite, idle.Asset, idle.FrameAt(0), sprite.Scale.X < 0 ? -1 : 1);
                    CheckClipShadow(sprite);
                }
                var skipped = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, 1);
                Require(await skipped && !board.IsPresenting,
                    "A coarse frame can cross every authored pose and release without stranding playback.");
                board.AnimationsEnabled = false;
                Require(await board.PresentTransitionAsync(viewportFeedback, transition) && !board.IsPresenting,
                    "Reduced motion settles the same committed action without replaying the authored clip.");
                board.AnimationsEnabled = true;
            }
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Torren thrust: six production poses, 160 ms release, 360 ms recovery, mirrors/cancellation/reduced motion passed.");
    }
}
