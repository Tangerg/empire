#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const string ReactionHit = "fixture.hit";
    private const string ReactionDeath = "fixture.death";

    private static ActorClipRecord ReactionClip(double length, bool death = false) => new()
    {
        Unit = "milliseconds",
        Death = death ? new()
        {
            Mode = "during-clip"
        } : null,
        // Timing fixtures use three existing trusted poses, not newly accepted reaction artwork.
        AppearanceId = "torren-stage01",
        Frames = new[]
        {
            new ActorClipFrameRecord { Frame = death ? "walk-b" : "walk-a", Span = 32 },
            new ActorClipFrameRecord { Frame = "attack", Span = 64 },
            new ActorClipFrameRecord { Frame = death ? "walk-a" : "walk-b", Span = length - 96 }
        }
    };

    private static (AssetManifest Manifest, ArtBindings Bindings) ReactionFixture(double hit = 128, double death = 256)
    {
        var (manifest, bindings) = ReleaseFixture();
        manifest.ActorClips[ReactionHit] = ReactionClip(hit);
        manifest.ActorClips[ReactionDeath] = ReactionClip(death, true);
        bindings.Units["c01.swordsman"].ActionClips["hit"] = ReactionHit;
        bindings.Units["c01.swordsman"].ActionClips["death"] = ReactionDeath;
        return (manifest, bindings);
    }

    private static JsonObject ReactionDamage(string kind, int id, double amount = 12)
    {
        var item = new JsonObject { ["type"] = kind, ["unit"] = id, ["amount"] = amount };
        if (kind == "statusTick")
        {
            item["status"] = "poisoned";
            item["hpAfter"] = 0;
        }
        else if (kind == "collisionDamage")
            item["killed"] = false;
        return item;
    }

    private static JsonObject ReactionDeathEvent(int id) => new() { ["type"] = "death", ["unit"] = id };

    private async Task CheckReactionClips()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            foreach (var (action, duration) in new[] { ("hit", 150), ("death", 220) })
            {
                var clip = assets.ResolveUnitClip(type, action, ActorClipUnit.Milliseconds, key);
                if (key == "campaign-torren" && action == "hit")
                {
                    Require(clip.Asset.Identity == "torren-recoil-v1" && clip.Length == 320
                        && clip.FrameAt(0) == 0 && clip.FrameAt(319.999) == 3 && clip.ReleaseMs is null,
                        "Torren's explicit authored recoil replaces only his static hit placeholder.");
                    continue;
                }
                Require(clip.Length == duration && clip.FrameAt(0) == clip.FrameAt(duration - 1)
                    && clip.ReleaseMs is null, "Shipped hit chooses an explicit 150 ms static hold; death retains its original 220 ms static duration.");
            }
        }
        var (manifest, bindings) = ReactionFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var state = ReleaseState();
        var aid = Int(state["units"]![0]!.AsObject(), "id");
        var did = Int(state["units"]![1]!.AsObject(), "id");
        var board = NewActorClipBoard(catalog);
        try
        {
            await CheckReactionTiming(board, state, aid, did);
            await CheckReactionScope(board, state, aid, did);
            await CheckReactionSourceOwnership(board, state, aid);
            await CheckReactionDeath(board, state, did);
            await CheckReactionResultWait(board, state, did);
            await CheckReactionLifetime(board, catalog, state, did);
            await CheckReactionVisibility(board, state, aid, did);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckReactionLongHit(state, did);
        await CheckReactionFailure(state, aid, did);
        await CheckReactionLegalReceipt();
        await CheckReactionLegalStatusReceipt();
    }

    private async Task CheckReactionTiming(BoardView board, JsonObject state, int aid, int did)
    {
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), [ReactionDamage("collisionDamage", did)]));
        var actor = Actor(board, did);
        var origin = actor.Position;
        Require(Frame(actor) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "Positive committed collision damage starts the recipient hit and impact together.");
        await Tick(board, .031999);
        Require(Frame(actor) == 1, "Hit holds its first authored pose immediately before the 32 ms boundary.");
        await Tick(board, .000001);
        Require(Frame(actor) == 2, "Hit advances exactly at the authored 32 ms boundary.");
        await Tick(board, .064);
        Require(Frame(actor) == 3 && actor.Position == origin, "Hit samples the third pose without inventing displacement.");
        await Tick(board, .032);
        Require(Frame(actor) == 0 && !task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "Short hit resumes idle at 128 ms while the independent 420 ms effect remains alive.");
        await Tick(board, .293);
        Require(task.IsCompleted && await task, "Short hit cannot append its length to the existing impact timer.");

        var scope = ScopedClipAttack(Strike("attack", aid, did), At(12, 16), At(13, 16));
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), scope));
        Require(Frame(Actor(board, aid)) == 1 && Frame(Actor(board, did)) == 0,
            "The target stays idle during source anticipation.");
        await Tick(board, .064);
        Require(Frame(Actor(board, aid)) == 2 && Frame(Actor(board, did)) == 1,
            "Explicit source release starts hit concurrently with source recovery.");
        await Tick(board, .064);
        Require(Frame(Actor(board, aid)) == 0 && Frame(Actor(board, did)) == 2,
            "Source recovery and recipient reaction have independent elapsed clocks.");
        await Tick(board, .064);
        Require(Frame(Actor(board, did)) == 0 && !task.IsCompleted,
            "Released hit returns idle independently before its longer effect finishes.");
        await Drain(board, task);

        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), scope));
        await Tick(board, .300);
        Require(Frame(Actor(board, aid)) == 0 && Frame(Actor(board, did)) == 0
            && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "One coarse clock sample crosses release and both actor endpoints without resetting reaction elapsed time.");
        await Drain(board, task);
    }

    private async Task CheckReactionScope(BoardView board, JsonObject state, int aid, int did)
    {
        foreach (var kind in new[] { "attack", "areaAttack", "counter", "supportAttack", "partingShot" })
        {
            var damage = Strike(kind, aid, did);
            if (kind == "partingShot")
                damage["at"] = At(13, 16);
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [damage]));
            if (kind is "counter" or "supportAttack" or "partingShot")
                await Tick(board, .064);
            Require(Frame(Actor(board, did)) == 1, $"Positive {kind} damage samples the recipient's explicit hit clip.");
            await Drain(board, task);
            damage["damage"] = 0;
            task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [damage]));
            if (kind is "counter" or "supportAttack" or "partingShot")
                await Tick(board, .064);
            Require(Frame(Actor(board, did)) == 0, $"Zero-damage {kind} never requests a hit pose.");
            await Drain(board, task);
        }
        foreach (var kind in new[] { "collisionDamage", "statusTick" })
        {
            foreach (var amount in new[] { 12, 0 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDamage(kind, did, amount)]));
                Require(Frame(Actor(board, did)) == (amount > 0 ? 1 : 0),
                    "Collision and status reactions depend on positive committed amount, not event name alone.");
                await Drain(board, task);
                Require(Actor(board, did).SelfModulate.A == 1 && Frame(Actor(board, did)) == 0,
                    "Status hpAfter zero alone never invents death or retires a living snapshot actor.");
            }
        }
        foreach (var healing in new[] { HealingEvent(aid, did), ReactionDamage("regen", did) })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [healing]));
            await Tick(board, .064);
            Require(Frame(Actor(board, did)) == 0, "Healing and regeneration never reuse the damage reaction.");
            await Drain(board, task);
        }
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                [Strike("areaAttack", aid, did), Strike("areaAttack", did, aid)]));
            Require(Frame(Actor(board, did)) == 1 && Frame(Actor(board, aid)) == 0,
                "Ordered multi-target damage does not invent parallel recipient reactions.");
            await Tick(board, .421);
            Require(published.SequenceEqual(new[] { 0 }) && Frame(Actor(board, did)) == 0 && Frame(Actor(board, aid)) == 1,
                "Only after the first impact completes does the next domain event own its recipient pose.");
            await Drain(board, task);
            Require(published.SequenceEqual(new[] { 0, 1 }), "Multi-target reactions publish once in original domain order.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckReactionSourceOwnership(BoardView board, JsonObject state, int aid)
    {
        // Identity overlap is presenter robustness, not an invented legal self-attack order.
        var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            ScopedClipAttack(Strike("attack", aid, aid), At(12, 16), At(12, 16))));
        await Tick(board, .064);
        Require(Frame(Actor(board, aid)) == 2 && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "A source receiving its own hit must finish existing recovery before the reaction takes pose ownership.");
        await Tick(board, .032);
        Require(Frame(Actor(board, aid)) == 3, "Overlapping identity cannot overwrite source recovery with a hit pose.");
        await Tick(board, .032);
        Require(Frame(Actor(board, aid)) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "A self-identity recipient begins its hit only when the source action releases pose ownership.");
        await Drain(board, task);
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [Strike("counter", aid, aid)]));
        await Tick(board, .096);
        Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "A reactive self-recipient also serializes source recovery before the hit.");
        await Tick(board, .032);
        Require(Frame(Actor(board, aid)) == 1, "Reactive self-recipient acquires the hit pose once recovery ends.");
        await Drain(board, task);
        var did = Int(state["units"]![1]!.AsObject(), "id");
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [Strike("counter", aid, did)]));
        await Tick(board, .080);
        Require(Frame(Actor(board, aid)) == 2 && Frame(Actor(board, did)) == 1,
            "The cancellation witness has simultaneously active source and recipient poses.");
        CheckClipShadow(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite"));
        board.CancelPresentation();
        Require(!await task && Frame(Actor(board, aid)) == 0 && Frame(Actor(board, did)) == 0,
            "Cancellation releases both concurrent pose owners together.");
        await Tick(board, 1);
        Require(board.GetNodeOrNull("BattleEffects") is null && Frame(Actor(board, did)) == 0,
            "Retired concurrent recovery cannot recreate recipient art or impact.");
    }

    private async Task CheckReactionDeath(BoardView board, JsonObject state, int did)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]!.AsArray().Remove(after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == did));
        var count = state["units"]!.AsArray().Count;
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, after, IdleAction(), [ReactionDeathEvent(did), ReactionDeathEvent(did)]));
        var actor = Actor(board, did);
        Require(Frame(actor) == 3 && actor.SelfModulate.A == 1, "Only the death event begins the death clip at its first authored pose.");
        await Tick(board, .032);
        Require(Frame(actor) == 2 && Mathf.IsEqualApprox(actor.SelfModulate.A, .875f),
            "Death fade uses authored clip length, not the old fixed 220 ms timer.");
        await Tick(board, .096);
        Require(Frame(actor) == 1 && Mathf.IsEqualApprox(actor.SelfModulate.A, .5f)
            && actor.Scale.IsEqualApprox(Vector2.One * .825f),
            "Death samples the third trusted pose with normalized half-duration fade and shrink.");
        await Tick(board, .127);
        Require(!task.IsCompleted && Frame(actor) == 1, "Death never returns to idle before retirement.");
        await Tick(board, .002);
        Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null
            && board.RenderedUnitCount == count - 1, "Duplicate death events retire the actor exactly once without a second wait or count decrement.");

        var fatal = ReactionDamage("collisionDamage", did);
        fatal["killed"] = true;
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [fatal]));
        await Tick(board, .430);
        Require(Frame(Actor(board, did)) == 0 && Actor(board, did).SelfModulate.A == 1,
            "A killed flag alone does not synthesize a death pose or retire the actor.");
        await Drain(board, task);
    }

    private async Task CheckReactionResultWait(BoardView board, JsonObject state, int did)
    {
        var after = (JsonObject)state.DeepClone();
        after["units"]!.AsArray().Remove(after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == did));
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(),
                [ReactionDeathEvent(did), new JsonObject { ["type"] = "gameOver", ["conclusion"] = Empire.Core.Lifecycle.BattleConclusion.LastTeamRemaining(1).ToJson() }]));
            await Tick(board, .255);
            Require(!task.IsCompleted && published.Count == 0,
                "Final result feedback cannot bypass the dying actor's authored clip.");
            await Tick(board, .002);
            Require(published.SequenceEqual(new[] { 0 }) && !task.IsCompleted,
                "The result event begins only after the death event retires its actor.");
            await Drain(board, task);
            Require(published.SequenceEqual(new[] { 0, 1 }), "Death and result publish exactly once in receipt order.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckReactionLongHit(JsonObject state, int did)
    {
        var (manifest, bindings) = ReactionFixture(640);
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [ReactionDamage("collisionDamage", did)]));
            await Tick(board, .421);
            Require(!task.IsCompleted && Frame(Actor(board, did)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                "Long hit outlives its effect without retaining an expired impact node.");
            await Tick(board, .100);
            Require(board.GetNodeOrNull("BattleEffects/Hit") is null, "Later hit samples cannot recreate a retired impact.");
            await Tick(board, .120);
            Require(task.IsCompleted && await task && Frame(Actor(board, did)) == 0,
                "Long hit finishes at its own endpoint, not hit plus effect duration.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckReactionFailure(JsonObject state, int aid, int did)
    {
        var (manifest, bindings) = ReactionFixture();
        manifest.ActorClips[ReleaseCastAction] = ReleaseClip(1e20, 32);
        bindings.Units["c01.swordsman"].ActionClips.Remove("hit");
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                [ReleaseStrike("counter", aid, did, ReleaseCastWeapon)]));
            await Tick(board, .033);
            Require(task.IsCompleted && !board.IsPresenting,
                "A recipient error must fail at release, not wait for an unrelated 1e20 ms source recovery.");
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && board.GetNodeOrNull("BattleEffects") is null && Frame(Actor(board, aid)) == 0,
                "A failed released recipient cancels recovery and settles the committed snapshot without hiding its error.");
            await Tick(board, 1);
            Require(Frame(Actor(board, aid)) == 0 && Frame(Actor(board, did)) == 0,
                "No failed branch can resume a retired source or recipient after settlement.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckReactionVisibility(BoardView board, JsonObject state, int aid, int did)
    {
        var (manifest, bindings) = ReactionFixture();
        bindings.Units["c01.swordsman"].ActionClips.Remove("hit");
        bindings.Units["c01.swordsman"].ActionClips.Remove("death");
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        var visible = new HashSet<int> { aid };
        foreach (var item in new[] { Strike("attack", aid, did), ReactionDamage("collisionDamage", did),
            ReactionDamage("statusTick", did), ReactionDeathEvent(did) })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [item], visible), visible);
            Require(!task.IsFaulted, "Hidden recipients are gated before missing reaction art is resolved.");
            await Drain(board, task);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null && board.GetNodeOrNull("BattleEffects") is null,
                "Hidden damage and death cannot leak actor poses or effects.");
        }
        foreach (var kind in new[] { "collisionDamage", "statusTick" })
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [ReactionDamage(kind, did)], new HashSet<int> { aid, did }), visible);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
                "Continuing-unit damage outside final visibility cannot resolve reaction art or disclose an effect.");
        }
        foreach (var item in new[] { ReactionDamage("collisionDamage", did), ReactionDeathEvent(did) })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [item]));
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                "Visible missing hit/death art fails explicitly and cleans up instead of falling back to idle.");
        }
    }

    private async Task CheckReactionLifetime(BoardView board, AssetCatalog catalog, JsonObject state, int did)
    {
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var item in new[] { ReactionDamage("collisionDamage", did), ReactionDeathEvent(did) })
            {
                var transition = Transition(state, state, IdleAction(), [item]);
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .064);
                board.CancelPresentation();
                Require(!await task && published.Count == 0 && !board.IsPresenting,
                    "Cancelling active hit or death retires its clock without publishing an unfinished event.");
                await Tick(board, 1);
                Require(Frame(Actor(board, did)) == 0 && Actor(board, did).SelfModulate.A == 1
                    && Actor(board, did).Scale == Vector2.One && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation restores the settled actor and later ticks cannot resurrect reaction ownership.");
                task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .064);
                ConfigureActorClipBoard(board, catalog);
                board.RenderState(state);
                Require(!await task && published.Count == 0 && Frame(Actor(board, did)) == 0,
                    "Rebinding cancels an active reaction without stale pose or publication.");
                task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .064);
                RemoveChild(board);
                Require(!await task && published.Count == 0, "Tree exit cancels an active hit or death.");
                AddChild(board);
                ConfigureActorClipBoard(board, catalog);
                board.RenderState(state);
                await Tick(board, 1);
                Require(Frame(Actor(board, did)) == 0 && board.GetNodeOrNull("BattleEffects") is null,
                    "Readding the board does not resurrect a retired reaction.");
            }
            board.AnimationsEnabled = false;
            var after = (JsonObject)state.DeepClone();
            after["units"]!.AsArray().Remove(after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == did));
            var reduced = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(),
                [ReactionDamage("collisionDamage", did), ReactionDeathEvent(did)]));
            Require(reduced.IsCompleted && await reduced && published.SequenceEqual(new[] { 0, 1 })
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion synchronously settles hit and death in order without residual ownership.");
            board.AnimationsEnabled = true;
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckReactionLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var checkpoint = witness["checkpoints"]![0]!.AsObject();
        var levelId = witness["levelId"]!.GetValue<string>();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(), "Reaction integration starts from the unchanged formal initial state.");
        var index = Int(checkpoint, "index");
        foreach (var action in witness["actions"]!.AsArray().Take(index).OfType<JsonObject>())
            battle.Dispatch(action);
        var before = battle.Snapshot();
        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), "Real legal orders reach the recorded reaction checkpoint.");
        var order = witness["actions"]![index]!.AsObject();
        var receipt = battle.Dispatch(order);
        var attack = receipt.Events.First(item => item["type"]!.GetValue<string>() == "attack");
        var did = Int(attack, "defender");
        var target = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == did);
        var (manifest, bindings) = ReactionFixture(640);
        var key = target["key"]?.GetValue<string>();
        var binding = key is not null && bindings.Actors.TryGetValue(key, out var named)
            ? named.UnitArt : bindings.Units[target["type"]!.GetValue<string>()];
        manifest.ActorClips["fixture.legal-recipient-hit"] = new()
        {
            Unit = "milliseconds",
            Frames = ReactionClip(640).Frames
        };
        binding.ActionClips["hit"] = "fixture.legal-recipient-hit";
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(before));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            var board = screen.Board;
            board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                recordedLevel, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
            var published = new List<int>();
            board.EventPresented += (_, eventIndex) => published.Add(eventIndex);
            Require(session.Dispatch(EcmaJson.ToElement(order), session.Revision).Accepted, "The native screen accepts the original lawful damage order.");
            screen.Refresh();
            screen.Refresh();
            for (var tick = 0; board.GetNodeOrNull("BattleEffects/Hit") is null && tick < 100; tick++)
                await Tick(board, .010);
            Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && Frame(Actor(board, did)) == 1,
                "The legal receipt starts its actual recipient's fixture hit at committed contact.");
            await Tick(board, .430);
            Require(screen.IsPresenting && Frame(Actor(board, did)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                "BattleScreen remains busy for the long recipient clip after the legal impact has expired.");
            screen.Refresh();
            for (var tick = 0; screen.IsPresenting && tick < 40; tick++)
                await Tick(board, 1);
            Require(!screen.IsPresenting && published.SequenceEqual(Enumerable.Range(0, receipt.Events.Count)),
                "Native refreshes do not duplicate damage or skip ordered receipt completion.");
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            await Tick(board, 1);
            Require(!screen.IsPresenting && published.Count == receipt.Events.Count && screen.RecentMessages.SequenceEqual(messages)
                && StateDigest.Hash(session.Snapshot()) == checkpoint["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()))
                    == checkpoint["eventsSha256"]!.GetValue<string>(),
                "Settled native reactions cannot replay and preserve the exact legal state and event hashes.");
        }
        finally { RemoveChild(screen); screen.Free(); }
    }
    private async Task CheckReactionLegalStatusReceipt()
    {
        // Synthetic lawful scenario, not a formal campaign witness. All status/HP changes come from dispatched rules.
        JsonObject Player(int id) => new()
        {
            ["id"] = id,
            ["team"] = id,
            ["color"] = id == 1 ? "#336699" : "#993333",
            ["controller"] = "human",
            ["resources"] = new JsonObject(),
            ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" })
        };
        var fixtureLevel = new JsonObject
        {
            ["schema"] = 3,
            ["id"] = "fixture.reaction-status",
            ["width"] = 6,
            ["height"] = 4,
            ["terrain"] = new JsonArray("......", "......", "......", "......"),
            ["units"] = new JsonArray(
                new JsonObject { ["unit"] = "rogue", ["owner"] = 1, ["x"] = 1, ["y"] = 1, ["facing"] = "east" },
                new JsonObject { ["unit"] = "knight", ["owner"] = 2, ["x"] = 2, ["y"] = 1, ["facing"] = "west" }),
            ["players"] = new JsonArray(Player(1), Player(2)),
            ["rules"] = new JsonObject
            {
                ["fog"] = false,
                ["zoneOfControl"] = false,
                ["moraleEnabled"] = false,
                ["counterAttack"] = false,
                ["turnLimit"] = null
            }
        };
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(fixtureLevel);
        var units = battle.Snapshot()["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var aid = Int(units[0], "id");
        var did = Int(units[1], "id");
        var attack = battle.Dispatch(new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = aid,
            ["path"] = new JsonArray(At(1, 1)),
            ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = "rogue_blades", ["target"] = At(2, 1) }
        });
        Require(attack.Events.Any(item => item["type"]!.GetValue<string>() == "statusApplied"
            && item["status"]!.GetValue<string>() == "poisoned"),
            "The lawful fixture applies poison through the genuine rogue weapon, without post-commit state injection.");
        var order = IdleAction();
        var receipt = battle.Dispatch(order);
        var events = receipt.Events;
        var damage = events.Single(item => item["type"]!.GetValue<string>() == "statusTick");
        Require(Int(damage, "unit") == did && damage["amount"]!.GetValue<double>() > 0,
            "The next lawful owner turn emits actual committed poison damage.");
        var eventIndex = events.ToList().IndexOf(damage);
        var (manifest, bindings) = ReactionFixture();
        manifest.ActorClips["fixture.lawful-status-hit"] = new()
        {
            Unit = "milliseconds",
            Frames = ReactionClip(128).Frames
        };
        bindings.Units["knight"].ActionClips["hit"] = "fixture.lawful-status-hit";
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(), engine.Grids.Get("square4"),
                engine.Catalog, BoardPlane.Dimetric), fixtureLevel, new AuthoredScene
                {
                    LevelId = "fixture.reaction-status",
                    MapSize = new[] { 6, 4 },
                    RuntimeReady = true,
                    FramePlacements = new()
                });
            board.ConfigureDynamicState();
            var after = battle.Snapshot();
            var beforeHash = StateDigest.Hash(receipt.Before);
            var afterHash = battle.StateHash;
            var eventBytes = new JsonArray(receipt.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString();
            var published = new List<int>();
            board.EventPresented += (_, index) => published.Add(index);
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(receipt.Before, after, order, receipt.Events));
            for (var step = 0; step < 1000 && !(published.Count == eventIndex
                && board.GetNodeOrNull("BattleEffects/Hit") is not null); step++)
                await Tick(board, .010);
            Require(published.Count == eventIndex && Frame(Actor(board, did)) == 1
                && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-" + damage["amount"]!.ToJsonString(),
                "The lawful status tick starts the recipient's explicit hit using its committed amount.");
            await Tick(board, .130);
            Require(Frame(Actor(board, did)) == 0 && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "The lawful status recipient returns idle while its independent impact finishes.");
            await Drain(board, task);
            Require(published.SequenceEqual(Enumerable.Range(0, receipt.Events.Count))
                && StateDigest.Hash(receipt.Before) == beforeHash && StateDigest.Hash(after) == afterHash && battle.StateHash == afterHash
                && new JsonArray(receipt.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString() == eventBytes,
                "Lawful status reaction preserves every state/event byte and publishes original receipt order.");
            GD.Print($"Synthetic lawful poison reaction: unit {did}, amount {damage["amount"]}, hit128/effect420.");
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
