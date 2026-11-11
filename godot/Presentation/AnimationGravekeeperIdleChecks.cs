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
    private async Task CheckGravekeeperIdlePlayback()
    {
        var clip = assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds);
        var original = assets.ResolveUnit("c01.gravekeeper");
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units["c01.gravekeeper"].ActionClips["idle"] == "gravekeeper.breathe"
            && clip.Asset.Identity == "gravekeeper-breathe-v1" && clip.Asset.TopicId == "C01-UNIT-GRAVEKEEPER"
            && clip.Length == 2400 && clip.Asset.FrameCount == 4
            && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .0792f)
            && clip.Asset.FrameOrder.SequenceEqual(new[] { "breathe-rest", "breathe-inhale", "breathe-high", "breathe-exhale" })
            && original.Identity == "C01-FFV1-UNIT-GRAVEKEEPER",
            "Only the generic keeper idle selects the exact four-pose, 2400 ms breathing appearance and uniform scale.");
        foreach (var key in new[] { "field-cleric", "bridge-cleric" })
            Require(!bindings.Actors.ContainsKey(key)
                && ReferenceEquals(assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds, key), clip)
                && assets.ResolveUnit("c01.gravekeeper", key) == original,
                "Named generic keepers inherit the declared idle without replacing their canonical appearance or adding overrides.");
        foreach (var type in new[] { "cleric", "c01.mirelle", "c01.druid" })
            Require(bindings.Units[type].ActionClips["idle"] == "static.idle"
                && assets.ResolveUnitClip(type, "idle", ActorClipUnit.Milliseconds).Asset == assets.ResolveUnit(type),
                "Mirelle and other healers retain their own static idle appearances.");
        Require(assets.ResolveUnit("c01.mirelle").Identity == "mirelle-stage01",
            "The separate Mirelle appearance never inherits the male gravekeeper's breathing pictures.");
        var starts = new[] { 0.0, 700, 1200, 1700 };
        var pictures = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Every keeper breathing picture preserves its complete padded shovel, lantern, body and boots.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt(index == 3 ? 2399.999 : starts[index + 1] - .001) == index,
                "The four authored anatomical phases occupy their exact half-open intervals.");
        }
        Require(pictures.Count == 4 && clip.Asset != original && clip.ReleaseMs is null && clip.Death is null
            && clip.FrameAt(2400) == 3,
            "Four distinct idle images have no finite release/death policy; the board, not ActorClip, owns loop wrap.");

        var state = ReleaseState();
        var unit = state["units"]![0]!.AsObject();
        unit["type"] = "c01.gravekeeper";
        unit.Remove("key");
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
                        await Tick(board, (starts[interval] - starts[interval - 1]) / 1000);
                    CheckClipSprite(sprite, clip.Asset, interval, sign);
                    CheckClipShadow(sprite);
                    Require(actor.Position == position && actor.Scale == Vector2.One
                        && actor.GetMeta("logical_contact").AsVector2() == contact,
                        "Anatomical breathing leaves the support plane, root and body scale stationary in both facings.");
                }
                await Tick(board, .700);
                CheckGravekeeperIdleActor(board, id, position, 0);
                var texture = sprite.Texture!.GetInstanceId();
                await Tick(board, .100);
                Require(sprite.Texture!.GetInstanceId() == texture,
                    "A held breathing pose retains its cached texture.");
                await Tick(board, 25.2);
                CheckGravekeeperIdleActor(board, id, position, 1300);
                board.Hide();
                await Tick(board, 10);
                board.Show();
                CheckGravekeeperIdleActor(board, id, position, 1300);
                board.AnimationsEnabled = false;
                CheckGravekeeperIdleActor(board, id, position, 0);
                await Tick(board, 10);
                CheckGravekeeperIdleActor(board, id, position, 0);
                board.AnimationsEnabled = true;
                await Tick(board, .701);
                CheckGravekeeperIdleActor(board, id, position, 701);
                Require(!board.IsPresenting && presented == 0 && state.ToJsonString() == unchanged
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Repeated, hidden and reduced-motion breathing never gates input, publishes events or changes battle state.");
            }
            await CheckGravekeeperIdleCasting(board, state, id);
            await CheckGravekeeperIdleHandoffs(board, state, id, original);
        }
        finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
        GD.Print("Gravekeeper idle: four authored phases, 2400 ms loop, mirrors/contact and cast/movement/reaction lifetimes passed.");
    }

    private void CheckGravekeeperIdleActor(BoardView board, int id, Vector2 contact, double elapsed)
    {
        var actor = Actor(board, id);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var idle = assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, idle.Asset, idle.FrameAt(elapsed % idle.Length), sprite.Scale.X < 0 ? -1 : 1);
        CheckClipShadow(sprite);
        Require(actor.Position == contact && actor.Scale == Vector2.One && actor.SelfModulate == Colors.White,
            "Settled keepers preserve contact, body scale and opacity as the declared idle phase advances.");
    }

    private async Task CheckGravekeeperIdleCasting(BoardView board, JsonObject state, int id)
    {
        var source = state["units"]![0]!.AsObject();
        var target = state["units"]![1]!.AsObject();
        var bystander = (JsonObject)source.DeepClone();
        bystander["id"] = 1000;
        bystander["x"] = 10;
        state["units"]!.AsArray().Add(bystander);
        try
        {
            foreach (var targetX in new[] { 11, 13 })
                foreach (var kind in new[] { "attack", "heal" })
                {
                    target["x"] = targetX;
                    var playing = board.PresentTransitionAsync(viewportFeedback,
                        Transition(state, state, IdleAction(), GravekeeperEvents(kind, source, target)));
                    var sourceContact = Actor(board, id).Position;
                    var otherContact = Actor(board, 1000).Position;
                    await Tick(board, .500);
                    var sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
                    var cast = assets.ResolveUnitClip("c01.gravekeeper", "attack", ActorClipUnit.Milliseconds);
                    CheckClipSprite(sprite, cast.Asset, 5, targetX < 12 ? -1 : 1);
                    CheckGravekeeperIdleActor(board, 1000, otherContact, 500);
                    await Tick(board, .100);
                    CheckGravekeeperIdleActor(board, id, sourceContact, 0);
                    await Tick(board, .101);
                    CheckGravekeeperIdleActor(board, id, sourceContact, 101);
                    CheckGravekeeperIdleActor(board, 1000, otherContact, 701);
                    Require(!playing.IsCompleted,
                        "The source restarts breathing at 600 ms while its independent bystander's old phase advances before recipient feedback ends.");
                    await Drain(board, playing);
                }
        }
        finally { state["units"]!.AsArray().Remove(bystander); }
        board.RenderState(state);
        var position = Actor(board, id).Position;
        await Tick(board, 1.201);
        RemoveChild(board);
        AddChild(board);
        var retained = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite").Texture!.GetInstanceId();
        await Tick(board, 3);
        Require(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite").Texture!.GetInstanceId() == retained
            && Frame(Actor(board, id)) == 2,
            "Tree exit retires breathing registrations; reattachment alone cannot restart a stale idle clock.");
        board.RenderState(state);
        CheckGravekeeperIdleActor(board, id, position, 0);
        await Tick(board, .701);
        CheckGravekeeperIdleActor(board, id, position, 701);
        ConfigureActorClipBoard(board, assets);
        board.RenderState(state);
        CheckGravekeeperIdleActor(board, id, position, 0);
    }

    private async Task CheckGravekeeperIdleHandoffs(BoardView board, JsonObject state, int id, RuntimeAsset original)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]![0]!["x"] = 7;
        var path = new JsonArray(At(12, 16), At(11, 16), At(10, 16), At(9, 16), At(8, 16), At(7, 16));
        var action = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = id,
            ["path"] = path.DeepClone(),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        };
        var moving = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, action,
            [new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path }]));
        await Tick(board, .710);
        Require(!moving.IsCompleted, "Movement retains pose ownership beyond the first 700 ms breathing boundary.");
        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), original, 1, -1);
        await Drain(board, moving);
        CheckGravekeeperIdleActor(board, id, Actor(board, id).Position, 0);
        await Tick(board, 1.3);
        CheckGravekeeperIdleActor(board, id, Actor(board, id).Position, 1300);

        var attacker = state["units"]![1]!.AsObject();
        var hit = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            MainAttackScope(attacker, state["units"]![0]!.AsObject(), "c01.border-blade", Strike("attack", Int(attacker, "id"), id))));
        await Tick(board, .08);
        var sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        var recoil = assets.ResolveUnitClip("c01.gravekeeper", "hit", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, recoil.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Drain(board, hit);
        CheckGravekeeperIdleActor(board, id, Actor(board, id).Position, 0);

        var celebrating = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [CelebrationEvent(BattleConclusion.LastTeamRemaining(1))]));
        await Tick(board, .219999);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Tick(board, .000001);
        CheckGravekeeperIdleActor(board, id, Actor(board, id).Position, 0);
        await Tick(board, .701);
        CheckGravekeeperIdleActor(board, id, Actor(board, id).Position, 701);
        Require(!celebrating.IsCompleted,
            "The keeper's unchanged 220 ms celebration restores its own idle while longer archer celebrations still own the receipt.");
        await Drain(board, celebrating);

        var afterDeath = (JsonObject)state.DeepClone();
        afterDeath["units"]!.AsArray().RemoveAt(0);
        var death = board.PresentTransitionAsync(viewportFeedback, Transition(state, afterDeath, IdleAction(),
            [new JsonObject { ["type"] = "death", ["unit"] = id, ["at"] = At(12, 16) }]));
        await Tick(board, .05);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Drain(board, death);
        await Tick(board, 3);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
            "Breathing cannot replace the existing static death or resurrect a retired keeper.");
    }
}
