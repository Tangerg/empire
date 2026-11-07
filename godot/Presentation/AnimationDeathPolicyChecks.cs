#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Transactions;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const string DeathPolicyClip = "fixture.death-after-clip";

    private static ActorClipRecord DeathPolicyRecord(ActorDeathRecord? death, string unit = "milliseconds",
        double? release = null, double length = 256) => new()
        {
            Unit = unit,
            ReleaseMs = release,
            Death = death,
            AppearanceId = "torren-stage01",
            // Trusted existing poses are timing fixtures, not approved falling artwork or a visual acceptance test.
            Frames = ReactionClip(length, true).Frames
        };

    private static (AssetManifest Manifest, ArtBindings Bindings) DeathPolicyFixture()
    {
        var (manifest, bindings) = ReactionFixture();
        manifest.ActorClips[DeathPolicyClip] = DeathPolicyRecord(new()
        {
            Mode = "after-clip",
            FadeMs = 120
        });
        bindings.Units["c01.swordsman"].ActionClips["death"] = DeathPolicyClip;
        return (manifest, bindings);
    }

    private static JsonObject WithoutDeathActor(JsonObject state, int id)
    {
        var after = (JsonObject)state.DeepClone();
        var units = after["units"]!.AsArray();
        units.Remove(units.OfType<JsonObject>().Single(unit => Int(unit, "id") == id));
        return after;
    }

    private async Task CheckDeathPolicies()
    {
        CheckDeathPolicyDefinitions();
        var (manifest, bindings) = DeathPolicyFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var state = ReleaseState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
        var id = Int(state["units"]![1]!.AsObject(), "id");
        var board = NewActorClipBoard(catalog);
        try
        {
            await CheckDeathPolicyBoundaries(board, state, id);
            await CheckDeathPolicyColor(board, state, id);
            await CheckDeathPolicyLifetime(board, catalog, state, id);
            await CheckDeathPolicyResult(board, state, id);
            await CheckDeathPolicyVisibility(board, state, id);
            ConfigureActorClipBoard(board, assets);
            await CheckDeathPolicyLegacy(board, state, id);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckDeathPolicyDeclaredTiming(state, id);
        await CheckDeathPolicyLegalReceipt();
    }

    private void CheckDeathPolicyDefinitions()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            var binding = key is null ? shipped.Units[type] : shipped.Actors[key].UnitArt;
            var clip = assets.ResolveUnitClip(type, "death", ActorClipUnit.Milliseconds, key);
            Require(binding.ActionClips["death"] == "static.death" && clip.Length == 220
                && clip.FrameIndices.Count == 1 && clip.ReleaseMs is null
                && clip.RequireDeath().Mode == ActorDeathMode.DuringClip && clip.RequireDeath().TerminalFadeMs == 0,
                "Every shipped death explicitly retains the original static 220 ms concurrent fade and shrink.");
        }

        var (manifest, bindings) = DeathPolicyFixture();
        bindings.Actors["campaign-torren"].UnitArt.ActionClips["death"] = ReactionDeath;
        var catalog = new AssetCatalog(manifest, bindings);
        var clipAfter = catalog.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds);
        var policy = clipAfter.RequireDeath();
        Require(policy.Mode == ActorDeathMode.AfterClip && policy.TerminalFadeMs == 120 && clipAfter.Length == 256
            && clipAfter.FrameAt(255.999) == 1 && clipAfter.FrameAt(376) == 1,
            "The death policy owns only the terminal fade; authored frame intervals remain owned by ActorClip.");
        Require(catalog.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds, "campaign-torren")
            .RequireDeath().Mode == ActorDeathMode.DuringClip,
            "Named and generic actors select death policy through their action bindings, not their asset identity.");
        Throws(() => catalog.ResolveUnitClip("c01.swordsman", "hit", ActorClipUnit.Milliseconds).RequireDeath(),
            "A non-death clip cannot invent a default death policy.");
        manifest.ActorClips[DeathPolicyClip].Frames[0] = new()
        {
            Frame = "idle",
            Span = 999
        };
        manifest.ActorClips[DeathPolicyClip] = DeathPolicyRecord(new()
        {
            Mode = "during-clip"
        });
        manifest.ActorClips.Clear();
        bindings.Units["c01.swordsman"].ActionClips.Clear();
        bindings.Actors["campaign-torren"].UnitArt.ActionClips.Clear();
        Require(policy.Mode == ActorDeathMode.AfterClip && policy.TerminalFadeMs == 120
            && clipAfter.Length == 256 && clipAfter.FrameAt(0) == 3
            && catalog.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds).RequireDeath() == policy
            && catalog.ResolveUnitClip("c01.swordsman", "death", ActorClipUnit.Milliseconds, "campaign-torren")
                .RequireDeath().Mode == ActorDeathMode.DuringClip,
            "Admitted death policy, frames and named/generic bindings remain detached from all caller-owned declarations.");

        void Reject(ActorClipRecord record, string reason)
        {
            var (invalidManifest, invalidBindings) = DeathPolicyFixture();
            invalidManifest.ActorClips[DeathPolicyClip] = record;
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings), reason);
        }
        Reject(DeathPolicyRecord(null), "A bound death action must explicitly declare its death presentation.");
        foreach (var mode in new[] { "", "After-clip", "after-clip ", " during-clip", "fade", "after_clip" })
            Reject(DeathPolicyRecord(new()
            {
                Mode = mode,
                FadeMs = 120
            }),
                "Unknown or noncanonical death policy modes fail during admission.");
        Reject(DeathPolicyRecord(new()
        {
            Mode = null!,
            FadeMs = 120
        }), "Null death modes cannot become an implicit policy.");
        foreach (var fade in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Reject(DeathPolicyRecord(new()
            {
                Mode = "after-clip",
                FadeMs = fade
            }),
                "After-clip death requires an explicit finite, strictly positive terminal fade.");
        foreach (var fade in new[] { 0, 120, double.NaN })
            Reject(DeathPolicyRecord(new()
            {
                Mode = "during-clip",
                FadeMs = fade
            }),
                "During-clip death cannot carry a competing terminal fade duration.");
        foreach (var mode in new[] { "during-clip", "after-clip" })
        {
            var declaration = new ActorDeathRecord { Mode = mode, FadeMs = mode == "after-clip" ? 120 : null };
            Reject(DeathPolicyRecord(declaration, "cells"), "Death policies cannot attach to distance-sampled clips.");
            foreach (var release in new[] { 0.0, 64.0, 256.0 })
                Reject(DeathPolicyRecord(declaration, release: release), "Death clips cannot also declare an attack release marker.");
        }
        Reject(DeathPolicyRecord(new()
        {
            Mode = "after-clip",
            FadeMs = double.MaxValue
        }, length: double.MaxValue),
            "A terminal fade cannot overflow its clip's finite presentation span.");
        Reject(DeathPolicyRecord(new()
        {
            Mode = "after-clip",
            FadeMs = double.Epsilon
        }),
            "A terminal fade must advance representable time beyond its clip endpoint.");
        foreach (var action in new[] { "idle", "move", "attack", "hit", "celebrate", "fixture.other" })
        {
            var (invalidManifest, invalidBindings) = DeathPolicyFixture();
            invalidBindings.Units["c01.swordsman"].ActionClips[action] = DeathPolicyClip;
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings),
                "Death policy is forbidden on every non-death action, including custom actions.");
        }
        var (namedManifest, namedBindings) = DeathPolicyFixture();
        namedBindings.Actors["campaign-torren"].UnitArt.ActionClips["death"] = ReactionHit;
        Throws(() => new AssetCatalog(namedManifest, namedBindings),
            "Named actor death bindings receive the same eager policy admission as generic units.");
    }

    private async Task CheckDeathPolicyBoundaries(BoardView board, JsonObject state, int id)
    {
        var after = WithoutDeathActor(state, id);
        var published = new List<int>();
        var counts = new List<int>();
        void Presented(BattlePresentationTransition _, int index)
        {
            published.Add(index);
            counts.Add(board.RenderedUnitCount);
        }
        board.EventPresented += Presented;
        try
        {
            foreach (var ms in new[] { 0, 31.999, 32, 95.999, 96, 255.999, 256, 256.001, 316, 375.999, 376, 10000 })
            {
                published.Clear();
                counts.Clear();
                var task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, after, IdleAction(), [ReactionDeathEvent(id), ReactionDeathEvent(id)]));
                var actor = Actor(board, id);
                var origin = actor.Position;
                var contact = actor.GetMeta("logical_contact").AsVector2();
                var exits = 0;
                actor.TreeExiting += () => exits++;
                await Tick(board, ms / 1000);
                if (ms < 376)
                {
                    var expectedFrame = ms < 32 ? 3 : ms < 96 ? 2 : 1;
                    var alpha = ms <= 256 ? 1 : (376 - ms) / 120;
                    Require(!task.IsCompleted && board.IsPresenting && published.Count == 0 && exits == 0
                        && Frame(actor) == expectedFrame && Mathf.IsEqualApprox(actor.SelfModulate.A, (float)alpha),
                        $"After-clip death samples its authored pose/fade and remains unpublished at {ms} ms.");
                    Require(actor.Position == origin && actor.Scale == Vector2.One
                        && actor.GetMeta("logical_contact").AsVector2() == contact && board.RenderedUnitCount == 2,
                        "Falling frames and terminal fade never shrink, displace, or retire the actor early.");
                    CheckClipShadow(actor.GetNode<Sprite2D>("AuthoredSprite"));
                    board.CancelPresentation();
                    Require(!await task, "Boundary witnesses remain cancellable until the terminal fade finishes.");
                }
                else
                    Require(task.IsCompleted && await task && exits == 1 && published.SequenceEqual(new[] { 0, 1 })
                        && counts.SequenceEqual(new[] { 1, 1 }) && board.RenderedUnitCount == 1
                        && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                        "A normal or coarse clock sample removes the actor once at clip plus fade; duplicate deaths add no wait or decrement.");
            }
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckDeathPolicyDeclaredTiming(JsonObject state, int id)
    {
        var (manifest, bindings) = DeathPolicyFixture();
        manifest.ActorClips[DeathPolicyClip] = DeathPolicyRecord(new()
        {
            Mode = "after-clip",
            FadeMs = 80
        }, length: 384);
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)]));
            var actor = Actor(board, id);
            await Tick(board, .384);
            Require(!task.IsCompleted && Frame(actor) == 1 && actor.SelfModulate.A == 1,
                "A second declared clip length owns the complete collapse independently of terminal fade.");
            await Tick(board, .040);
            Require(!task.IsCompleted && Frame(actor) == 1 && Mathf.IsEqualApprox(actor.SelfModulate.A, .5f),
                "The terminal fade samples its declared 80 ms span, not a fixed default or fraction of clip length.");
            await Tick(board, .039);
            Require(!task.IsCompleted, "Declared total 464 ms retains its last frame until the terminal endpoint.");
            await Tick(board, .002);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "Different clip and fade declarations compose at their exact total without a hidden timing constant.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckDeathPolicyColor(BoardView board, JsonObject state, int id)
    {
        var tint = new Color(.2f, .4f, .7f, .6f);
        var scale = new Vector2(.8f, 1.2f);
        var origin = Vector2.Zero;
        void Initialize(BattlePresentationTransition _, int index)
        {
            if (index != 0)
                return;
            var actor = Actor(board, id);
            actor.SelfModulate = tint;
            actor.Scale = scale;
            actor.Position += new Vector2(3, -4);
            origin = actor.Position;
        }
        board.EventPresented += Initialize;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(),
                [new JsonObject { ["type"] = "facingChanged", ["unit"] = id, ["to"] = "east" }, ReactionDeathEvent(id)]));
            var actor = Actor(board, id);
            foreach (var seconds in new[] { 0, .032, .064, .160 })
            {
                await Tick(board, seconds);
                Require(actor.SelfModulate.IsEqualApprox(tint) && actor.Scale == scale && actor.Position == origin,
                    "Every falling frame, including the exact clip/fade boundary, preserves initial RGBA and root transform.");
            }
            Require(Frame(actor) == 1 && !task.IsCompleted, "The complete falling clip holds its last pose before fade begins.");
            await Tick(board, .060);
            Require(actor.SelfModulate.IsEqualApprox(new Color(tint.R, tint.G, tint.B, tint.A / 2))
                && actor.Scale == scale && actor.Position == origin && Frame(actor) == 1,
                "Terminal fade multiplies the captured alpha while preserving RGB, root scale, position and final pose.");
            await Tick(board, .061);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "A tinted, scaled actor still retires through the same terminal endpoint.");
        }
        finally { board.EventPresented -= Initialize; }
    }

    private async Task CheckDeathPolicyLifetime(BoardView board, AssetCatalog catalog, JsonObject state, int id)
    {
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var ms in new[] { 64, 256, 316 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                Require(!task.IsCompleted && Frame(Actor(board, id)) != 0,
                    "Cancellation interrupts a live falling, phase-boundary, or terminal-fade owner.");
                board.CancelPresentation();
                Require(!await task && published.Count == 0 && !board.IsPresenting,
                    "Cancellation in either death phase cannot publish the unfinished event.");
                await Tick(board, 10);
                CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(13, 16)));
                Require(board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Retired death phase callbacks cannot recreate effects or overwrite settled idle art.");

                task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                board.CancelPresentation();
                await Tick(board, 10);
                Require(!await task && published.Count == 0 && board.RenderedUnitCount == 1
                    && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                    "Cancelled death settles the committed dead snapshot without resurrection or a second retirement.");

                task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                ConfigureActorClipBoard(board, assets);
                board.RenderState(state);
                await Tick(board, 10);
                Require(!await task && published.Count == 0 && !board.IsPresenting,
                    "Rebinding during falling, phase boundary, or fade cancels the old timeline without stale publication.");
                CheckSettledClipActor(board, id, assets.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(13, 16)));
                ConfigureActorClipBoard(board, catalog);
            }
            foreach (var ms in new[] { 64, 316 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [ReactionDeathEvent(id)]));
                await Tick(board, ms / 1000.0);
                RemoveChild(board);
                Require(!await task && published.Count == 0, "Tree exit cancels both falling and terminal-fade waiters.");
                AddChild(board);
                ConfigureActorClipBoard(board, catalog);
                board.RenderState(state);
                await Tick(board, 1);
                CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(13, 16)));
            }
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id), ReactionDeathEvent(id)]));
            Require(reduced.IsCompleted && await reduced && published.SequenceEqual(new[] { 0, 1 })
                && board.RenderedUnitCount == 1 && !board.IsPresenting && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Reduced motion settles both death phases synchronously, preserving event order and single removal.");
        }
        finally { board.AnimationsEnabled = true; board.EventPresented -= Presented; }
    }

    private async Task CheckDeathPolicyResult(BoardView board, JsonObject state, int id)
    {
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, WithoutDeathActor(state, id), IdleAction(),
                [ReactionDeathEvent(id), CelebrationEvent(BattleConclusion.LastTeamRemaining(1))]));
            await Tick(board, .256);
            Require(published.Count == 0 && !task.IsCompleted && Actor(board, id).SelfModulate.A == 1,
                "The result event cannot begin at the falling-clip endpoint while terminal fade is pending.");
            await Tick(board, .119);
            Require(published.Count == 0 && !task.IsCompleted && Frame(Actor(board, id)) == 1,
                "Result publication waits through the complete terminal fade while the final death pose remains visible.");
            await Tick(board, .002);
            Require(published.SequenceEqual(new[] { 0 }) && !task.IsCompleted
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "Only terminal retirement releases the result event's own presentation beat.");
            await Drain(board, task);
            Require(published.SequenceEqual(new[] { 0, 1 }), "Death and result each publish once in committed order.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckDeathPolicyVisibility(BoardView board, JsonObject state, int id)
    {
        var (manifest, bindings) = DeathPolicyFixture();
        bindings.Units["c01.swordsman"].ActionClips.Remove("death");
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        var visible = new HashSet<int> { Int(state["units"]![0]!.AsObject(), "id") };
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)], visible), visible);
            Require(task.IsCompleted && await task && published.SequenceEqual(new[] { 0 })
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null && board.GetNodeOrNull("BattleEffects") is null,
                "Hidden death skips both phases before resolving absent art and cannot leak a ghost or extra delay.");
            published.Clear();
            var failed = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)]));
            var rejected = false;
            try
            {
                await failed;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && published.Count == 0 && !board.IsPresenting && board.RenderedUnitCount == 1
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Visible missing death art fails explicitly, settles committed state and releases all presentation gates.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckDeathPolicyLegacy(BoardView board, JsonObject state, int id)
    {
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, WithoutDeathActor(state, id), IdleAction(), [ReactionDeathEvent(id)]));
        var actor = Actor(board, id);
        var origin = actor.Position;
        var frame = Frame(actor);
        await Tick(board, .110);
        Require(Frame(actor) == frame && Mathf.IsEqualApprox(actor.SelfModulate.A, .5f)
            && actor.Scale.IsEqualApprox(Vector2.One * .825f)
            && actor.Position.IsEqualApprox(origin + new Vector2(0, -15) * .175f),
            "Explicit during-clip policy retains the shipped half-time alpha, 35 percent shrink and original billboard pivot.");
        await Tick(board, .109);
        Require(!task.IsCompleted && Frame(actor) == frame, "Legacy death still holds its static pose immediately before 220 ms.");
        await Tick(board, .002);
        Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
            "Legacy death removes at 220 ms without appending the after-clip terminal fade.");
    }

    private async Task CheckDeathPolicyLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var levelId = witness["levelId"]!.GetValue<string>();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(),
            "Death integration begins from the unchanged recorded formal campaign initial state.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        GrayFlagSession? session = null;
        BattleReceipt? deathReceipt = null;
        JsonObject? order = null;
        foreach (var action in witness["actions"]!.AsArray().OfType<JsonObject>())
        {
            var receipt = battle.Dispatch(action);
            if (!receipt.Events.Any(item => item["type"]!.GetValue<string>() == "death"))
                continue;
            var candidate = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(receipt.Before));
            if (candidate.IsComputerTurn)
                continue;
            session = candidate;
            deathReceipt = receipt;
            order = action;
            break;
        }
        Require(deathReceipt is not null && order is not null && session is not null,
            "Recorded legal player-turn campaign commands produce a real committed death without HP or event injection.");
        var events = deathReceipt!.Events;
        var before = deathReceipt.Before;
        var deathIndex = events.ToList().FindIndex(item => item["type"]!.GetValue<string>() == "death");
        var id = Int(events[deathIndex], "unit");
        var unit = before["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == id);
        var expectedHash = battle.StateHash;
        var expectedBefore = StateDigest.Hash(before);
        var expectedEvents = new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString();
        var (manifest, bindings) = DeathPolicyFixture();
        manifest.ActorClips["fixture.legal-death-after-clip"] = new()
        {
            Unit = "milliseconds",
            Death = new()
            {
                Mode = "after-clip",
                FadeMs = 120
            },
            Frames = ReactionClip(256, true).Frames
        };
        var key = unit["key"]?.GetValue<string>();
        var binding = key is not null && bindings.Actors.TryGetValue(key, out var named)
            ? named.UnitArt : bindings.Units[unit["type"]!.GetValue<string>()];
        binding.ActionClips["death"] = "fixture.legal-death-after-clip";
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            var board = screen.Board;
            board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session!,
                new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                recordedLevel, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
            var published = new List<int>();
            board.EventPresented += (_, index) => published.Add(index);
            Require(session!.Dispatch(EcmaJson.ToElement(order!), session.Revision).Accepted,
                "The native screen accepts the original legal campaign order that produces the death receipt.");
            screen.Refresh();
            screen.Refresh();
            for (var step = 0; step < 2000 && published.Count < deathIndex; step++)
                await Tick(board, .010);
            Require(published.Count == deathIndex && screen.IsPresenting && screen.IsInputBlocked
                && Frame(Actor(board, id)) == 3 && Actor(board, id).SelfModulate.A == 1,
                "The real ordered receipt enters falling poses at full opacity and keeps native input gated.");
            await Tick(board, .200);
            screen.Refresh();
            Require(published.Count == deathIndex && screen.IsPresenting && screen.IsInputBlocked
                && Frame(Actor(board, id)) == 1 && Actor(board, id).SelfModulate.A == 1,
                "Repeated screen refresh cannot shorten the real complete falling clip or begin its terminal fade early.");
            await Tick(board, .100);
            screen.Refresh();
            Require(published.Count == deathIndex && screen.IsPresenting && screen.IsInputBlocked
                && Frame(Actor(board, id)) == 1 && Actor(board, id).SelfModulate.A is > 0 and < 1,
                "The real recipient remains on its last pose during terminal fade, still blocking receipt completion.");
            for (var step = 0; screen.IsPresenting && step < 40; step++)
                await Tick(board, 1);
            Require(!screen.IsPresenting && published.SequenceEqual(Enumerable.Range(0, events.Count))
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "The legal death retires once and the native screen consumes every committed event in order.");
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            await Tick(board, 1);
            Require(!screen.IsPresenting && published.Count == events.Count && screen.RecentMessages.SequenceEqual(messages)
                && session.ActionLog.Count == 1 && StateDigest.Hash(session.Snapshot()) == expectedHash
                && StateDigest.Hash(before) == expectedBefore && battle.StateHash == expectedHash
                && new JsonArray(session.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString() == expectedEvents,
                "Settled refreshes cannot replay death, duplicate logs, or mutate the real campaign state, receipt or event order.");
        }
        finally { RemoveChild(screen); screen.Free(); }
    }
}
