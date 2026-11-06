#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const string ReleaseCastAction = "fixture.cast";
    private const string ReleaseCastWeapon = "fixture.cast-stationary";
    private const string ReleaseLungeWeapon = "fixture.cast-lunge";
    private const string ReleaseGenericWeapon = "fixture.generic-stationary";

    private void CheckReleaseDefinitions()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        var weapons = packs.OfType<JsonObject>().SelectMany(pack => (pack["weapons"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()).Select(weapon => weapon["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        Require(weapons.Count == 38 && weapons.SetEquals(shipped.WeaponActions.Keys),
            "Every shipped weapon has exactly one explicit presentation action, including weapons without themed FX.");
        foreach (var weapon in weapons)
        {
            var declaration = shipped.WeaponActions[weapon];
            var action = assets.ResolveWeaponAction(weapon);
            Require(action.Action == declaration.Action && action.Motion == (declaration.Motion == "lunge" ? ActorMotion.Lunge : ActorMotion.Stationary),
                "Weapon presentation resolves the declared action and motion rather than classifying its effect topic.");
        }
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            var shippedClip = assets.ResolveUnitClip(type, "attack", ActorClipUnit.Milliseconds, key);
            Require(shippedClip.Length == 150 && shippedClip.ReleaseMs == 75 && shippedClip.RequireRelease() == 75,
                "Shipped attack and work poses release halfway through their unchanged 150 ms duration.");
        }

        var (manifest, bindings) = ReleaseFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var clip = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds);
        var policy = catalog.ResolveWeaponAction(ReleaseCastWeapon);
        manifest.ActorClips[ClipFixtureId] = ReleaseClip(128, 1);
        bindings.WeaponActions[ReleaseCastWeapon] = new()
        {
            Action = "attack",
            Motion = "lunge"
        };
        bindings.WeaponActions.Clear();
        bindings.Units["c01.swordsman"].ActionClips.Clear();
        Require(clip.ReleaseMs == 64 && clip.RequireRelease() == 64 && clip.Length == 128
            && catalog.ResolveWeaponAction(ReleaseCastWeapon).Action == ReleaseCastAction
            && catalog.ResolveWeaponAction(ReleaseCastWeapon).Motion == ActorMotion.Stationary
            && policy.Action == ReleaseCastAction && policy.Motion == ActorMotion.Stationary,
            "Admitted release markers and weapon policies are detached immutable values, not caller-owned declarations.");
        Throws(() => catalog.ResolveWeaponAction("fixture.unbound"), "Missing weapon action policies must fail explicitly.");
        Throws(() => catalog.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds).RequireRelease(),
            "A clip without a release is valid for idle but cannot silently invent a strike marker.");

        foreach (var release in new[] { -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 128.001 })
        {
            var (invalidManifest, invalidBindings) = ReleaseFixture();
            invalidManifest.ActorClips[ClipFixtureId] = ReleaseClip(128, release);
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings), "Release markers must be finite and inside the timed clip.");
        }
        foreach (var release in new[] { 0.0, 128.0 })
        {
            var (boundaryManifest, boundaryBindings) = ReleaseFixture();
            boundaryManifest.ActorClips[ClipFixtureId] = ReleaseClip(128, release);
            Require(new AssetCatalog(boundaryManifest, boundaryBindings)
                .ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds).RequireRelease() == release,
                "A timed clip can release exactly at its first or final instant.");
        }
        var (cellsManifest, cellsBindings) = ReleaseFixture();
        cellsManifest.ActorClips[ClipFixtureId] = new()
        {
            Unit = "cells",
            ReleaseMs = 0,
            Frames = ClipFixtureFrames()
        };
        Throws(() => new AssetCatalog(cellsManifest, cellsBindings), "Distance clips cannot carry a millisecond release marker.");
        foreach (var (action, motion) in new[] { ("", "lunge"), (" attack", "lunge"), ("attack", ""), ("attack", "Lunge"), ("attack", "teleport") })
        {
            var (invalidManifest, invalidBindings) = ReleaseFixture();
            invalidBindings.WeaponActions[ReleaseCastWeapon] = new()
            {
                Action = action,
                Motion = motion
            };
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings), "Malformed weapon actions and unknown motion policies fail during admission.");
        }
        var (nullManifest, nullBindings) = ReleaseFixture();
        nullBindings.WeaponActions[ReleaseCastWeapon] = null!;
        Throws(() => new AssetCatalog(nullManifest, nullBindings), "A null weapon presentation record cannot become a fallback policy.");
    }

    private async Task CheckReleasePlayback()
    {
        var (manifest, bindings) = ReleaseFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        var state = ReleaseState();
        var aid = Int(state["units"]![0]!.AsObject(), "id");
        var did = Int(state["units"]![1]!.AsObject(), "id");
        try
        {
            await CheckReleaseBoundaries(board, catalog, state, aid, did);
            await CheckWeaponActionIndependence(board, catalog, state, aid, did);
            await CheckReleaseEventScope(board, state, aid, did);
            await CheckReleaseLifetime(board, catalog, state, aid, did);
            await CheckReleaseFailures(board, state, aid, did);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckLongRecoveryRelease();
        await CheckReleaseEndpointsAndLargeTime();
        await CheckReleaseLegalReceipt();
    }

    private async Task CheckReleaseBoundaries(BoardView board, AssetCatalog catalog, JsonObject state, int aid, int did)
    {
        var transition = Transition(state, state, IdleAction(), [Strike("attack", aid, did)]);
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var milliseconds in new[] { 63.999, 64, 64.001 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                var actor = Actor(board, aid);
                var origin = actor.Position;
                var contact = actor.GetMeta("logical_contact").AsVector2();
                var direction = (board.Projection.CellContact(new(13, 16)) - origin).Normalized();
                await Tick(board, milliseconds / 1000);
                Require((board.GetNodeOrNull("BattleEffects/Hit") is not null) == (milliseconds >= 64)
                    && completed.Count == 0 && !task.IsCompleted,
                    "Impact is absent immediately before release and present at and immediately after the exact release instant.");
                Require(actor.Position.IsEqualApprox(origin + direction * 7)
                    && actor.GetMeta("logical_contact").AsVector2() == contact && Frame(actor) == 2,
                    "The lunge peaks at the clip's release while the actor retains its canonical logical contact.");
                CheckClipShadow(actor.GetNode<Sprite2D>("AuthoredSprite"));
                board.CancelPresentation();
                Require(!await task, "An unfinished release boundary witness cancels without publication.");
            }

            var playing = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            var hit = board.GetNode<Node2D>("BattleEffects/Hit");
            var number = hit.GetNode<Label>("Number");
            var numberOrigin = number.Position;
            Require(hit.Position == board.Projection.CellContact(new(13, 16)) && number.Text == "-12"
                && number.Modulate.A == 1 && hit.GetNode<Sprite2D>("Strip").GetMeta("frame").AsInt32() == 0,
                "Release begins one impact at the current recipient contact using unchanged emitted damage.");
            await Tick(board, .032);
            var actorAtRecovery = Actor(board, aid);
            var directionAtRecovery = (board.Projection.CellContact(new(13, 16)) - board.Projection.CellContact(new(12, 16))).Normalized();
            Require(Frame(actorAtRecovery) == 3 && actorAtRecovery.Position.IsEqualApprox(board.Projection.CellContact(new(12, 16))
                    + directionAtRecovery * (float)(7 * Math.Cos(Math.PI / 4)))
                && ReferenceEquals(hit, board.GetNode<Node2D>("BattleEffects/Hit")),
                "Recovery and the already-released impact share one clock without restarting the hit.");
            await Tick(board, .032);
            CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
            Require(ReferenceEquals(hit, board.GetNode<Node2D>("BattleEffects/Hit")) && !playing.IsCompleted,
                "A short actor clip resumes idle at its own end while its longer impact remains active.");
            await Tick(board, .146);
            CheckReleaseImpactSample(hit, numberOrigin, 210);
            await Tick(board, .211);
            Require(await playing && completed.SequenceEqual(new[] { 0 }) && board.GetNodeOrNull("BattleEffects") is null,
                "The receipt completes once at release plus impact duration, without appending another actor duration.");

            completed.Clear();
            playing = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .300);
            CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
            CheckReleaseImpactSample(board.GetNode<Node2D>("BattleEffects/Hit"), numberOrigin, 236);
            Require(completed.Count == 0, "A coarse step crossing both release and clip completion keeps the impact's true elapsed overshoot.");
            await Drain(board, playing);
            completed.Clear();
            playing = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .700);
            Require(playing.IsCompleted && await playing && completed.SequenceEqual(new[] { 0 })
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "One coarse tick across the whole composite action completes immediately without deferred hit playback.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private void CheckReleaseImpactSample(Node2D hit, Vector2 origin, double milliseconds)
    {
        var progress = milliseconds / 420;
        var number = hit.GetNode<Label>("Number");
        Require(number.Position.IsEqualApprox(origin + new Vector2(0, (float)(-14 * progress)))
            && Mathf.IsEqualApprox(number.Modulate.A, (float)(1 - progress * progress))
            && hit.GetNode<Sprite2D>("Strip").GetMeta("frame").AsInt32() == Math.Min(3, (int)(milliseconds * 12 / 1000)),
            "Impact strip, number rise and quadratic fade sample time since release, including overshoot in the release tick.");
    }

    private async Task CheckWeaponActionIndependence(BoardView board, AssetCatalog catalog, JsonObject state, int aid, int did)
    {
        foreach (var (weapon, action, motion, release, firstFrame, topic) in new[]
        {
            ("c01.border-blade", "attack", ActorMotion.Lunge, 64.0, 1, "C01-FX-01"),
            (ReleaseCastWeapon, ReleaseCastAction, ActorMotion.Stationary, 32.0, 3, "C01-FX-01"),
            (ReleaseLungeWeapon, ReleaseCastAction, ActorMotion.Lunge, 32.0, 3, "C01-FX-06"),
            (ReleaseGenericWeapon, "attack", ActorMotion.Stationary, 64.0, 1, (string?)null)
        })
        {
            var item = ReleaseStrike("attack", aid, did, weapon);
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [item]));
            var actor = Actor(board, aid);
            var origin = actor.Position;
            var logical = actor.GetMeta("logical_contact").AsVector2();
            var clip = catalog.ResolveUnitClip("c01.swordsman", action, ActorClipUnit.Milliseconds);
            CheckClipSprite(actor.GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, firstFrame, 1);
            await Tick(board, release / 2000);
            var direction = (board.Projection.CellContact(new(13, 16)) - origin).Normalized();
            Require(actor.Position.IsEqualApprox(origin + (motion == ActorMotion.Lunge ? direction * (float)(7 * Math.Sin(Math.PI / 4)) : Vector2.Zero))
                && board.GetNodeOrNull("BattleEffects/Hit") is null,
                "The same actor chooses approach motion from its weapon policy, independently of clip and FX identity.");
            await Tick(board, release / 2000);
            var hit = board.GetNode<Node2D>("BattleEffects/Hit");
            var strip = hit.GetNodeOrNull<Sprite2D>("Strip");
            Require(actor.Position.IsEqualApprox(origin + (motion == ActorMotion.Lunge ? direction * 7 : Vector2.Zero))
                && actor.GetMeta("logical_contact").AsVector2() == logical
                && (topic is null ? strip is null : strip is not null && strip.GetMeta("topic_id").AsString() == topic)
                && hit.GetNode<Label>("Number").Text == "-12",
                "Same-FX weapons can use different actions and motion; the same action supports another FX or a generic impact.");
            await Drain(board, task);
        }
    }

    private async Task CheckReleaseEventScope(BoardView board, JsonObject state, int aid, int did)
    {
        foreach (var kind in new[] { "counter", "supportAttack", "partingShot", "attackStructure" })
        {
            var before = (JsonObject)state.DeepClone();
            var item = ReleaseStrike(kind, aid, did, ReleaseCastWeapon);
            var target = board.Projection.CellContact(new(13, 16));
            if (kind == "partingShot")
            {
                item["at"] = At(13, 17);
                target = board.Projection.CellCenter(new(13, 17));
            }
            if (kind == "attackStructure")
            {
                var structure = new JsonObject
                {
                    ["id"] = "fixture.release-structure",
                    ["type"] = "command_node",
                    ["owner"] = 2,
                    ["x"] = 13,
                    ["y"] = 17,
                    ["hp"] = 100,
                    ["disabled"] = false
                };
                before["structures"]!.AsArray().Add(structure);
                item["structure"] = structure["id"]!.GetValue<string>();
                item["destroyed"] = false;
                target = board.Projection.CellCenter(new(Int(structure, "x"), Int(structure, "y")));
            }
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(before, before, IdleAction(), [item]));
            Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                $"{kind} selects its weapon's explicit actor action before release.");
            await Tick(board, .032);
            Require(board.GetNode<Node2D>("BattleEffects/Hit").Position == target,
                $"{kind} releases on the declared marker while preserving its event-specific impact lookup.");
            await Drain(board, task);
        }

        var cast = new JsonObject { ["type"] = "castResolved", ["unit"] = aid };
        var events = new[] { cast, ReleaseStrike("areaAttack", aid, did, ReleaseCastWeapon), ReleaseStrike("areaAttack", aid, did, ReleaseCastWeapon) };
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
            Require(completed.SequenceEqual(new[] { 0 }) && Frame(Actor(board, aid)) == 0
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "castResolved remains marker-only and the first area victim starts impact immediately without a caster action.");
            await Tick(board, .421);
            Require(completed.SequenceEqual(new[] { 0, 1 }) && Frame(Actor(board, aid)) == 0
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "The next area victim retains ordered impact-only playback without a grouping heuristic or repeated caster action.");
            await Drain(board, task);
            Require(completed.SequenceEqual(new[] { 0, 1, 2 }), "Area victims preserve exactly one publication per emitted event.");
        }
        finally { board.EventPresented -= Presented; }

        var healing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [new JsonObject { ["type"] = "heal", ["source"] = aid, ["target"] = did, ["amount"] = 20 }]));
        Require(Frame(Actor(board, aid)) == 1 && board.GetNodeOrNull("BattleEffects/Heal") is null,
            "The explicit heal ability selects its source clip without inventing an attack event or changing the recipient yet.");
        await Tick(board, .064);
        Require(Frame(Actor(board, aid)) == 2 && board.GetNode<Label>("BattleEffects/Heal/Number").Text == "+20",
            "Healing releases its original emitted amount at the source clip's declared marker.");
        await Tick(board, .186);
        Require(Frame(Actor(board, aid)) == 0 && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "The healing source rejoins idle while recipient recovery feedback continues, without creating a harmful impact.");
        await Drain(board, healing);
    }

    private async Task CheckLongRecoveryRelease()
    {
        var (manifest, bindings) = ReleaseFixture();
        manifest.ActorClips[ClipFixtureId] = ReleaseClip(640, 64);
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        var state = ReleaseState();
        var aid = Int(state["units"]![0]!.AsObject(), "id");
        var did = Int(state["units"]![1]!.AsObject(), "id");
        var item = Strike("attack", aid, did);
        item["killed"] = true;
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [item]));
            await Tick(board, .300);
            Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "A long recovery remains active after release while its impact advances independently.");
            await Tick(board, .185);
            Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null
                && completed.Count == 0 && !task.IsCompleted,
                "The impact retires at release plus 420 ms even when the actor is still recovering.");
            await Tick(board, .156);
            CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
            Require(!task.IsCompleted && completed.Count == 0, "The fatal beat starts only after both actor recovery and impact have ended.");
            await Tick(board, .039);
            Require(!task.IsCompleted && completed.Count == 0, "Fatal impact keeps the existing full forty-millisecond final beat.");
            await Tick(board, .002);
            Require(await task && completed.SequenceEqual(new[] { 0 }), "Long recovery publishes once after its final fatal beat.");
        }
        finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
    }

    private async Task CheckReleaseLifetime(BoardView board, AssetCatalog catalog, JsonObject state, int aid, int did)
    {
        var transition = Transition(state, state, IdleAction(), [Strike("attack", aid, did)]);
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var milliseconds in new[] { 32, 96, 300 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, milliseconds / 1000.0);
                board.CancelPresentation();
                Require(!await task && completed.Count == 0 && !board.IsPresenting
                    && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Cancellation before release, during overlapping recovery, or during impact alone removes every owned effect without publication.");
                await Tick(board, 1);
                CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
            }

            var old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .096);
            Task<bool>? replacement = null;
            async Task ReplaceAfterCancellation()
            {
                Require(!await old, "The replaced release sequence reports cancellation.");
                replacement = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [ReleaseStrike("attack", aid, did, ReleaseCastWeapon)]));
            }
            var replacing = ReplaceAfterCancellation();
            board.CancelPresentation();
            await replacing;
            var root = board.GetNode<Node2D>("BattleEffects");
            await Tick(board, .032);
            Require(replacement is not null && !replacement.IsCompleted && Frame(Actor(board, aid)) == 2
                && ReferenceEquals(root, board.GetNode<Node2D>("BattleEffects")) && root.GetNodeOrNull("Hit") is not null,
                "Synchronous cancellation replacement keeps its new action, root and release; retired callbacks cannot clear them.");
            await Drain(board, replacement!);
            completed.Clear();

            old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .096);
            ConfigureActorClipBoard(board, assets);
            board.RenderState(state);
            await Tick(board, 1);
            Require(!await old && completed.Count == 0 && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                "Rebinding during overlapping action and impact retires both halves without stale publication.");
            CheckSettledClipActor(board, aid, assets.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
            ConfigureActorClipBoard(board, catalog);

            old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .096);
            RemoveChild(board);
            Require(!await old && !board.IsPresenting && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Tree exit immediately cancels a released action and its impact without touching disposed actor views.");
            AddChild(board);
            ConfigureActorClipBoard(board, catalog);
            board.RenderState(state);
            await Tick(board, 1);
            Require(completed.Count == 0 && board.GetNodeOrNull("BattleEffects") is null, "Reattachment cannot resurrect retired release callbacks.");

            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && completed.SequenceEqual(new[] { 0 }) && !board.IsPresenting
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Reduced motion settles the composite sequence synchronously with one receipt and no retained effect.");
            board.AnimationsEnabled = true;
            completed.Clear();
            var visible = new HashSet<int> { did };
            var hidden = Transition(state, state, IdleAction(), [Strike("attack", aid, did)], visible);
            var playing = board.PresentTransitionAsync(viewportFeedback, hidden, visible);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "A fog-hidden attacker acquires no action and introduces no release delay to its visible recipient's impact.");
            await Drain(board, playing);
            completed.Clear();
            visible = new HashSet<int> { aid };
            hidden = Transition(state, state, IdleAction(), [Strike("attack", aid, did)], visible);
            playing = board.PresentTransitionAsync(viewportFeedback, hidden, visible);
            Require(playing.IsCompleted && await playing && completed.SequenceEqual(new[] { 0 })
                && Frame(Actor(board, aid)) == 0 && board.GetNodeOrNull("BattleEffects") is null,
                "A hidden recipient suppresses the whole strike without exposing an otherwise visible attacker's action.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckReleaseEndpointsAndLargeTime()
    {
        var state = ReleaseState();
        var aid = Int(state["units"]![0]!.AsObject(), "id");
        var did = Int(state["units"]![1]!.AsObject(), "id");
        foreach (var release in new[] { 0.0, 128.0, 1.0 })
        {
            var (manifest, bindings) = ReleaseFixture();
            var huge = release == 1;
            manifest.ActorClips[ClipFixtureId] = huge ? new()
            {
                Unit = "milliseconds",
                AppearanceId = "torren-stage01",
                ReleaseMs = 1,
                Frames = [new() { Frame = "attack", Span = 1.6e308 }]
            } : ReleaseClip(128, release);
            var catalog = new AssetCatalog(manifest, bindings);
            var board = NewActorClipBoard(catalog);
            var completed = 0;
            void Presented(BattlePresentationTransition _, int index) => completed++;
            board.EventPresented += Presented;
            try
            {
                var playing = board.PresentTransitionAsync(viewportFeedback,
                    Transition(state, state, IdleAction(), [Strike("attack", aid, did)]));
                var origin = board.Projection.CellContact(new(12, 16));
                var direction = (board.Projection.CellContact(new(13, 16)) - origin).Normalized();
                if (huge)
                {
                    await Tick(board, 1.2e305);
                    var actor = Actor(board, aid);
                    Require(float.IsFinite(actor.Position.X) && float.IsFinite(actor.Position.Y)
                        && actor.Position.IsEqualApprox(origin + direction * (float)(7 * Math.Cos(Math.PI / 2 * .75)))
                        && Frame(actor) == 2 && board.GetNodeOrNull("BattleEffects/Hit") is null && !playing.IsCompleted,
                        "An admitted huge finite recovery normalizes elapsed time before trigonometry and retires its already-ended impact.");
                    await Tick(board, 1e303);
                    Require(board.GetNode<Node2D>("BattleEffects").GetChildCount() == 0 && completed == 0,
                        "Later samples of a huge recovery cannot recreate an impact that already retired.");
                    board.CancelPresentation();
                    Require(!await playing && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                        && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                        "A huge finite action remains cancellable without stale impact or completion ownership.");
                }
                else
                {
                    Require((board.GetNodeOrNull("BattleEffects/Hit") is not null) == (release == 0),
                        "Release at zero starts the hit immediately; release at Length waits for the declared final instant.");
                    if (release == 0)
                        Require(Actor(board, aid).Position.IsEqualApprox(origin + direction * 7),
                            "A zero-release lunge begins recovery at its finite peak without dividing by zero.");
                    await Tick(board, .128);
                    CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), origin);
                    Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && !playing.IsCompleted,
                        "Both endpoint release policies restore idle at clip end while retaining the correctly timed impact.");
                    await Drain(board, playing);
                    Require(completed == 1, "Endpoint release markers publish the event exactly once.");
                }
            }
            finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
        }
    }

    private async Task CheckReleaseFailures(BoardView board, JsonObject state, int aid, int did)
    {
        foreach (var failure in new[] { "policy", "action", "release", "unit", "resource" })
        {
            var (manifest, bindings) = ReleaseFixture();
            if (failure == "policy")
                bindings.WeaponActions.Remove(ReleaseCastWeapon);
            else if (failure == "action")
                bindings.Units["c01.swordsman"].ActionClips.Remove(ReleaseCastAction);
            else if (failure == "resource")
            {
                manifest.Assets.Add(new()
                {
                    TopicId = "fixture.missing-release-fx",
                    Category = "fx",
                    ResourcePath = "res://assets/fixtures/missing-release-fx.png",
                    Width = 32,
                    Height = 32,
                    Frames = 1,
                    PhysicalWidth = 32,
                    PhysicalHeight = 32,
                    PixelDensity = 1,
                    Anchor = [16, 16],
                    GroundFront = [16, 16]
                });
                bindings.WeaponEffects[ReleaseCastWeapon] = "fixture.missing-release-fx";
            }
            else
                manifest.ActorClips[ReleaseCastAction] = new()
                {
                    Unit = failure == "unit" ? "cells" : "milliseconds",
                    AppearanceId = "torren-stage01",
                    Frames = ClipFixtureFrames()
                };
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
            var failed = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [ReleaseStrike("attack", aid, did, ReleaseCastWeapon)]));
            if (failure == "resource")
            {
                Require(!failed.IsCompleted && Frame(Actor(board, aid)) == 3,
                    "The resource-failure witness admits and begins its valid action before impact creation fails at release.");
                await Tick(board, .032);
            }
            var rejected = false;
            try
            {
                await failed;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                $"Missing or invalid {failure} fails explicitly and releases both presentation roots instead of inventing an action.");
            CheckSettledClipActor(board, aid, assets.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(12, 16)));
        }
    }

    private async Task CheckReleaseLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var checkpoint = witness["checkpoints"]![0]!.AsObject();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == witness["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(), "Release integration begins at the unchanged recorded C06 state.");
        var index = Int(checkpoint, "index");
        foreach (var order in witness["actions"]!.AsArray().Take(index).OfType<JsonObject>())
            battle.Dispatch(order);
        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), "The accepted C06 prefix reaches the exact recorded attack checkpoint.");
        var action = witness["actions"]![index]!.AsObject();
        var receipt = battle.Dispatch(action);
        var attack = receipt.Events.First(value => value["type"]!.GetValue<string>() == "attack");
        var id = Int(attack, "attacker");
        var unit = receipt.Before["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == id);
        var type = unit["type"]!.GetValue<string>();
        var key = unit["key"]?.GetValue<string>();
        var (manifest, bindings) = ReleaseFixture();
        // This synthetic timing witness reuses the real actor's trusted sheet, not new accepted animation art.
        manifest.ActorClips["fixture.legal-release"] = new()
        {
            Unit = "milliseconds",
            ReleaseMs = 64,
            Frames = ClipFixtureFrames()
        };
        var binding = key is not null && bindings.Actors.TryGetValue(key, out var named) ? named.UnitArt : bindings.Units[type];
        binding.ActionClips[bindings.WeaponActions[attack["weapon"]!.GetValue<string>()].Action] = "fixture.legal-release";
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric),
                recordedLevel, AuthoredScene.Load($"res://data/scenes/{witness["levelId"]!.GetValue<string>()}.json"));
            board.ConfigureDynamicState();
            var after = battle.Snapshot();
            var transition = Transition(receipt.Before, after, action, receipt.Events);
            var source = transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
                + string.Concat(transition.Events.Select(value => value.GetRawText()));
            var completed = new List<int>();
            void Presented(BattlePresentationTransition _, int eventIndex) => completed.Add(eventIndex);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                var movementMs = (action["path"]!.AsArray().Count - 1) * 160;
                await Tick(board, (movementMs + 1) / 1000.0);
                Require(Frame(Actor(board, id)) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The real accepted command hands movement ownership to its selected actor action without premature impact.");
                await Tick(board, .065);
                Require(Frame(Actor(board, id)) == 2 && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-" + attack["damage"]!.ToJsonString(),
                    "The real receipt releases its unchanged damage during the selected clip rather than after recovery.");
                await Drain(board, task);
                Require(completed.SequenceEqual(Enumerable.Range(0, receipt.Events.Count))
                    && battle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>()
                    && StateDigest.Hash(after) == checkpoint["afterStateHash"]!.GetValue<string>()
                    && EcmaJson.Sha256(new JsonArray(receipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                        == checkpoint["eventsSha256"]!.GetValue<string>()
                    && source == transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
                        + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "Release playback preserves C06's exact canonical before/after hashes, event bytes, order and immutable receipt.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private static ActorClipRecord ReleaseClip(double length, double? release) => new()
    {
        Unit = "milliseconds",
        AppearanceId = "torren-stage01",
        ReleaseMs = release,
        Frames = new[]
        {
            new ActorClipFrameRecord { Frame = "walk-a", Span = 32 },
            new ActorClipFrameRecord { Frame = "attack", Span = 64 },
            new ActorClipFrameRecord { Frame = "walk-b", Span = length - 96 }
        }
    };

    private static (AssetManifest Manifest, ArtBindings Bindings) ReleaseFixture()
    {
        var (manifest, bindings) = ActorClipFixture();
        // Trusted existing pictures are deliberately sequenced as synthetic timing fixtures, not new character animation.
        manifest.ActorClips[ClipFixtureId] = ReleaseClip(128, 64);
        manifest.ActorClips[ReleaseCastAction] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            ReleaseMs = 32,
            Frames = new[]
            {
                new ActorClipFrameRecord { Frame = "walk-b", Span = 32 },
                new ActorClipFrameRecord { Frame = "attack", Span = 64 },
                new ActorClipFrameRecord { Frame = "walk-a", Span = 96 }
            }
        };
        bindings.Units["c01.swordsman"].ActionClips[ReleaseCastAction] = ReleaseCastAction;
        bindings.WeaponActions["c01.border-blade"] = new()
        {
            Action = "attack",
            Motion = "lunge"
        };
        bindings.WeaponActions[ReleaseCastWeapon] = new()
        {
            Action = ReleaseCastAction,
            Motion = "stationary"
        };
        bindings.WeaponActions[ReleaseLungeWeapon] = new()
        {
            Action = ReleaseCastAction,
            Motion = "lunge"
        };
        bindings.WeaponActions[ReleaseGenericWeapon] = new()
        {
            Action = "attack",
            Motion = "stationary"
        };
        bindings.WeaponEffects[ReleaseCastWeapon] = "C01-FX-01";
        bindings.WeaponEffects[ReleaseLungeWeapon] = "C01-FX-06";
        return (manifest, bindings);
    }

    private JsonObject ReleaseState()
    {
        // Synthetic receipt data tests presentation only; these are actual supported C01 cells, not legal combat orders.
        var state = Copy();
        var units = state["units"]!.AsArray().OfType<JsonObject>().Take(2).ToArray();
        foreach (var unit in units)
        {
            unit["type"] = "c01.swordsman";
            unit.Remove("key");
            unit["y"] = 16;
        }
        units[0]["x"] = 12;
        units[1]["x"] = 13;
        return state;
    }

    private static JsonObject ReleaseStrike(string kind, int aid, int did, string weapon)
    {
        var item = Strike(kind, aid, did);
        item["weapon"] = weapon;
        return item;
    }
}
