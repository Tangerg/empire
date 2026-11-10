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
    private void CheckHealingDefinitions()
    {
        var selected = assets.ResolveAbilityAction("heal");
        Require(selected.Action == "attack" && selected.Motion == ActorMotion.Stationary,
            "Shipped healing explicitly selects the existing attack pose and stationary motion.");
        foreach (var type in new[] { "cleric", "c01.mirelle", "c01.gravekeeper", "c01.druid" })
        {
            var clip = assets.ResolveUnitClip(type, selected.Action, ActorClipUnit.Milliseconds);
            Require(clip.Length == (type == "c01.gravekeeper" ? 600 : 150)
                && clip.RequireRelease() == (type == "c01.gravekeeper" ? 300 : 75),
                "Every shipped healer admits the selected timed actor action and release marker.");
        }
        var (manifest, bindings) = HealingFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var detached = catalog.ResolveAbilityAction("heal");
        bindings.AbilityActions["heal"] = new()
        {
            Action = "attack",
            Motion = "lunge"
        };
        bindings.AbilityActions.Clear();
        Require(detached.Action == ReleaseCastAction && detached.Motion == ActorMotion.Stationary
            && catalog.ResolveAbilityAction("heal") == detached,
            "Ability action selection is an immutable admitted value detached from mutable input records.");
        Throws(() => catalog.ResolveAbilityAction("missing"), "Missing ability policy fails instead of selecting a weapon action.");
        foreach (var (action, motion) in new[] { ("", "stationary"), (" attack", "stationary"), ("attack", "teleport") })
        {
            var (invalidManifest, invalidBindings) = HealingFixture();
            invalidBindings.AbilityActions["heal"] = new()
            {
                Action = action,
                Motion = motion
            };
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings), "Invalid ability action and motion declarations fail admission.");
        }
        var (nullManifest, nullBindings) = HealingFixture();
        nullBindings.AbilityActions["heal"] = null!;
        Throws(() => new AssetCatalog(nullManifest, nullBindings), "Null ability declarations cannot silently fall back.");
    }

    private static (AssetManifest Manifest, ArtBindings Bindings) HealingFixture()
    {
        // Synthetic timing only: existing trusted frames, deliberately different from the weapon action.
        var (manifest, bindings) = ReleaseFixture();
        bindings.AbilityActions["heal"] = new()
        {
            Action = ReleaseCastAction,
            Motion = "stationary"
        };
        return (manifest, bindings);
    }

    private static JsonObject HealingEvent(int source, int target) => new()
    {
        ["type"] = "heal",
        ["source"] = source,
        ["target"] = target,
        ["amount"] = 20
    };

    private async Task CheckHealingPlayback()
    {
        var (manifest, bindings) = HealingFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        var state = ReleaseState();
        var source = Int(state["units"]![0]!.AsObject(), "id");
        var target = Int(state["units"]![1]!.AsObject(), "id");
        try
        {
            await CheckHealingClock(board, catalog, state, source, target);
            await CheckHealingVisibility(board, catalog, state, source, target);
            await CheckHealingLifetime(board, catalog, state, source, target);
            await CheckHealingFailures(board, state, source, target);
            await CheckHealingEffectIndependence(board, state, source, target);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckHealingLongRecovery(state, source, target);
    }

    private async Task CheckHealingClock(BoardView board, AssetCatalog catalog, JsonObject state, int source, int target)
    {
        var transition = Transition(state, state, IdleAction(), [HealingEvent(source, target)]);
        foreach (var milliseconds in new[] { 31.999, 32, 32.001 })
        {
            var playing = board.PresentTransitionAsync(viewportFeedback, transition);
            var actor = Actor(board, source);
            var origin = actor.Position;
            Require(Frame(actor) == 3 && Frame(Actor(board, target)) == 0,
                "Healing chooses its ability clip, not the source weapon's attack clip or the recipient actor.");
            await Tick(board, milliseconds / 1000);
            Require((board.GetNodeOrNull("BattleEffects/Heal") is not null) == (milliseconds >= 32)
                && actor.Position == origin && !playing.IsCompleted,
                "Stationary ability release has exact before/at/after boundaries independent of weapon lunge and FX.");
            board.CancelPresentation();
            Require(!await playing, "A cancelled healing boundary witness publishes no completion.");
        }
        var task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .032);
        var heal = board.GetNode<Node2D>("BattleEffects/Heal");
        var number = heal.GetNode<Label>("Number");
        var numberOrigin = number.Position;
        Require(heal.Position == Actor(board, target).Position && number.Text == "+20" && Frame(Actor(board, source)) == 2,
            "Healing releases one unchanged recipient effect while the selected source clip recovers.");
        await Tick(board, .160);
        Require(Frame(Actor(board, source)) == 0 && ReferenceEquals(heal, board.GetNode<Node2D>("BattleEffects/Heal"))
            && !task.IsCompleted, "Ability clip ends independently at 192 ms without restarting its healing effect.");
        await Drain(board, task);
        task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .300);
        heal = board.GetNode<Node2D>("BattleEffects/Heal");
        number = heal.GetNode<Label>("Number");
        const double progress = 268.0 / 500;
        Require(Frame(Actor(board, source)) == 0
            && number.Position.IsEqualApprox(numberOrigin + new Vector2(0, (float)(-16 * progress)))
            && Mathf.IsEqualApprox(number.Modulate.A, (float)(1 - progress * progress)),
            "A coarse tick samples healing rise and fade from release overshoot, not from the tick's endpoint.");
        await Drain(board, task);
        task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .600);
        Require(task.IsCompleted && await task && board.GetNodeOrNull("BattleEffects") is null,
            "One coarse tick spans the whole healing composite without an appended second timer.");

        // Presenter robustness only: current legal heal targeting excludes self.
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [HealingEvent(source, source)]));
        var same = Actor(board, source);
        await Tick(board, .032);
        Require(ReferenceEquals(same, Actor(board, source)) && Frame(same) == 2
            && board.GetNode<Node2D>("BattleEffects/Heal").Position == same.Position,
            "Synthetic self-identity uses the existing actor once and releases at its own contact.");
        await Drain(board, task);
    }

    private async Task CheckHealingVisibility(BoardView board, AssetCatalog catalog, JsonObject state, int source, int target)
    {
        var visible = new HashSet<int> { target };
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), [HealingEvent(source, target)], visible), visible);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{source}") is null
            && board.GetNodeOrNull("BattleEffects/Heal") is not null,
            "A hidden caster retains immediate target-only recovery without exposing source animation timing.");
        await Drain(board, task);
        foreach (var recipient in new[] { target, int.MaxValue })
        {
            visible = new HashSet<int> { source };
            task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(source, recipient)], visible), visible);
            Require(task.IsCompleted && await task && Frame(Actor(board, source)) == 0
                && board.GetNodeOrNull("BattleEffects") is null,
                "Hidden or missing healing recipients suppress the entire event, including visible caster motion.");
        }
        task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), [HealingEvent(int.MaxValue, target)]));
        Require(board.GetNodeOrNull("BattleEffects/Heal") is not null,
            "A missing source retains the visible recipient's immediate healing feedback.");
        await Drain(board, task);
        var (manifest, bindings) = HealingFixture();
        bindings.AbilityActions.Remove("heal");
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        foreach (var caster in new[] { source, int.MaxValue })
        {
            visible = new HashSet<int> { target };
            task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(caster, target)], visible), visible);
            Require(!task.IsFaulted, "Hidden or absent casters must not resolve an unavailable ability action policy.");
            Require(board.GetNodeOrNull("BattleEffects/Heal") is not null,
                "A visible recipient starts immediate recovery even when its unseen caster has no admitted ability policy.");
            await Drain(board, task);
        }
        foreach (var recipient in new[] { target, int.MaxValue })
        {
            visible = new HashSet<int> { source };
            task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(source, recipient)], visible), visible);
            Require(!task.IsFaulted && task.IsCompleted && await task && Frame(Actor(board, source)) == 0
                && board.GetNodeOrNull("BattleEffects") is null,
                "Hidden or absent recipients suppress healing before attempting an unavailable caster ability policy.");
        }
        ConfigureActorClipBoard(board, catalog);
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [new JsonObject { ["type"] = "regen", ["unit"] = target, ["amount"] = 20 }]));
        Require(Frame(Actor(board, target)) == 0 && board.GetNodeOrNull("BattleEffects/Heal") is not null,
            "Regeneration stays immediate target-only feedback rather than selecting the healing ability action.");
        await Drain(board, task);
    }

    private async Task CheckHealingLifetime(BoardView board, AssetCatalog catalog, JsonObject state, int source, int target)
    {
        var transition = Transition(state, state, IdleAction(), [HealingEvent(source, target)]);
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var time in new[] { .016, .064, .300 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, time);
                board.CancelPresentation();
                Require(!await task && completed.Count == 0 && !board.IsPresenting
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation before release, during recovery or during healing alone retires all ownership without publication.");
                await Tick(board, 1);
                Require(Frame(Actor(board, source)) == 0, "Cancelled healing cannot resurrect the caster pose.");
            }
            var old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            Task<bool>? replacement = null;
            async Task Replace()
            {
                Require(!await old, "Replaced healing reports cancellation.");
                replacement = board.PresentTransitionAsync(viewportFeedback, transition);
            }
            var replacing = Replace();
            board.CancelPresentation();
            await replacing;
            await Tick(board, .032);
            Require(replacement is not null && !replacement.IsCompleted && Frame(Actor(board, source)) == 2
                && board.GetNodeOrNull("BattleEffects/Heal") is not null,
                "Synchronous cancellation replacement retains its own caster and heal effect after retired cleanup.");
            await Drain(board, replacement!);
            Require(completed.SequenceEqual(new[] { 0 }), "Only the replacement healing event publishes once.");
            completed.Clear();
            old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            ConfigureActorClipBoard(board, assets);
            board.RenderState(state);
            await Tick(board, 1);
            Require(!await old && completed.Count == 0 && board.GetNodeOrNull("BattleEffects") is null,
                "Rebinding retires both released healing and caster ownership without stale publication.");
            ConfigureActorClipBoard(board, catalog);
            old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            RemoveChild(board);
            Require(!await old && completed.Count == 0 && !board.IsPresenting
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Tree exit cancels overlapping healing and caster recovery, releases both effect roots and publishes nothing.");
            AddChild(board);
            ConfigureActorClipBoard(board, catalog);
            board.RenderState(state);
            await Tick(board, 1);
            Require(completed.Count == 0 && Frame(Actor(board, source)) == 0
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Readding the healing board cannot resurrect its retired caster, effects or publication callback.");
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && completed.SequenceEqual(new[] { 0 })
                && Frame(Actor(board, source)) == 0 && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion settles healing synchronously with one event and no retained actor action.");
            board.AnimationsEnabled = true;
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckHealingLongRecovery(JsonObject state, int source, int target)
    {
        var (manifest, bindings) = HealingFixture();
        manifest.ActorClips[ReleaseCastAction] = ReleaseClip(640, 32);
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(source, target)]));
            await Tick(board, .533);
            Require(!task.IsCompleted && Frame(Actor(board, source)) != 0 && board.GetNodeOrNull("BattleEffects/Heal") is null,
                "Healing retires after release plus 500 ms even when its caster's recovery continues longer.");
            await Tick(board, .020);
            Require(board.GetNodeOrNull("BattleEffects/Heal") is null, "Later recovery samples cannot recreate a retired heal.");
            await Tick(board, .088);
            Require(task.IsCompleted && await task && Frame(Actor(board, source)) == 0,
                "Long caster recovery finishes at its own 640 ms endpoint rather than appending healing duration.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private static ArtBindings HealingEffectBindings(ArtBindings bindings, string? topic) => new()
    {
        AbilityActions = bindings.AbilityActions,
        WeaponActions = bindings.WeaponActions,
        Units = bindings.Units,
        Actors = bindings.Actors,
        MapStructures = bindings.MapStructures,
        Structures = bindings.Structures,
        WeaponEffects = bindings.WeaponEffects,
        HealEffect = topic
    };

    private async Task CheckHealingEffectIndependence(BoardView board, JsonObject state, int source, int target)
    {
        foreach (var topic in new[] { "C01-FX-01", "C01-FX-06", (string?)null })
        {
            var (manifest, bindings) = HealingFixture();
            bindings.WeaponActions.Clear();
            bindings.WeaponEffects.Clear();
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, HealingEffectBindings(bindings, topic)));
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(source, target)]));
            var origin = Actor(board, source).Position;
            Require(Frame(Actor(board, source)) == 3 && board.GetNodeOrNull("BattleEffects/Heal") is null,
                "Ability action selection needs no weapon policy or weapon FX, including generic healing feedback.");
            await Tick(board, .032);
            var strip = board.GetNodeOrNull<Sprite2D>("BattleEffects/Heal/Strip");
            Require(Frame(Actor(board, source)) == 2 && Actor(board, source).Position == origin
                && (topic is null ? strip is null : strip is not null && strip.GetMeta("topic_id").AsString() == topic),
                "Changing the heal effect or omitting its strip does not change the selected caster action, motion or release.");
            await Drain(board, task);
        }
    }

    private async Task CheckHealingFailures(BoardView board, JsonObject state, int source, int target)
    {
        foreach (var failure in new[] { "policy", "action", "release", "unit", "resource" })
        {
            var (manifest, bindings) = HealingFixture();
            if (failure == "policy")
                bindings.AbilityActions.Remove("heal");
            else if (failure == "action")
                bindings.Units["c01.swordsman"].ActionClips.Remove(ReleaseCastAction);
            else if (failure == "resource")
            {
                manifest.Assets.Add(new()
                {
                    TopicId = "fixture.missing-heal-fx",
                    Category = "fx",
                    ResourcePath = "res://assets/fixtures/missing-heal-fx.png",
                    Width = 32,
                    Height = 32,
                    Frames = 1,
                    PhysicalWidth = 32,
                    PhysicalHeight = 32,
                    PixelDensity = 1,
                    Anchor = [16, 16],
                    GroundFront = [16, 16]
                });
                bindings = HealingEffectBindings(bindings, "fixture.missing-heal-fx");
            }
            else
                manifest.ActorClips[ReleaseCastAction] = new()
                {
                    Unit = failure == "unit" ? "cells" : "milliseconds",
                    AppearanceId = "torren-stage01",
                    Frames = ClipFixtureFrames()
                };
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, state, IdleAction(), [HealingEvent(source, target)]));
            if (failure == "resource")
            {
                Require(!task.IsCompleted && Frame(Actor(board, source)) == 3,
                    "A valid caster action starts before its missing impact resource fails at release.");
                await Tick(board, .032);
            }
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                && Frame(Actor(board, source)) == 0,
                $"Healing with missing or invalid {failure} fails explicitly and cleans up rather than choosing a weapon fallback.");
        }
    }

    private async Task CheckHealingLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonObject>("res://tests/native/c01-12-healing-target.json");
        var levelId = witness["levelId"]!.GetValue<string>();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(),
            "Healing integration starts from the unchanged formal C12 initial state.");
        var index = Int(witness, "index");
        foreach (var order in witness["actions"]!.AsArray().Take(index).OfType<JsonObject>())
            battle.Dispatch(order);
        Require(battle.StateHash == witness["beforeStateHash"]!.GetValue<string>(),
            "The real recorded C12 orders reach the exact healing checkpoint without state injection.");
        var action = witness["actions"]![index]!.AsObject();
        var receipt = battle.Dispatch(action);
        var heal = receipt.Events.Single(value => value["type"]!.GetValue<string>() == "heal");
        var sourceId = Int(heal, "source");
        var targetId = Int(heal, "target");
        Require(sourceId == 4 && targetId == 6 && action["path"]!.AsArray().Count == 1,
            "The legal C12 witness heals a distinct ally from its current cell, not a synthetic self-heal.");
        var after = battle.Snapshot();
        var transition = Transition(receipt.Before, after, action, receipt.Events);
        var original = transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
            + string.Concat(transition.Events.Select(value => value.GetRawText()));
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric),
                recordedLevel, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
            board.ConfigureDynamicState();
            var completed = new List<int>();
            void Presented(BattlePresentationTransition _, int eventIndex) => completed.Add(eventIndex);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                var source = Actor(board, sourceId);
                var origin = source.Position;
                var target = Actor(board, targetId);
                Require(Frame(source) == 2 && source.Position == origin && Frame(target) == 0
                    && board.GetNodeOrNull("BattleEffects/Heal") is null,
                    "The legal C12 heal starts the caster's explicit stationary attack pose, not the recipient's pose or feedback.");
                await Tick(board, .075);
                var effect = board.GetNode<Node2D>("BattleEffects/Heal");
                Require(Frame(source) == 2 && source.Position == origin && effect.Position == target.Position
                    && effect.GetNode<Label>("Number").Text == "+27",
                    "C12 releases its unchanged +27 healing at the shipped 75 ms marker during caster recovery.");
                await Tick(board, .075);
                Require(Frame(source) == 0 && source.Position == origin && ReferenceEquals(effect, board.GetNode<Node2D>("BattleEffects/Heal"))
                    && !task.IsCompleted,
                    "C12 caster resumes idle at 150 ms while the same recipient effect continues.");
                await Tick(board, .424);
                Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Heal") is not null,
                    "C12 healing stays active until release plus its unchanged 500 ms duration.");
                await Tick(board, .002);
                await Drain(board, task);
                Require(completed.SequenceEqual(Enumerable.Range(0, receipt.Events.Count))
                    && StateDigest.Hash(receipt.Before) == witness["beforeStateHash"]!.GetValue<string>()
                    && battle.StateHash == witness["afterStateHash"]!.GetValue<string>()
                    && StateDigest.Hash(after) == witness["afterStateHash"]!.GetValue<string>()
                    && EcmaJson.Sha256(new JsonArray(receipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                        == witness["eventsSha256"]!.GetValue<string>()
                    && original == transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
                        + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "Healing presentation preserves C12's initial/before/after witness, emitted event bytes, ordered publication and immutable receipt.");
                GD.Print("Healing C12: stationary source 4, release75, target6 +27, idle150, effect575; all recorded hashes unchanged.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
