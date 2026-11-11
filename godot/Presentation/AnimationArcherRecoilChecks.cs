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
    private async Task CheckArcherRecoilPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.archer", "hit", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units.Count + bindings.Actors.Count == 40
            && bindings.Units["c01.archer"].ActionClips["hit"] == "archer.recoil"
            && clip.Asset.Identity == "archer-recoil-v1" && clip.Asset.TopicId == "C01-UNIT-ARCHER"
            && clip.Asset.FrameCount == 4 && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .072f)
            && clip.Length == 320 && clip.ReleaseMs is null && clip.Death is null
            && clip.Asset.FrameOrder.SequenceEqual(new[] { "recoil-onset", "recoil-peak", "recoil-recover", "recoil-ready" }),
            "The C01 archer selects exactly four authored recoil poses at uniform scale over 320 ms, without a release or death policy.");
        Require(!bindings.Actors.ContainsKey("campaign-elin") && bindings.Units["c01.archer"].AppearanceId is null
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "hit", ActorClipUnit.Milliseconds, "campaign-elin"), clip)
            && bindings.Units["c01.archer"].ActionClips["idle"] == "archer.breathe"
            && bindings.Units["c01.archer"].ActionClips["attack"] == "archer.loose"
            && bindings.Units["c01.archer"].ActionClips["death"] == "archer.fall",
            "Elin inherits the generic recoil while the archer's base appearance, breathing, shot and authored fall retain their owners.");
        foreach (var (type, key) in bindings.Units.Keys.Select(type => (type, (string?)null))
            .Concat(bindings.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            if ((type == "c01.archer" || type == "c01.gravekeeper") && key is null || key == "campaign-torren")
                continue;
            var other = assets.ResolveUnitClip(type, "hit", ActorClipUnit.Milliseconds, key);
            Require(other.Asset == assets.ResolveUnit(type, key) && other.Length == 150
                && other.FrameAt(0) == other.FrameAt(149.999) && other.ReleaseMs is null,
                "Every remaining generic or named actor, including the base archer, Bran and Mirelle, retains its static hit.");
        }
        var starts = new[] { 0.0, 40, 150, 240 };
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt((frame == 3 ? 320 : starts[frame + 1]) - .001) == frame,
                "Every archer recoil pose has its precise half-open 40/110/90/80 ms interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each original archer recoil drawing retains its complete RGBA silhouette and transparent padding.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 4 && clip.FrameAt(320) == 3 && clip.FrameAt(10000) == 3,
            "Four distinct recoil pictures hold the last finite frame instead of looping.");

        // Production art on synthetic receipts exercises presentation ownership, not lawful battle orders.
        var state = ReleaseState();
        var target = state["units"]![0]!.AsObject();
        target["type"] = "c01.archer";
        var source = state["units"]![1]!.AsObject();
        var board = NewActorClipBoard(assets);
        try
        {
            await CheckArcherRecoilTiming(board, state, source, target, clip, idle);
            target["facing"] = "east";
            source["x"] = 13;
            await CheckArcherRecoilDamageScope(board, state, source, target, clip, idle);
            await CheckArcherRecoilCounterOrder(board, state, source, target, clip, idle);
            await CheckArcherRecoilAreaOrder(board, state, source, target, clip, idle);
            await CheckArcherRecoilDeathHandoff(board, state, source, target, clip, idle);
            await CheckArcherRecoilLifetime(board, state, source, target, idle);
            await CheckArcherRecoilVisibility(board, state, source, target, clip);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Archer recoil: four padded production poses, exact 320 ms reaction/420 ms impact, counter/area/death ordering, damage gates, mirrors/contact and lifetime passed.");
    }

    private async Task CheckArcherRecoilTiming(BoardView board, JsonObject state, JsonObject source, JsonObject target,
        ActorClip recoil, ActorClip idle)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        foreach (var facing in new[] { "west", "east" })
            foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
            {
                target["facing"] = facing;
                source["x"] = facing == "west" ? 11 : 13;
                var sign = facing == "west" ? -1 : 1;
                var strike = Strike(kind, aid, did);
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
                    var task = board.PresentTransitionAsync(viewportFeedback, transition);
                    var elapsedSeconds = 0.0;
                    async Task AdvanceTo(double milliseconds)
                    {
                        var seconds = milliseconds / 1000;
                        await Tick(board, seconds - elapsedSeconds);
                        elapsedSeconds = seconds;
                    }
                    var actor = Actor(board, did);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    var contact = actor.GetMeta("logical_contact").AsVector2();
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    await AdvanceTo(74.999);
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is null && published.Count == 0,
                        "A damaged archer remains idle until the incoming source's exact 75 ms release.");
                    await AdvanceTo(75);
                    var impact = board.GetNode<Node2D>("BattleEffects/Hit");
                    Require(impact.Position == origin, "Released incoming feedback is anchored to the recipient, not the source.");
                    var ends = new[] { 115.0, 225, 315, 395 };
                    for (var frame = 0; frame < ends.Length; frame++)
                    {
                        CheckClipSprite(sprite, recoil.Asset, frame, sign);
                        CheckClipShadow(sprite);
                        Require(actor.Position == origin && actor.Scale == Vector2.One
                            && actor.SelfModulate == Colors.White && actor.Modulate == Colors.White
                            && actor.GetMeta("logical_contact").AsVector2() == contact
                            && actor.GetNodeOrNull("ActionAccent") is null
                            && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit")),
                            "Recoil changes anatomy without moving support, scaling or tinting the root, adding an accent or replaying impact.");
                        await AdvanceTo(ends[frame] - .001);
                        CheckClipSprite(sprite, recoil.Asset, frame, sign);
                        await AdvanceTo(ends[frame]);
                    }
                    CheckClipSprite(sprite, idle.Asset, 0, sign);
                    CheckClipShadow(sprite);
                    Require(!task.IsCompleted && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit")),
                        "At release plus 320 ms recoil returns to breathing while the original 420 ms impact remains alive.");
                    await AdvanceTo(494.999);
                    Require(!task.IsCompleted && ReferenceEquals(impact, board.GetNodeOrNull("BattleEffects/Hit"))
                        && published.SequenceEqual(kind == "attack" ? new[] { 0 } : Array.Empty<int>()),
                        "Recoil cannot finish the impact receipt before release plus 420 ms.");
                    await AdvanceTo(495);
                    Require(task.IsCompleted && await task && !board.IsPresenting
                        && board.GetNodeOrNull("BattleEffects") is null
                        && published.SequenceEqual(Enumerable.Range(0, events.Length))
                        && state.ToJsonString() == unchanged
                        && new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString() == eventBytes,
                        "At 495 ms each original receipt publishes once, with no added command tail or mutation of battle facts.");
                    CheckSettledClipActor(board, did, idle.Asset, origin);
                }
                finally { board.EventPresented -= Presented; }
            }
    }

    private async Task CheckArcherRecoilDamageScope(BoardView board, JsonObject state, JsonObject source, JsonObject target,
        ActorClip recoil, ActorClip idle)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        foreach (var kind in new[] { "collisionDamage", "statusTick" })
            foreach (var amount in new[] { 0.0, 12.0 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDamage(kind, did, amount)]));
                var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
                CheckClipSprite(sprite, amount > 0 ? recoil.Asset : idle.Asset, 0, 1);
                await Tick(board, .150);
                CheckClipSprite(sprite, amount > 0 ? recoil.Asset : idle.Asset, amount > 0 ? 2 : 0, 1);
                await Tick(board, .170);
                CheckClipSprite(sprite, idle.Asset, 0, 1);
                Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "Only positive committed collision/status damage owns recoil; its independent feedback still lasts 420 ms.");
                await Tick(board, .100);
                Require(task.IsCompleted && await task, "Collision and status hit presentation adds no recipient tail.");
            }
        foreach (var kind in new[] { "attack", "heal", "regen" })
        {
            var fixture = (JsonObject)state.DeepClone();
            var attacker = fixture["units"]![1]!.AsObject();
            var victim = fixture["units"]![0]!.AsObject();
            var zero = Strike("attack", aid, did);
            zero["damage"] = 0;
            if (kind == "heal")
                attacker["type"] = "c01.gravekeeper";
            var events = kind == "heal" ? new[] { HealingEvent(aid, did) }
                : kind == "regen" ? [new JsonObject { ["type"] = "regen", ["unit"] = did, ["amount"] = 12 }]
                : MainAttackScope(attacker, victim, "c01.border-blade", zero);
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(fixture, fixture, IdleAction(), events));
            var elapsedMs = kind == "heal" ? 300.0 : kind == "attack" ? 75 : 0;
            await Tick(board, elapsedMs / 1000);
            var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
            foreach (var step in new[] { 0.0, 40, 110, 90, 80 })
            {
                await Tick(board, step / 1000);
                elapsedMs += step;
                CheckClipSprite(sprite, idle.Asset, idle.FrameAt(elapsedMs % idle.Length), 1);
                Require(board.GetNodeOrNull(kind == "attack" ? "BattleEffects/Hit" : "BattleEffects/Heal") is not null
                    && Actor(board, did).GetNodeOrNull("ActionAccent") is null,
                    "Zero damage, healing and regeneration retain feedback without acquiring archer recoil.");
            }
            await Drain(board, task);
        }
    }

    private async Task CheckArcherRecoilCounterOrder(BoardView board, JsonObject state, JsonObject source, JsonObject target,
        ActorClip recoil, ActorClip idle)
    {
        var did = Int(target, "id");
        var shot = assets.ResolveUnitClip("c01.archer", "attack", ActorClipUnit.Milliseconds);
        foreach (var archerStarts in new[] { false, true })
        {
            var attacker = archerStarts ? target : source;
            var defender = archerStarts ? source : target;
            var weapon = archerStarts ? "c01.ranger-bow" : "c01.border-blade";
            var counterWeapon = archerStarts ? "c01.border-blade" : "c01.ranger-bow";
            var events = MainAttackScope(attacker, defender, weapon,
                ReleaseStrike("attack", Int(attacker, "id"), Int(defender, "id"), weapon),
                ReleaseStrike("counter", Int(defender, "id"), Int(attacker, "id"), counterWeapon));
            var unchanged = state.ToJsonString();
            var published = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => published.Add(index);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
                var sprite = Actor(board, did).GetNode<Sprite2D>("AuthoredSprite");
                if (archerStarts)
                {
                    await Tick(board, .599999);
                    CheckClipSprite(sprite, shot.Asset, 5, 1);
                    await Tick(board, .000001);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await Tick(board, .139999);
                    Require(published.SequenceEqual(new[] { 0 }), "Counter waits for the original 740 ms bow impact boundary.");
                    await Tick(board, .000001);
                    var counterImpact = board.GetNodeOrNull("BattleEffects/Hit");
                    Require(counterImpact is null && published.SequenceEqual(new[] { 0, 1 }),
                        "The counter source begins only after the first impact, with no early archer recoil.");
                    await Tick(board, .074999);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await Tick(board, .000001);
                    CheckClipSprite(sprite, recoil.Asset, 0, 1);
                    await Tick(board, .320);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await Tick(board, .099999);
                }
                else
                {
                    await Tick(board, .075);
                    CheckClipSprite(sprite, recoil.Asset, 0, 1);
                    await Tick(board, .320);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await Tick(board, .099999);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    Require(published.SequenceEqual(new[] { 0 }), "Archer counter cannot steal the incoming 420 ms impact tail.");
                    await Tick(board, .000001);
                    CheckClipSprite(sprite, shot.Asset, 0, 1);
                    Require(published.SequenceEqual(new[] { 0, 1 }) && board.GetNodeOrNull("BattleEffects/Hit") is null,
                        "At 495 ms the archer begins its existing shot, without a new recoil delay or premature second impact.");
                    await Tick(board, .319999);
                    CheckClipSprite(sprite, shot.Asset, 2, 1);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is null, "Counter shot preserves its full 320 ms release gate.");
                    await Tick(board, .000001);
                    CheckClipSprite(sprite, shot.Asset, 3, 1);
                    await Tick(board, .280);
                    CheckClipSprite(sprite, idle.Asset, 0, 1);
                    await Tick(board, .139999);
                }
                Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "Both attack/counter orders retain the final impact through 1234.999 ms.");
                await Tick(board, .000001);
                Require(task.IsCompleted && await task && published.SequenceEqual(Enumerable.Range(0, events.Length))
                    && state.ToJsonString() == unchanged && board.GetNodeOrNull("BattleEffects") is null,
                    "At 1235 ms both source orders settle once without changing the committed battle state.");
            }
            finally { board.EventPresented -= Presented; }
        }
    }

    private async Task CheckArcherRecoilAreaOrder(BoardView board, JsonObject state, JsonObject source, JsonObject target,
        ActorClip recoil, ActorClip idle)
    {
        var fixture = (JsonObject)state.DeepClone();
        var second = (JsonObject)target.DeepClone();
        second["id"] = 1000;
        second["x"] = 10;
        fixture["units"]!.AsArray().Add(second);
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var events = MainAttackScope(source, target, "c01.border-blade",
            Strike("areaAttack", aid, did), Strike("areaAttack", aid, 1000));
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(fixture, fixture, IdleAction(), events));
            await Tick(board, .075);
            CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            var firstImpact = board.GetNode<Node2D>("BattleEffects/Hit");
            await Tick(board, .419999);
            CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            await Tick(board, .000001);
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
            Require(published.SequenceEqual(new[] { 0, 1 })
                && !ReferenceEquals(firstImpact, board.GetNodeOrNull("BattleEffects/Hit")),
                "The second area victim starts only at the first 420 ms impact boundary, without replaying source anticipation.");
            await Tick(board, .320);
            CheckClipSprite(Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            await Tick(board, .099999);
            Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "The second area impact remains alive immediately before its 915 ms endpoint.");
            await Tick(board, .000002);
            Require(task.IsCompleted && await task && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                "Two area recipients retain one 75 ms release and two 420 ms impacts, complete immediately after 915 ms.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckArcherRecoilDeathHandoff(BoardView board, JsonObject state, JsonObject source, JsonObject target,
        ActorClip recoil, ActorClip idle)
    {
        var did = Int(target, "id");
        var after = (JsonObject)state.DeepClone();
        after["units"]!.AsArray().RemoveAt(0);
        var strike = Strike("attack", Int(source, "id"), did);
        strike["killed"] = true;
        var events = MainAttackScope(source, target, "c01.border-blade", strike, ReactionDeathEvent(did));
        var beforeBytes = state.ToJsonString();
        var afterBytes = after.ToJsonString();
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            var elapsedSeconds = 0.0;
            async Task AdvanceTo(double milliseconds)
            {
                var seconds = milliseconds / 1000;
                await Tick(board, seconds - elapsedSeconds);
                elapsedSeconds = seconds;
            }
            await AdvanceTo(75);
            var actor = Actor(board, did);
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            CheckClipSprite(sprite, recoil.Asset, 0, 1);
            await AdvanceTo(395);
            CheckClipSprite(sprite, idle.Asset, 0, 1);
            await AdvanceTo(534.999);
            CheckClipSprite(sprite, idle.Asset, 0, 1);
            Require(actor.SelfModulate == Colors.White && actor.Scale == Vector2.One
                && published.SequenceEqual(new[] { 0 }),
                "A fatal flag cannot begin archer death before the 420 ms impact and existing 40 ms fatal beat finish.");
            await AdvanceTo(535);
            var death = assets.ResolveUnitClip("c01.archer", "death", ActorClipUnit.Milliseconds);
            CheckClipSprite(sprite, death.Asset, 0, 1);
            Require(death.Asset.Identity == "archer-fall-v1" && death.Length == 620
                && death.RequireDeath().Mode == ActorDeathMode.AfterClip && death.RequireDeath().TerminalFadeMs == 160
                && actor.SelfModulate == Colors.White && actor.Scale == Vector2.One
                && published.SequenceEqual(new[] { 0, 1 }) && !task.IsCompleted,
                "At 535 ms only the ordered death event starts the authored fall after release, impact and fatal beat.");
            await AdvanceTo(1155);
            CheckClipSprite(sprite, death.Asset, 5, 1);
            Require(actor.SelfModulate == Colors.White && actor.Scale == Vector2.One
                && published.SequenceEqual(new[] { 0, 1 }) && !task.IsCompleted,
                "The complete 620 ms fall remains opaque before its independent terminal fade.");
            await AdvanceTo(1235);
            Require(Mathf.IsEqualApprox(actor.SelfModulate.A, .5f) && actor.Scale == Vector2.One
                && Frame(actor) == 5 && !task.IsCompleted,
                "Terminal fade halves alpha without shrinking or replacing the grounded drawing.");
            await AdvanceTo(1314.999);
            Require(!task.IsCompleted && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is not null,
                "The full authored death interval remains visible until 1314.999 ms.");
            await AdvanceTo(1315);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
                && published.SequenceEqual(Enumerable.Range(0, events.Length))
                && state.ToJsonString() == beforeBytes && after.ToJsonString() == afterBytes,
                "At 1315 ms fatal recoil and authored death settle once with no change to committed state.");
            await Tick(board, 2);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
                "Breathing and recoil cannot resurrect a retired archer.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckArcherRecoilLifetime(BoardView board, JsonObject state, JsonObject source, JsonObject target, ActorClip idle)
    {
        var did = Int(target, "id");
        var events = MainAttackScope(source, target, "c01.border-blade", Strike("attack", Int(source, "id"), did));
        var transition = Transition(state, state, IdleAction(), events);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var seconds in new[] { .050, .090, .290, .450 })
            {
                published.Clear();
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, seconds);
                var origin = Actor(board, did).Position;
                board.CancelPresentation();
                Require(!await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation before release, during recoil or during impact-only feedback retires every finite owner.");
                CheckSettledClipActor(board, did, idle.Asset, origin);
                await Tick(board, .501);
                CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, 1);
                Require(published.SequenceEqual(seconds >= .075 ? new[] { 0 } : Array.Empty<int>()),
                    "Cancelled recoil cannot publish a stale damage receipt after idle resumes.");
            }
            published.Clear();
            var coarse = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, 1);
            Require(coarse.IsCompleted && await coarse && !board.IsPresenting
                && board.GetNodeOrNull("BattleEffects") is null
                && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                "A coarse tick crosses release, every recoil pose and impact completion without duplicate callbacks.");
            CheckSettledClipActor(board, did, idle.Asset, Actor(board, did).Position);
            board.AnimationsEnabled = false;
            try
            {
                published.Clear();
                var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
                Require(reduced.IsCompleted && await reduced && !board.IsPresenting
                    && board.GetNodeOrNull("BattleEffects") is null
                    && published.SequenceEqual(Enumerable.Range(0, events.Length)),
                    "Reduced motion settles the same hit synchronously without stale recoil ownership or missing receipts.");
                CheckSettledClipActor(board, did, idle.Asset, Actor(board, did).Position);
            }
            finally { board.AnimationsEnabled = true; }
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckArcherRecoilVisibility(BoardView board, JsonObject state, JsonObject source, JsonObject target, ActorClip recoil)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var events = MainAttackScope(source, target, "c01.border-blade", Strike("attack", aid, did));
        var targetOnly = new HashSet<int> { did };
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), events, targetOnly), targetOnly);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
            && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "A hidden source adds no release delay or leaked source art to a visible archer's committed hit.");
        CheckClipSprite(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"), recoil.Asset, 0, 1);
        await Tick(board, .420);
        Require(task.IsCompleted && await task, "A visible recipient of a hidden source settles at the independent 420 ms boundary.");
        var sourceOnly = new HashSet<int> { aid };
        task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), events, sourceOnly), sourceOnly);
        await Tick(board, .075);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
            && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "An unseen archer receives no visible recoil or recipient feedback.");
        await Tick(board, .075);
        Require(task.IsCompleted && await task, "Hidden recoil adds no wait beyond the visible source's existing recovery.");
    }
}
