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
    private async Task CheckTorrenIdlePlayback()
    {
        var clip = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var original = assets.ResolveUnit("c01.swordsman", "campaign-torren");
        var starts = new[] { 0.0, 440, 660, 900, 1340, 1580 };
        var frames = new[] { 0, 1, 2, 3, 2, 1 };
        var pictures = new HashSet<string>();
        for (var index = 0; index < 4; index++)
        {
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Every authored breathing picture retains its full padded silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 4 && clip.Asset != original && clip.ReleaseMs is null && clip.Death is null,
            "Four distinct frame images belong to the declared idle clip without a finite release or death policy.");
        for (var index = 0; index < starts.Length; index++)
            Require(clip.FrameAt(starts[index]) == frames[index]
                && clip.FrameAt(index == 5 ? 1799.999 : starts[index + 1] - .001) == frames[index],
                "The shipped breath uses six half-open intervals and reverses through its authored intermediate poses.");

        var state = ReleaseState();
        var unit = state["units"]![0]!.AsObject();
        unit["key"] = "campaign-torren";
        var id = Int(unit, "id");
        var board = NewActorClipBoard(assets);
        var presented = 0;
        void Presented(BattlePresentationTransition _, int index) => presented++;
        board.EventPresented += Presented;
        try
        {
            foreach (var facing in new[] { "west", "east" })
            {
                unit["facing"] = facing;
                board.RenderState(state);
                var unchanged = state.ToJsonString();
                var actor = Actor(board, id);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var sign = facing == "west" ? -1 : 1;
                var contact = actor.GetMeta("logical_contact").AsVector2();
                var position = actor.Position;
                for (var interval = 0; interval < starts.Length; interval++)
                {
                    if (interval > 0)
                        await Tick(board, (starts[interval] - starts[interval - 1]) / 1000.0);
                    CheckClipSprite(sprite, clip.Asset, frames[interval], sign);
                    CheckClipShadow(sprite);
                    Require(actor.Position == position && actor.Scale == Vector2.One
                        && actor.GetMeta("logical_contact").AsVector2() == contact,
                        "Breathing changes authored anatomy without moving the support plane, root or body scale.");
                }
                await Tick(board, .220);
                CheckClipSprite(sprite, clip.Asset, 0, sign);
                var texture = sprite.Texture!.GetInstanceId();
                await Tick(board, .100);
                Require(sprite.Texture!.GetInstanceId() == texture,
                    "A held breathing picture does not replace its cached texture.");
                await Tick(board, 18.8);
                CheckClipSprite(sprite, clip.Asset, 3, sign);
                board.Hide();
                await Tick(board, 10);
                board.Show();
                CheckClipSprite(sprite, clip.Asset, 3, sign);
                board.AnimationsEnabled = false;
                CheckClipSprite(sprite, clip.Asset, 0, sign);
                await Tick(board, 10);
                CheckClipSprite(sprite, clip.Asset, 0, sign);
                board.AnimationsEnabled = true;
                await Tick(board, .441);
                CheckClipSprite(sprite, clip.Asset, 1, sign);
                Require(!board.IsPresenting && presented == 0 && state.ToJsonString() == unchanged
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Repeated, hidden and reduced-motion breathing never gates input, publishes events or mutates battle data.");
            }
            await CheckTorrenIdleHandoffs(board, state, id, clip, original);
        }
        finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
        GD.Print("Torren idle: four authored pictures, six intervals, 1800 ms loop, mirrors/contact and finite handoffs passed.");
    }

    private async Task CheckTorrenIdleHandoffs(BoardView board, JsonObject state, int id, ActorClip idle, RuntimeAsset original)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]![0]!["x"] = 9;
        var path = new JsonArray(At(12, 16), At(11, 16), At(10, 16), At(9, 16));
        var action = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = id,
            ["path"] = path.DeepClone(),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        };
        var movement = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, action,
            [new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path }]));
        await Tick(board, .450);
        Require(!movement.IsCompleted, "Movement retains pose ownership beyond the first 440 ms idle boundary.");
        var sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 1, -1);
        await Drain(board, movement);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, idle.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Tick(board, .7);
        CheckClipSprite(sprite, idle.Asset, 2, sprite.Scale.X < 0 ? -1 : 1);

        var attacker = state["units"]![1]!.AsObject();
        var hit = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            MainAttackScope(attacker, state["units"]![0]!.AsObject(), "c01.border-blade", Strike("attack", Int(attacker, "id"), id))));
        await Tick(board, .08);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Tick(board, .151);
        CheckClipSprite(sprite, idle.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Drain(board, hit);

        var afterDeath = (JsonObject)state.DeepClone();
        afterDeath["units"]!.AsArray().RemoveAt(0);
        var death = board.PresentTransitionAsync(viewportFeedback, Transition(state, afterDeath, IdleAction(),
            [new JsonObject { ["type"] = "death", ["unit"] = id, ["at"] = At(12, 16) }]));
        await Tick(board, .05);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Drain(board, death);
        await Tick(board, 2);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
            "The authored idle cannot overwrite death or resurrect its retired actor.");
    }
}
