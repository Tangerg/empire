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
    private async Task CheckGravekeeperRecoilPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.gravekeeper", "hit", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds);
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units.Count + bindings.Actors.Count == 40
            && bindings.Units["c01.gravekeeper"].ActionClips["hit"] == "gravekeeper.recoil"
            && clip.Asset.Identity == "gravekeeper-recoil-v1" && clip.Asset.TopicId == "C01-UNIT-GRAVEKEEPER"
            && clip.Asset.FrameCount == 4 && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .0688f)
            && clip.Length == 320 && clip.ReleaseMs is null && clip.Death is null
            && clip.Asset.FrameOrder.SequenceEqual(new[] { "recoil-onset", "recoil-peak", "recoil-recover", "recoil-ready" }),
            "The shared keeper selects four original recoil poses at the exact uniform scale and 320 ms duration, without release or death policy.");
        Require(assets.ResolveUnit("c01.gravekeeper").Identity == "C01-FFV1-UNIT-GRAVEKEEPER"
            && bindings.Units["c01.gravekeeper"].AppearanceId is null
            && bindings.Units["c01.gravekeeper"].ActionClips["idle"] == "gravekeeper.breathe"
            && bindings.Units["c01.gravekeeper"].ActionClips["attack"] == "gravekeeper.channel"
            && bindings.Units["c01.gravekeeper"].ActionClips["move"] == "legacy.move"
            && bindings.Units["c01.gravekeeper"].ActionClips["death"] == "static.death"
            && bindings.Units["c01.gravekeeper"].ActionClips["celebrate"] == "static.celebrate",
            "Recipient recoil does not replace the keeper's canonical sheet or any other declared action.");
        foreach (var key in new[] { "field-cleric", "bridge-cleric" })
            Require(!bindings.Actors.ContainsKey(key)
                && ReferenceEquals(assets.ResolveUnitClip("c01.gravekeeper", "hit", ActorClipUnit.Milliseconds, key), clip),
                "Named generic keepers inherit recoil without a new appearance or actor override.");
        foreach (var type in new[] { "cleric", "c01.mirelle", "c01.druid" })
            Require(bindings.Units[type].ActionClips["hit"] == "static.hit"
                && assets.ResolveUnitClip(type, "hit", ActorClipUnit.Milliseconds).Asset == assets.ResolveUnit(type),
                "Mirelle and other healers retain their independent static reaction appearances.");
        var starts = new[] { 0.0, 40, 150, 240 };
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt((frame == 3 ? 320 : starts[frame + 1]) - .001) == frame,
                "Every keeper recoil pose occupies its exact half-open 40/110/90/80 ms interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 12 && used.Position.Y >= 12
                && used.End.X <= image.GetWidth() - 12 && used.End.Y <= image.GetHeight() - 12,
                "Each original keeper recoil preserves complete RGBA equipment, body and boots with twelve transparent padding pixels.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 4 && clip.FrameAt(320) == 3 && clip.FrameAt(10000) == 3,
            "Four distinct keeper recoil drawings hold their finite final frame instead of looping.");

        // Production pictures on synthetic receipts isolate presentation ownership, not battle legality.
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var plane in new BoardPlaneFactory[] { FacingPlane, BoardPlane.Dimetric })
            {
                var fixture = ConfigureFacingBoard(board, assets, new Square4Grid(), plane);
                fixture["units"]![0]!["type"] = "c01.gravekeeper";
                fixture["units"]![0]!.AsObject().Remove("key");
                fixture["units"]![1]!["type"] = "c01.swordsman";
                fixture["units"]![1]!.AsObject().Remove("key");
                await CheckGravekeeperRecoilTiming(board, fixture, clip, idle);
            }
            ConfigureActorClipBoard(board, assets);
            var state = ReleaseState();
            state["units"]![0]!["type"] = "c01.gravekeeper";
            state["units"]![0]!["facing"] = "east";
            state["units"]![0]!.AsObject().Remove("key");
            await CheckGravekeeperRecoilDamageScope(board, state, clip, idle);
            await CheckGravekeeperRecoilCounterOrder(board, state, clip, idle);
            await CheckGravekeeperRecoilAreaOrder(board, state, clip, idle);
            await CheckGravekeeperRecoilDeathHandoff(board, state, clip, idle);
            await CheckGravekeeperRecoilLifetime(board, state, clip, idle);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Gravekeeper recoil: four original poses, exact 320/420 ms ownership, both projections/facings, casting/counter/death order and interruption lifetimes passed.");
    }

    private async Task CheckGravekeeperRecoilTiming(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        var target = state["units"]![0]!.AsObject();
        var source = state["units"]![1]!.AsObject();
        var did = Int(target, "id");
        foreach (var facing in new[] { "west", "east" })
            foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
            {
                target["facing"] = facing;
                source["x"] = Int(target, "x") + (facing == "west" ? -1 : 1);
                source["y"] = Int(target, "y");
                var sign = facing == "west" ? -1 : 1;
                var strike = Strike(kind, Int(source, "id"), did);
                if (kind == "partingShot")
                    strike["at"] = At(Int(target, "x"), Int(target, "y"));
                var events = kind == "attack" ? MainAttackScope(source, target, "c01.border-blade", strike) : [strike];
                var transition = Transition(state, state, IdleAction(), events);
                var unchanged = state.ToJsonString();
                var eventBytes = new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString();
                var published = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => published.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                    var elapsed = 0.0;
                    async Task AdvanceTo(double milliseconds)
                    {
                        var seconds = milliseconds / 1000;
                        await Tick(board, seconds - elapsed);
                        elapsed = seconds;
                    }
                    var actor = Actor(board, did);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    var contact = actor.GetMeta("logical_contact").AsVector2();
                    await AdvanceTo(74.999);
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    Require(published.Count == 0 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                        "The keeper cannot recoil or publish damage before the incoming source's exact 75 ms release.");
                    await AdvanceTo(75);
                    var impact = board.GetNode<Node2D>("BattleEffects/Hit");
                    var ends = new[] { 115.0, 225, 315, 395 };
                    for (var frame = 0; frame < ends.Length; frame++)
                    {
                        CheckClipSprite(sprite, recoil.Asset, frame, sign);
                        CheckClipShadow(sprite);
                        Require(actor.Position == origin && impact.Position == origin && actor.Scale == Vector2.One
                            && actor.SelfModulate == Colors.White && actor.Modulate == Colors.White
                            && actor.GetMeta("logical_contact").AsVector2() == contact
                            && actor.GetNodeOrNull("ActionAccent") is null
                            && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit")),
                            "Anatomical keeper recoil changes only its frame, preserving root/contact, body scale, opacity, facing and the single original impact.");
                        await AdvanceTo(ends[frame] - .001);
                        CheckClipSprite(sprite, recoil.Asset, frame, sign);
                        await AdvanceTo(ends[frame]);
                    }
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    CheckClipShadow(sprite);
                    Require(!playing.IsCompleted && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit")),
                        "At release plus 320 ms breathing resumes while the original 420 ms impact still owns the receipt.");
                    await AdvanceTo(494.999);
                    Require(!playing.IsCompleted && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit"))
                        && published.SequenceEqual(kind == "attack" ? new[] { 0 } : Array.Empty<int>()),
                        "New keeper artwork cannot shorten the incoming impact or publish an unfinished damage event.");
                    await AdvanceTo(495);
                    Require(playing.IsCompleted && await playing && !board.IsPresenting
                        && board.GetNodeOrNull("BattleEffects") is null
                        && published.SequenceEqual(Enumerable.Range(0, events.Length)) && state.ToJsonString() == unchanged
                        && new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString() == eventBytes,
                        "At 495 ms all original events publish once with unchanged state/event bytes and no added command tail.");
                    CheckSettledClipActor(board, did, idle.Asset, origin);
                }
                finally { board.EventPresented -= Presented; }
            }
    }

    private async Task CheckGravekeeperRecoilDamageScope(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        var target = state["units"]![0]!.AsObject();
        var source = state["units"]![1]!.AsObject();
        var did = Int(target, "id");
        foreach (var kind in new[] { "collisionDamage", "statusTick" })
            foreach (var amount in new[] { 0.0, 12.0 })
            {
                var playing = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDamage(kind, did, amount)]));
                var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
                CheckClipSprite(sprite, amount > 0 ? recoil.Asset : idle.Asset, 0, 1);
                await Tick(board, .150);
                CheckClipSprite(sprite, amount > 0 ? recoil.Asset : idle.Asset, amount > 0 ? 2 : 0, 1);
                await Tick(board, .170);
                CheckClipSprite(sprite, idle.Asset, 0, 1);
                Require(!playing.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "Only positive collision/status damage acquires keeper recoil; independent impact feedback remains alive through 420 ms.");
                await Tick(board, .100);
                Require(playing.IsCompleted && await playing && Actor(board, did).Scale == Vector2.One,
                    "A status hpAfter value alone cannot synthesize death or extend the recipient impact.");
            }
        foreach (var kind in new[] { "attack", "heal", "regen", "magic" })
        {
            var fixture = (JsonObject)state.DeepClone();
            var caster = fixture["units"]![1]!.AsObject();
            var victim = fixture["units"]![0]!.AsObject();
            caster["type"] = kind == "attack" ? "c01.swordsman" : "c01.gravekeeper";
            caster["facing"] = "west";
            caster.Remove("key");
            var zero = Strike("attack", Int(caster, "id"), did);
            zero["damage"] = 0;
            var events = kind is "heal" or "magic" ? GravekeeperEvents(kind == "magic" ? "attack" : "heal", caster, victim)
                : kind == "regen" ? [new JsonObject { ["type"] = "regen", ["unit"] = did, ["amount"] = 12 }]
                : MainAttackScope(caster, victim, "c01.border-blade", zero);
            var playing = board.PresentTransitionAsync(viewportFeedback, Transition(fixture, fixture, IdleAction(), events));
            var release = kind is "heal" or "magic" ? .300 : kind == "attack" ? .075 : 0;
            if (release > 0)
            {
                await Tick(board, release - .000001);
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
                await Tick(board, .000001);
            }
            var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
            CheckClipSprite(sprite, kind == "magic" ? recoil.Asset : idle.Asset, 0, 1);
            await Tick(board, .300);
            CheckClipSprite(sprite, kind == "magic" ? recoil.Asset : idle.Asset, kind == "magic" ? 3 : 0, 1);
            if (kind is "heal" or "magic")
                CheckClipSprite(Actor(board, Int(caster, "id")).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, -1);
            await Tick(board, .020);
            CheckClipSprite(sprite, idle.Asset, 0, 1);
            Require(!playing.IsCompleted && board.GetNodeOrNull(kind is "attack" or "magic" ? "BattleEffects/Hit" : "BattleEffects/Heal") is not null,
                "Magic damage uses the same hit owner; zero damage, healing and regeneration keep feedback without borrowing recoil.");
            await Drain(board, playing);
        }
    }

    private async Task CheckGravekeeperRecoilCounterOrder(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        var keeper = state["units"]![0]!.AsObject();
        var other = state["units"]![1]!.AsObject();
        var did = Int(keeper, "id");
        var cast = assets.ResolveUnitClip("c01.gravekeeper", "attack", ActorClipUnit.Milliseconds);
        foreach (var keeperStarts in new[] { false, true })
        {
            var attacker = keeperStarts ? keeper : other;
            var defender = keeperStarts ? other : keeper;
            var weapon = keeperStarts ? "c01.lantern-staff" : "c01.border-blade";
            var counter = keeperStarts ? "c01.border-blade" : "c01.lantern-staff";
            var events = MainAttackScope(attacker, defender, weapon,
                ReleaseStrike("attack", Int(attacker, "id"), Int(defender, "id"), weapon),
                ReleaseStrike("counter", Int(defender, "id"), Int(attacker, "id"), counter));
            var unchanged = state.ToJsonString();
            var published = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => published.Add(index);
            board.EventPresented += Presented;
            try
            {
                var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
                var elapsed = 0.0;
                async Task AdvanceTo(double milliseconds)
                {
                    var seconds = milliseconds / 1000;
                    await Tick(board, seconds - elapsed);
                    elapsed = seconds;
                }
                var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
                if (keeperStarts)
                {
                    await AdvanceTo(599.999);
                    CheckClipSprite(sprite, cast.Asset, 5, 1);
                    await AdvanceTo(600);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await AdvanceTo(719.999);
                    Require(published.SequenceEqual(new[] { 0 }), "The counter waits for the keeper's original 720 ms magic impact boundary.");
                    await AdvanceTo(720);
                    Require(published.SequenceEqual(new[] { 0, 1 }) && board.GetNodeOrNull("BattleEffects/Hit") is null,
                        "The generic counter starts after the original magic receipt and before its own release.");
                    await AdvanceTo(794.999);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await AdvanceTo(795);
                    CheckClipSprite(sprite, recoil.Asset, 0, 1);
                    await AdvanceTo(1115);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                }
                else
                {
                    await AdvanceTo(75);
                    CheckClipSprite(sprite, recoil.Asset, 0, 1);
                    await AdvanceTo(395);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await AdvanceTo(494.999);
                    Require(published.SequenceEqual(new[] { 0 }), "Keeper casting cannot steal the incoming impact's 100 ms tail after recoil.");
                    await AdvanceTo(495);
                    CheckClipSprite(sprite, cast.Asset, 0, 1);
                    Require(published.SequenceEqual(new[] { 0, 1 }) && board.GetNodeOrNull("BattleEffects/Hit") is null,
                        "At 495 ms the keeper's unchanged counter channel acquires the source pose.");
                    await AdvanceTo(794.999);
                    CheckClipSprite(sprite, cast.Asset, 2, 1);
                    await AdvanceTo(795);
                    CheckClipSprite(sprite, cast.Asset, 3, 1);
                    await AdvanceTo(1095);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                }
                await AdvanceTo(1214.999);
                Require(!playing.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "Both keeper attack/counter orders preserve the final impact through 1214.999 ms.");
                await AdvanceTo(1215);
                Require(playing.IsCompleted && await playing && published.SequenceEqual(Enumerable.Range(0, events.Length))
                    && board.GetNodeOrNull("BattleEffects") is null && state.ToJsonString() == unchanged,
                    "At 1215 ms both attack/counter orders settle once without changing the existing release or damage facts.");
            }
            finally { board.EventPresented -= Presented; }
        }
    }

    private async Task CheckGravekeeperRecoilAreaOrder(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        foreach (var sameRecipient in new[] { false, true })
        {
            var fixture = (JsonObject)state.DeepClone();
            var target = fixture["units"]![0]!.AsObject();
            var source = fixture["units"]![1]!.AsObject();
            var did = Int(target, "id");
            var secondId = sameRecipient ? did : 1000;
            if (!sameRecipient)
            {
                var second = (JsonObject)target.DeepClone();
                second["id"] = secondId;
                second["x"] = 10;
                fixture["units"]!.AsArray().Add(second);
            }
            var events = MainAttackScope(source, target, "c01.border-blade",
                Strike("areaAttack", Int(source, "id"), did), Strike("areaAttack", Int(source, "id"), secondId));
            var published = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => published.Add(index);
            board.EventPresented += Presented;
            try
            {
                var playing = board.PresentTransitionAsync(viewportFeedback, Transition(fixture, fixture, IdleAction(), events));
                await Tick(board, .075);
                var first = board.GetNode<Node2D>("BattleEffects/Hit");
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
                await Tick(board, .320);
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
                await Tick(board, .099999);
                Require(published.SequenceEqual(new[] { 0 }) && ReferenceEquals(first, board.GetNodeOrNull("BattleEffects/Hit")),
                    "The next area receipt cannot restart either the same or a second keeper before the first impact finishes.");
                await Tick(board, .000001);
                CheckClipSprite(Actor(board, secondId).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
                Require(published.SequenceEqual(new[] { 0, 1 }) && !ReferenceEquals(first, board.GetNodeOrNull("BattleEffects/Hit")),
                    "The next committed area hit starts a fresh pose zero and impact without another source wind-up.");
                await Tick(board, .320);
                CheckClipSprite(Actor(board, secondId).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
                await Tick(board, .099999);
                Require(!playing.IsCompleted, "Two area hits retain their original 915 ms receipt endpoint.");
                await Tick(board, .000002);
                Require(playing.IsCompleted && await playing && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                    "Repeated and distinct keeper recipients publish all original area events once and in order.");
            }
            finally { board.EventPresented -= Presented; }
        }
    }

    private async Task CheckGravekeeperRecoilDeathHandoff(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        var target = state["units"]![0]!.AsObject();
        var source = state["units"]![1]!.AsObject();
        var did = Int(target, "id");
        var after = (JsonObject)state.DeepClone();
        after["units"]!.AsArray().RemoveAt(0);
        var strike = Strike("attack", Int(source, "id"), did);
        strike["killed"] = true;
        var events = MainAttackScope(source, target, "c01.border-blade", strike, ReactionDeathEvent(did), ReactionDeathEvent(did));
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            var elapsed = 0.0;
            async Task AdvanceTo(double milliseconds)
            {
                var seconds = milliseconds / 1000;
                await Tick(board, seconds - elapsed);
                elapsed = seconds;
            }
            var actor = Actor(board, did);
            var exits = 0;
            actor.TreeExiting += () => exits++;
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            await AdvanceTo(75);
            CheckClipSprite(sprite, recoil.Asset, 0, 1);
            await AdvanceTo(395);
            CheckClipSprite(sprite, idle.Asset, 0, 1);
            await AdvanceTo(534.999);
            Require(actor.Scale == Vector2.One && actor.SelfModulate == Colors.White && published.SequenceEqual(new[] { 0 }),
                "A fatal flag cannot start death before the full 420 ms impact and separate 40 ms fatal beat.");
            await AdvanceTo(535);
            var death = assets.ResolveUnitClip("c01.gravekeeper", "death", ActorClipUnit.Milliseconds);
            CheckClipSprite(sprite, death.Asset, 0, 1);
            Require(death.Asset == assets.ResolveUnit("c01.gravekeeper") && death.Length == 220
                && death.RequireDeath().Mode == ActorDeathMode.DuringClip && published.SequenceEqual(new[] { 0, 1 }),
                "Only the ordered death event acquires the unchanged canonical keeper death policy.");
            await AdvanceTo(645);
            Require(Mathf.IsEqualApprox(actor.SelfModulate.A, .5f) && actor.Scale.IsEqualApprox(Vector2.One * .825f),
                "The existing static death still owns its normalized half-duration fade and shrink.");
            await AdvanceTo(754.999);
            Require(!playing.IsCompleted && exits == 0, "Recoil cannot shorten the existing 755 ms fatal receipt endpoint.");
            await AdvanceTo(755);
            Require(playing.IsCompleted && await playing && exits == 1
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
                && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                "Duplicate death events retire the keeper once and add no second death wait.");
            await Tick(board, 3);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
                "Neither breathing nor recoil can resurrect a retired keeper.");
            var killedOnly = ReactionDamage("collisionDamage", did);
            killedOnly["killed"] = true;
            var flagged = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [killedOnly]));
            await Drain(board, flagged);
            Require(Actor(board, did).Scale == Vector2.One && Actor(board, did).SelfModulate == Colors.White,
                "A killed flag without a death event never removes or fades the keeper.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckGravekeeperRecoilLifetime(BoardView board, JsonObject state, ActorClip recoil, ActorClip idle)
    {
        var target = state["units"]![0]!.AsObject();
        var source = state["units"]![1]!.AsObject();
        var did = Int(target, "id");
        var aid = Int(source, "id");
        var events = MainAttackScope(source, target, "c01.border-blade", Strike("attack", aid, did));
        var transition = Transition(state, state, IdleAction(), events);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var interruption in new[] { "cancel", "rebind", "exit" })
                foreach (var seconds in new[] { .050, .090, .290, .450 })
                {
                    published.Clear();
                    var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, seconds);
                    var origin = Actor(board, did).Position;
                    if (interruption == "cancel")
                        board.CancelPresentation();
                    else
                    {
                        if (interruption == "exit")
                        {
                            RemoveChild(board);
                            Require(!await playing, "Tree exit cancels the finite keeper hit without requiring another process frame.");
                            AddChild(board);
                        }
                        ConfigureActorClipBoard(board, assets);
                        board.RenderState(state);
                    }
                    Require(!await playing && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                        "Cancellation, rebind and exit retire all finite owners before release, during recoil and in the impact-only tail.");
                    CheckSettledClipActor(board, did, idle.Asset, origin);
                    await Tick(board, .701);
                    CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, 1);
                    Require(published.SequenceEqual(seconds >= .075 ? new[] { 0 } : Array.Empty<int>()),
                        "Fresh breathing advances while cancelled recoil cannot publish a stale damage receipt.");
                }
            published.Clear();
            var coarse = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, 1);
            Require(coarse.IsCompleted && await coarse && published.SequenceEqual(Enumerable.Range(0, events.Length))
                && board.GetNodeOrNull("BattleEffects") is null,
                "A coarse tick crosses release, all hit poses and impact retirement without duplicate publication.");
            CheckSettledClipActor(board, did, idle.Asset, Actor(board, did).Position);
            board.AnimationsEnabled = false;
            try
            {
                published.Clear();
                var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
                Require(reduced.IsCompleted && await reduced && !board.IsPresenting
                    && board.GetNodeOrNull("BattleEffects") is null
                    && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                    "Reduced motion settles identical keeper hit receipts synchronously without a new animation owner.");
                CheckSettledClipActor(board, did, idle.Asset, Actor(board, did).Position);
            }
            finally { board.AnimationsEnabled = true; }
            var targetOnly = new HashSet<int> { did };
            var hiddenSource = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), events, targetOnly), targetOnly);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "An unseen source adds no release delay or source disclosure to the visible keeper hit.");
            CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
            await Tick(board, .420);
            Require(hiddenSource.IsCompleted && await hiddenSource, "Visible keeper feedback from a hidden source retains its independent 420 ms endpoint.");
            var sourceOnly = new HashSet<int> { aid };
            var hiddenTarget = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), events, sourceOnly), sourceOnly);
            await Tick(board, .075);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
                && board.GetNodeOrNull("BattleEffects/Hit") is null,
                "An unseen keeper discloses no recoil artwork or recipient feedback.");
            await Tick(board, .075);
            Require(hiddenTarget.IsCompleted && await hiddenTarget,
                "A hidden recipient adds no wait beyond the visible source's existing 150 ms recovery.");
        }
        finally { board.EventPresented -= Presented; }
    }
}
