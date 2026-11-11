#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckGravekeeperFallPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.gravekeeper", "death", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds);
        var recoil = assets.ResolveUnitClip("c01.gravekeeper", "hit", ActorClipUnit.Milliseconds);
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        var names = new[] { "fall-buckle", "fall-kneel", "fall-collapse", "fall-contact", "fall-settle", "fall-still" };
        Require(bindings.Units.Count + bindings.Actors.Count == 40
            && bindings.Units["c01.gravekeeper"].ActionClips["death"] == "gravekeeper.fall"
            && clip.Asset.Identity == "gravekeeper-fall-v1" && clip.Asset.TopicId == "C01-UNIT-GRAVEKEEPER"
            && clip.Asset.FrameCount == 6 && clip.Asset.FrameOrder.SequenceEqual(names)
            && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .1f)
            && clip.FrameIndices.SequenceEqual(Enumerable.Range(0, 6)) && clip.Length == 620 && clip.ReleaseMs is null
            && clip.RequireDeath().Mode == ActorDeathMode.AfterClip && clip.RequireDeath().TerminalFadeMs == 160,
            "The shared keeper selects six original falling poses at the exact common scale, then a separate 160 ms terminal fade.");
        Require(assets.ResolveUnit("c01.gravekeeper").Identity == "C01-FFV1-UNIT-GRAVEKEEPER"
            && bindings.Units["c01.gravekeeper"].AppearanceId is null,
            "Falling artwork does not replace the keeper's canonical gait or standing appearance.");
        foreach (var (action, expected) in new[] { ("idle", "gravekeeper.breathe"), ("move", "legacy.move"),
            ("attack", "gravekeeper.channel"), ("hit", "gravekeeper.recoil"), ("celebrate", "static.celebrate") })
            Require(bindings.Units["c01.gravekeeper"].ActionClips[action] == expected,
                "Keeper fall changes no non-death action binding.");
        foreach (var key in new[] { "field-cleric", "bridge-cleric" })
            Require(!bindings.Actors.ContainsKey(key)
                && ReferenceEquals(assets.ResolveUnitClip("c01.gravekeeper", "death", ActorClipUnit.Milliseconds, key), clip),
                "Named generic keepers inherit the same fall without a new actor override.");
        foreach (var type in new[] { "cleric", "c01.mirelle", "c01.druid" })
        {
            var other = assets.ResolveUnitClip(type, "death", ActorClipUnit.Milliseconds);
            Require(bindings.Units[type].ActionClips["death"] == "static.death"
                && other.Asset == assets.ResolveUnit(type) && other.Length == 220
                && other.RequireDeath().Mode == ActorDeathMode.DuringClip,
                "Mirelle and unrelated healer types retain their original static death art and policy.");
        }
        var starts = new[] { 0.0, 60, 130, 220, 330, 440 };
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt((frame == 5 ? 620 : starts[frame + 1]) - .001) == frame,
                "Each keeper fall pose occupies its exact half-open 60/70/90/110/110/180 ms interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 12 && used.Position.Y >= 12
                && used.End.X <= image.GetWidth() - 12 && used.End.Y <= image.GetHeight() - 12,
                "Each original keeper fall preserves its complete RGBA silhouette with twelve transparent padding pixels.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(620) == 5 && clip.FrameAt(10000) == 5,
            "Six distinct keeper drawings hold the final grounded picture without looping or resuming idle.");

        // Synthetic production-art receipts isolate ownership, not new legal battle commands.
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var plane in new BoardPlaneFactory[] { FacingPlane, BoardPlane.Dimetric })
            {
                var fixture = ConfigureFacingBoard(board, assets, new Square4Grid(), plane);
                fixture["units"] = new JsonArray(fixture["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
                var target = fixture["units"]![0]!.AsObject();
                target["type"] = "c01.gravekeeper";
                target.Remove("key");
                foreach (var facing in new[] { "west", "east" })
                {
                    target["facing"] = facing;
                    await CheckAuthoredFallBoundaries(board, fixture, Int(target, "id"), clip, facing == "west" ? -1 : 1);
                }
            }
            ConfigureActorClipBoard(board, assets);
            var state = ReleaseState();
            state["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
            state["units"]![0]!["type"] = "c01.gravekeeper";
            state["units"]![0]!["facing"] = "east";
            state["units"]![0]!.AsObject().Remove("key");
            state["units"]![1]!["type"] = "c01.swordsman";
            state["units"]![1]!.AsObject().Remove("key");
            var id = Int(state["units"]![0]!.AsObject(), "id");
            await CheckGravekeeperFallSources(board, state, clip, idle, recoil);
            await CheckGravekeeperFallDamageKinds(board, state, clip, idle, recoil);
            await CheckGravekeeperFallAreaOrder(board, state, clip, recoil);
            await CheckGravekeeperFallSourceRecovery(board, state, clip);
            await CheckAuthoredFallColor(board, state, id, clip);
            await CheckAuthoredFallScope(board, state, id, idle, recoil);
            await CheckAuthoredFallResult(board, state, id);
            await CheckAuthoredFallLifetime(board, state, id, idle);
            await CheckAuthoredFallVisibility(board, state, id, "c01.gravekeeper", null);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Gravekeeper fall: six original poses, grounded hold and 620+160 ms phases, both projections/facings, source/receipt/result ownership and retirement lifetimes passed.");
    }

    private async Task CheckGravekeeperFallSources(BoardView board, JsonObject original, ActorClip fall, ActorClip idle, ActorClip recoil)
    {
        foreach (var (type, weapon, release, recovery) in new[] {
            ("c01.swordsman", "c01.border-blade", 75, 150),
            ("c01.archer", "c01.ranger-bow", 320, 600),
            ("c01.gravekeeper", "c01.lantern-staff", 300, 600) })
            foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
            {
                var state = (JsonObject)original.DeepClone();
                var target = state["units"]![0]!.AsObject();
                var source = state["units"]![1]!.AsObject();
                source["type"] = type;
                source["facing"] = "west";
                var aid = Int(source, "id");
                var did = Int(target, "id");
                var strike = ReleaseStrike(kind, aid, did, weapon);
                strike["killed"] = true;
                if (kind == "partingShot")
                    strike["at"] = At(Int(target, "x"), Int(target, "y"));
                var body = new[] { strike, ReactionDeathEvent(did), ReactionDeathEvent(did) };
                var events = kind == "attack" ? MainAttackScope(source, target, weapon, body) : body;
                var activationCount = kind == "attack" ? 1 : 0;
                var unchanged = state.ToJsonString();
                var published = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => published.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var playing = board.PresentTransitionAsync(viewportFeedback,
                        Transition(state, WithoutDeathActor(state, did), IdleAction(), events));
                    var actor = Actor(board, did);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    var sourceOrigin = Actor(board, aid).Position;
                    var exits = 0;
                    actor.TreeExiting += () => exits++;
                    var elapsed = 0.0;
                    async Task AdvanceTo(double milliseconds)
                    {
                        await Tick(board, (milliseconds - elapsed) / 1000);
                        elapsed = milliseconds;
                    }
                    await AdvanceTo(release - .001);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is null && published.Count == 0,
                        "The new keeper fall cannot advance the incoming source's declared release.");
                    await AdvanceTo(release);
                    CheckClipSprite(sprite, recoil.Asset, 0, 1);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is not null
                        && published.SequenceEqual(Enumerable.Range(0, activationCount)),
                        "Incoming damage starts the existing recoil at source release, not the death clip.");
                    await AdvanceTo(recovery);
                    CheckSettledClipActor(board, aid, assets.ResolveUnitClip(type, "idle", ActorClipUnit.Milliseconds).Asset, sourceOrigin);
                    await AdvanceTo(release + 320);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await AdvanceTo(release + 420);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is null
                        && published.SequenceEqual(Enumerable.Range(0, activationCount)),
                        "The impact ends after 420 ms while its separate fatal beat still owns the strike receipt.");
                    await AdvanceTo(release + 459.999);
                    Require(sprite.GetMeta("art_identity").AsString() == idle.Asset.Identity && actor.SelfModulate == Colors.White,
                        "Neither a fatal flag nor the final snapshot can start falling before the ordered death event.");
                    var deathStart = release + 460;
                    await AdvanceTo(deathStart);
                    CheckClipSprite(sprite, fall.Asset, 0, 1);
                    Require(published.SequenceEqual(Enumerable.Range(0, activationCount + 1)) && exits == 0,
                        "Only the ordered death event takes the keeper pose after the unchanged impact and fatal beat.");
                    await AdvanceTo(deathStart + 439.999);
                    CheckClipSprite(sprite, fall.Asset, 4, 1);
                    await AdvanceTo(deathStart + 440.001);
                    CheckClipSprite(sprite, fall.Asset, 5, 1);
                    Require(actor.SelfModulate == Colors.White, "The grounded final picture begins its full opaque 180 ms hold.");
                    await AdvanceTo(deathStart + 620);
                    Require(actor.SelfModulate == Colors.White && actor.Scale == Vector2.One && actor.Position == origin,
                        "Falling never shrinks or moves the root and cannot fade before all six authored intervals finish.");
                    await AdvanceTo(deathStart + 700);
                    Require(Mathf.IsEqualApprox(actor.SelfModulate.A, .5f) && actor.Scale == Vector2.One && Frame(actor) == 5,
                        "Only the terminal fade changes opacity, holding the same grounded pose at half alpha.");
                    await AdvanceTo(deathStart + 779.999);
                    Require(!playing.IsCompleted && exits == 0, "No incoming source or duplicate receipt shortens the final fade.");
                    await AdvanceTo(deathStart + 780);
                    Require(playing.IsCompleted && await playing && exits == 1
                        && published.SequenceEqual(Enumerable.Range(0, events.Length)) && board.RenderedUnitCount == 1
                        && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
                        && board.GetNodeOrNull("BattleEffects") is null && state.ToJsonString() == unchanged,
                        "Generic, archer and magic sources retire the keeper at 1315/1560/1540 ms with every original receipt ordered once.");
                }
                finally { board.EventPresented -= Presented; }
            }
    }

    private async Task CheckGravekeeperFallDamageKinds(BoardView board, JsonObject state, ActorClip fall, ActorClip idle, ActorClip recoil)
    {
        var id = Int(state["units"]![0]!.AsObject(), "id");
        foreach (var (kind, deathStart) in new[] { ("collisionDamage", 460), ("statusTick", 420) })
        {
            var damage = ReactionDamage(kind, id);
            if (kind == "collisionDamage")
                damage["killed"] = true;
            var published = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => published.Add(index);
            board.EventPresented += Presented;
            try
            {
                var playing = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, WithoutDeathActor(state, id), IdleAction(), [damage, ReactionDeathEvent(id)]));
                CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
                await Tick(board, .320);
                CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
                await Tick(board, (deathStart - 320 - .001) / 1000);
                Require(published.Count == 0 && !playing.IsCompleted,
                    "Collision keeps its fatal beat; status damage does not invent one from hpAfter zero.");
                await Tick(board, .000001);
                CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), fall.Asset, 0, 1);
                Require(published.SequenceEqual(new[] { 0 }), "The explicit death starts at its own damage-kind boundary.");
                await Tick(board, .779999);
                Require(!playing.IsCompleted && Frame(Actor(board, id)) == 5, "Grounded fade keeps both terminal damage kinds pending.");
                await Tick(board, .000001);
                Require(playing.IsCompleted && await playing && published.SequenceEqual(new[] { 0, 1 })
                    && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                    "Collision and status death finish at 1240/1200 ms without adding or losing a fatal beat.");
            }
            finally { board.EventPresented -= Presented; }
        }
    }

    private async Task CheckGravekeeperFallAreaOrder(BoardView board, JsonObject original, ActorClip fall, ActorClip recoil)
    {
        var state = (JsonObject)original.DeepClone();
        var target = state["units"]![0]!.AsObject();
        var source = state["units"]![1]!.AsObject();
        var firstId = Int(target, "id");
        var second = (JsonObject)target.DeepClone();
        second["id"] = 1000;
        second["x"] = 11;
        state["units"]!.AsArray().Add(second);
        var after = WithoutDeathActor(WithoutDeathActor(state, firstId), 1000);
        var firstStrike = Strike("areaAttack", Int(source, "id"), firstId);
        var secondStrike = Strike("areaAttack", Int(source, "id"), 1000);
        firstStrike["killed"] = true;
        secondStrike["killed"] = true;
        var events = MainAttackScope(source, target, "c01.border-blade",
            firstStrike, ReactionDeathEvent(firstId), secondStrike, ReactionDeathEvent(1000));
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            await Tick(board, .535);
            CheckClipSprite(Actor(board, firstId).GetNode<Sprite2D>("AuthoredSprite"), fall.Asset, 0, 1);
            await Tick(board, .779999);
            Require(published.SequenceEqual(new[] { 0, 1 }) && board.RenderedUnitCount == 3,
                "The next area recipient waits through the first keeper's complete collapse and fade.");
            await Tick(board, .000001);
            Require(published.SequenceEqual(new[] { 0, 1, 2 }) && board.RenderedUnitCount == 2
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{firstId}") is null,
                "First retirement releases exactly the next area receipt, not a second source wind-up.");
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
            await Tick(board, .460);
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), fall.Asset, 0, 1);
            await Tick(board, .779999);
            Require(!playing.IsCompleted && published.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "The second keeper has its own full falling and fade owner.");
            await Tick(board, .000001);
            Require(playing.IsCompleted && await playing && published.SequenceEqual(Enumerable.Range(0, events.Length))
                && board.RenderedUnitCount == 1 && board.GetNodeOrNull("BattleEffects") is null,
                "Two ordered fatal area recipients settle once at 2555 ms with no skipped death or stale effects.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckGravekeeperFallSourceRecovery(BoardView board, JsonObject state, ActorClip fall)
    {
        var id = Int(state["units"]![0]!.AsObject(), "id");
        var cast = assets.ResolveUnitClip("c01.gravekeeper", "attack", ActorClipUnit.Milliseconds);
        var events = new[] { Activation(id, "c01.lantern-staff"), ReactionDeathEvent(id), ActivationCompleted(id, "c01.lantern-staff") };
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(), events));
            await Tick(board, .300);
            var actor = Actor(board, id);
            Require(published.SequenceEqual(new[] { 0 }) && actor.GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString() == cast.Asset.Identity,
                "A death receipt for the active caster cannot race its still-owned source recovery.");
            await Tick(board, .299999);
            Require(actor.GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString() == cast.Asset.Identity,
                "The full 600 ms cast still owns the source drawing before death can acquire it.");
            await Tick(board, .000001);
            CheckClipSprite(actor.GetNode<Sprite2D>("AuthoredSprite"), fall.Asset, 0, 1);
            await Tick(board, .779999);
            Require(!playing.IsCompleted && published.SequenceEqual(new[] { 0 }), "Cast recovery plus both death phases gate completion until 1380 ms.");
            await Tick(board, .000001);
            Require(playing.IsCompleted && await playing && published.SequenceEqual(new[] { 0, 1, 2 })
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "The ended cast cannot restore idle or resurrect its source after ordered death retirement.");
            await Tick(board, 5);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null, "No stale cast or fall callback survives terminal retirement.");
        }
        finally { board.EventPresented -= Presented; }
    }
}
