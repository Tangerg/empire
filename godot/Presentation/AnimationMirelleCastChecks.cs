#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckMirelleCastPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.mirelle", "attack", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.mirelle", "idle", ActorClipUnit.Milliseconds);
        Require(idle.Asset.Identity == "mirelle-stage01" && clip.Asset.Identity == "mirelle-cast-v1",
            "Mirelle's channel returns to her own resting appearance, not the generic gravekeeper.");
        var starts = new[] { 0.0, 90, 190, 300, 400, 500 };
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt(frame == 5 ? 599.999 : starts[frame + 1] - .001) == frame,
                "Each personal cast pose owns its declared half-open interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Mirelle's six extracted poses retain transparent padding around hands, lantern and boots.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && !clip.ShowAccent, "Six different anatomical poses need no source accent substitute.");
        var state = ReleaseState();
        var source = state["units"]![0]!.AsObject();
        source["type"] = "c01.mirelle";
        var target = state["units"]![1]!.AsObject();
        var aid = Int(source, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var x in new[] { 11, 13 })
                foreach (var kind in new[] { "attack", "heal" })
                {
                    target["x"] = x;
                    var unchanged = state.ToJsonString();
                    var transition = Transition(state, state, IdleAction(), GravekeeperEvents(kind, source, target));
                    var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                    var actor = Actor(board, aid);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    for (var frame = 0; frame < starts.Length; frame++)
                    {
                        if (frame > 0)
                            await Tick(board, (starts[frame] - starts[frame - 1]) / 1000);
                        CheckClipSprite(sprite, clip.Asset, frame, x < 12 ? -1 : 1);
                        CheckClipShadow(sprite);
                        Require(actor.Position == origin && actor.GetNodeOrNull("ActionAccent") is null,
                            "Mirelle's planted casting keeps the contact root fixed without a procedural source accent.");
                        Require((board.GetNodeOrNull(kind == "heal" ? "BattleEffects/Heal" : "BattleEffects/Hit") is not null) == (frame >= 3),
                            "Attack and healing release recipient feedback only at the personal release pose.");
                    }
                    await Drain(board, playing);
                    Require(state.ToJsonString() == unchanged, "Personal casting never modifies canonical combat state.");
                    foreach (var seconds in new[] { .19, .4 })
                    {
                        playing = board.PresentTransitionAsync(viewportFeedback, transition);
                        await Tick(board, seconds);
                        board.CancelPresentation();
                        Require(!await playing && board.GetNodeOrNull("BattleEffects") is null, "Cancellation clears personal wind-up and released feedback.");
                        sprite = Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite");
                        CheckClipSprite(sprite, idle.Asset, idle.FrameAt(0), sprite.Scale.X < 0 ? -1 : 1);
                    }
                    board.AnimationsEnabled = false;
                    Require(await board.PresentTransitionAsync(viewportFeedback, transition) && !board.IsPresenting,
                        "Reduced motion settles the same personal action without a delayed pose.");
                    board.AnimationsEnabled = true;
                }
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Mirelle cast: six personal poses, attack/heal, mirrors, cancellation and reduced motion passed.");
    }
}
