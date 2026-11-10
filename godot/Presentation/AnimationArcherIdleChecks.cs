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
    private async Task CheckArcherIdlePlayback()
    {
        var clip = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        var original = assets.ResolveUnit("c01.archer");
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units["c01.archer"].ActionClips["idle"] == "archer.breathe"
            && clip.Asset.Identity == "archer-breathe-v1" && clip.Asset.TopicId == "C01-UNIT-ARCHER"
            && clip.Length == 2200 && clip.Asset.FrameCount == 4
            && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .072f)
            && clip.Asset.FrameOrder.SequenceEqual(new[] { "breath-low", "inhale-low", "inhale-high", "breath-high" }),
            "C01 archer idle selects the exact authored breathing appearance, timing and uniform scale.");
        Require(!bindings.Actors.ContainsKey("campaign-elin")
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds, "campaign-elin"), clip)
            && assets.ResolveUnit("c01.archer", "campaign-elin") == original,
            "Elin inherits the C01 archer idle while retaining her canonical base appearance.");
        foreach (var type in new[] { "archer", "c01.bran" })
            Require(bindings.Units[type].ActionClips["idle"] == "static.idle"
                && assets.ResolveUnitClip(type, "idle", ActorClipUnit.Milliseconds).Asset == assets.ResolveUnit(type),
                "The base archer and Bran retain their static idle artwork.");
        var starts = new[] { 0.0, 500, 800, 1100, 1600, 1900 };
        var frames = new[] { 0, 1, 2, 3, 2, 1 };
        var pictures = new HashSet<string>();
        for (var index = 0; index < 4; index++)
        {
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Every authored breathing picture retains its full padded silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 4 && clip.Asset != original && clip.ReleaseMs is null && clip.Death is null,
            "Four distinct frame images belong to the declared idle clip without a finite release or death policy.");
        for (var index = 0; index < starts.Length; index++)
            Require(clip.FrameAt(starts[index]) == frames[index]
                && clip.FrameAt(index == 5 ? 2199.999 : starts[index + 1] - .001) == frames[index],
                "The shipped breath uses six half-open intervals and reverses through its authored intermediate poses.");

        var state = ReleaseState();
        var unit = state["units"]![0]!.AsObject();
        unit["type"] = "c01.archer";
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
                        await Tick(board, (starts[interval] - starts[interval - 1]) / 1000.0);
                    CheckClipSprite(sprite, clip.Asset, frames[interval], sign);
                    CheckClipShadow(sprite);
                    Require(actor.Position == position && actor.Scale == Vector2.One
                        && actor.GetMeta("logical_contact").AsVector2() == contact,
                        "Breathing changes authored anatomy without moving the support plane, root or body scale.");
                }
                await Tick(board, .300);
                CheckClipSprite(sprite, clip.Asset, 0, sign);
                var texture = sprite.Texture!.GetInstanceId();
                await Tick(board, .100);
                Require(sprite.Texture!.GetInstanceId() == texture,
                    "A held breathing picture does not replace its cached texture.");
                await Tick(board, 23.2);
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
                await Tick(board, .501);
                CheckClipSprite(sprite, clip.Asset, 1, sign);
                Require(!board.IsPresenting && presented == 0 && state.ToJsonString() == unchanged
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Repeated, hidden and reduced-motion breathing never gates input, publishes events or mutates battle data.");
            }
            await CheckArcherIdleShotOwnership(board, state, id);
            await CheckArcherIdleHandoffs(board, state, id, clip, original);
        }
        finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
        GD.Print("Archer idle: four authored pictures, six intervals, 2200 ms loop, mirrors/contact and finite handoffs passed.");
    }

    private void CheckArcherIdleActor(BoardView board, int id, Vector2 contact, double elapsed)
    {
        var actor = Actor(board, id);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var idle = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, idle.Asset, idle.FrameAt(elapsed % idle.Length), sprite.Scale.X < 0 ? -1 : 1);
        CheckClipShadow(sprite);
        Require(actor.Position == contact && actor.Scale == Vector2.One && actor.SelfModulate == Colors.White,
            "Settled archers keep canonical contact, scale and opacity while breathing advances.");
    }

    private async Task CheckArcherIdleShotOwnership(BoardView board, JsonObject state, int id)
    {
        var source = state["units"]![0]!.AsObject();
        var target = state["units"]![1]!.AsObject();
        var bystander = (JsonObject)source.DeepClone();
        bystander["id"] = 1000;
        bystander["x"] = 10;
        state["units"]!.AsArray().Add(bystander);
        var transition = Transition(state, state, IdleAction(), ArcherEvents("attack", source, target));
        var playing = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .501);
        var sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        var shot = assets.ResolveUnitClip("c01.archer", "attack", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, shot.Asset, 5, sprite.Scale.X < 0 ? -1 : 1);
        CheckArcherIdleActor(board, 1000, board.Projection.CellContact(new(10, 16)), 501);
        Require(!playing.IsCompleted, "The shot retains its finite pose across the first breathing boundary while another archer idles.");
        await Tick(board, .099);
        CheckArcherIdleActor(board, id, Actor(board, id).Position, 0);
        await Drain(board, playing);
        board.RenderState(state);
        await Tick(board, .801);
        RemoveChild(board);
        AddChild(board);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        var texture = sprite.Texture!.GetInstanceId();
        await Tick(board, 1);
        Require(sprite.Texture!.GetInstanceId() == texture && Frame(Actor(board, id)) == 2,
            "Tree exit retires authored breathing registrations, even after the board is reattached.");
        board.RenderState(state);
        CheckArcherIdleActor(board, id, Actor(board, id).Position, 0);
        await Tick(board, .501);
        CheckArcherIdleActor(board, id, Actor(board, id).Position, 501);
        ConfigureActorClipBoard(board, assets);
        board.RenderState(state);
        CheckArcherIdleActor(board, id, Actor(board, id).Position, 0);
        await Tick(board, .801);
        CheckArcherIdleActor(board, id, Actor(board, id).Position, 801);
        state["units"]!.AsArray().Remove(bystander);
    }

    private async Task CheckArcherIdleHandoffs(BoardView board, JsonObject state, int id, ActorClip idle, RuntimeAsset original)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]![0]!["x"] = 8;
        var path = new JsonArray(At(12, 16), At(11, 16), At(10, 16), At(9, 16), At(8, 16));
        var action = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = id,
            ["path"] = path.DeepClone(),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        };
        var movement = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, action,
            [new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path }]));
        await Tick(board, .510);
        Require(!movement.IsCompleted, "Movement retains pose ownership beyond the first 500 ms idle boundary.");
        var sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, original, 3, -1);
        await Drain(board, movement);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, idle.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Tick(board, .9);
        CheckClipSprite(sprite, idle.Asset, 2, sprite.Scale.X < 0 ? -1 : 1);

        var attacker = state["units"]![1]!.AsObject();
        var hit = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            MainAttackScope(attacker, state["units"]![0]!.AsObject(), "c01.border-blade", Strike("attack", Int(attacker, "id"), id))));
        await Tick(board, .08);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        var recoil = assets.ResolveUnitClip("c01.archer", "hit", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, recoil.Asset, recoil.FrameAt(5), sprite.Scale.X < 0 ? -1 : 1);
        await Tick(board, .316);
        CheckClipSprite(sprite, idle.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        await Drain(board, hit);

        var afterDeath = (JsonObject)state.DeepClone();
        afterDeath["units"]!.AsArray().RemoveAt(0);
        var death = board.PresentTransitionAsync(viewportFeedback, Transition(state, afterDeath, IdleAction(),
            [new JsonObject { ["type"] = "death", ["unit"] = id, ["at"] = At(12, 16) }]));
        await Tick(board, .05);
        sprite = Actor(board, id).GetNode<Sprite2D>("AuthoredSprite");
        var fall = assets.ResolveUnitClip("c01.archer", "death", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, fall.Asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        Require(Actor(board, id).SelfModulate == Colors.White
            && Actor(board, id).Scale == Vector2.One,
            "Death replaces breathing with an opaque authored fall without shrinking the root.");
        await Drain(board, death);
        await Tick(board, 2);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
            "The authored idle cannot overwrite death or resurrect its retired actor.");
    }
}
