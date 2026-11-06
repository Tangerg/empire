#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Localization;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private static JsonObject Activation(int source, string weapon = ReleaseCastWeapon) => new()
    {
        ["type"] = "attackActivated",
        ["attacker"] = source,
        ["weapon"] = weapon,
        ["from"] = At(12, 16),
        ["at"] = At(13, 16)
    };

    private static JsonObject ActivationCompleted(int source, string weapon = ReleaseCastWeapon) => new()
    {
        ["type"] = "attackCompleted",
        ["attacker"] = source,
        ["weapon"] = weapon
    };

    private static JsonObject[] ActivationScope(int source, params JsonObject[] impacts) =>
        new[] { Activation(source) }.Concat(impacts).Append(ActivationCompleted(source)).ToArray();

    private async Task CheckActivationPlayback()
    {
        var (manifest, bindings) = ReleaseFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        var state = ReleaseState();
        var source = Int(state["units"]![0]!.AsObject(), "id");
        var target = Int(state["units"]![1]!.AsObject(), "id");
        try
        {
            await CheckActivationClock(board, state, source, target);
            await CheckActivationTargets(board, state, source, target);
            await CheckActivationVisibility(board, catalog, state, source, target);
            await CheckActivationLifetime(board, catalog, state, source, target);
            await CheckActivationFailures(board, state, source, target);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckActivationLongRecovery(state, source, target);
        await CheckActivationUnrepresentableImpact(state, source, target);
    }

    private async Task CheckActivationClock(BoardView board, JsonObject state, int source, int target)
    {
        var events = ActivationScope(source, ReleaseStrike("attack", source, target, ReleaseCastWeapon));
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var milliseconds in new[] { 31.999, 32, 32.001 })
            {
                completed.Clear();
                var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
                Require(Frame(Actor(board, source)) == 3 && Frame(Actor(board, target)) == 0,
                    "Activation selects the explicit source's alternate clip, never the victim's pose.");
                var origin = Actor(board, source).Position;
                await Tick(board, milliseconds / 1000);
                Require((board.GetNodeOrNull("BattleEffects/Hit") is not null) == (milliseconds >= 32)
                    && completed.SequenceEqual(milliseconds >= 32 ? new[] { 0 } : Array.Empty<int>())
                    && Actor(board, source).Position == origin && !task.IsCompleted,
                    "Activation publishes at its exact release, opening impact while stationary recovery continues.");
                board.CancelPresentation();
                Require(!await task, "Cancelled activation cannot publish receipt completion.");
            }
            completed.Clear();
            var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
            await Tick(board, .032);
            var hit = board.GetNode<Node2D>("BattleEffects/Hit");
            var numberOrigin = hit.GetNode<Label>("Number").Position;
            Require(hit.Position == Actor(board, target).Position && Frame(Actor(board, source)) == 2,
                "Released direct damage lands on the target while the single source action recovers.");
            await Tick(board, .160);
            Require(Frame(Actor(board, source)) == 0 && ReferenceEquals(hit, board.GetNode<Node2D>("BattleEffects/Hit")),
                "Source returns to idle at its own clip end without replacing its active impact.");
            await Drain(board, playing);
            Require(completed.SequenceEqual(new[] { 0, 1, 2 }), "Each activation, impact and completion publishes exactly once.");
            completed.Clear();
            playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
            await Tick(board, .300);
            CheckReleaseImpactSample(board.GetNode<Node2D>("BattleEffects/Hit"), numberOrigin, 268);
            Require(Frame(Actor(board, source)) == 0 && completed.SequenceEqual(new[] { 0 }),
                "One coarse tick preserves release overshoot across the explicit event boundary.");
            await Tick(board, .200);
            Require(playing.IsCompleted && await playing && completed.SequenceEqual(new[] { 0, 1, 2 }),
                "A coarse tick consumes chained impact and completion boundaries on the same clock.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckActivationTargets(BoardView board, JsonObject state, int source, int target)
    {
        foreach (var kind in new[] { "attack", "areaAttack" })
        {
            var bare = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                [ReleaseStrike(kind, source, target, ReleaseCastWeapon)]));
            Require(Frame(Actor(board, source)) == 0 && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "An isolated main-attack impact remains target-only and cannot infer an activation.");
            await Drain(board, bare);
        }
        var marker = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [new JsonObject { ["type"] = "castResolved", ["unit"] = source }]));
        Require(marker.IsCompleted && await marker && Frame(Actor(board, source)) == 0,
            "castResolved alone cannot manufacture an attack activation.");
        var events = ActivationScope(source,
            ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon),
            ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon));
        var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
        await Tick(board, .032);
        var first = board.GetNode<Node2D>("BattleEffects/Hit");
        await Tick(board, .421);
        var second = board.GetNode<Node2D>("BattleEffects/Hit");
        Require(!ReferenceEquals(first, second) && Frame(Actor(board, source)) == 0 && !task.IsCompleted,
            "Multiple area victims retain ordered impacts without restarting the single activation pose.");
        await Drain(board, task);

        foreach (var kind in new[] { "attackStructure", "areaAttackStructure" })
        {
            var structures = (JsonObject)state.DeepClone();
            structures["structures"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "fixture.activation-structure",
                ["type"] = "command_node",
                ["owner"] = 2,
                ["x"] = 13,
                ["y"] = 17,
                ["hp"] = 100,
                ["disabled"] = false
            });
            var impact = ReleaseStrike(kind, source, target, ReleaseCastWeapon);
            impact["structure"] = "fixture.activation-structure";
            impact["destroyed"] = false;
            task = board.PresentTransitionAsync(viewportFeedback,
                Transition(structures, structures, IdleAction(), ActivationScope(source, impact)));
            Require(Frame(Actor(board, source)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                "A structure-only scope begins its source action without requiring a unit victim.");
            await Tick(board, .032);
            Require(board.GetNode<Node2D>("BattleEffects/Hit").Position == board.Projection.CellCenter(new(13, 17)),
                "Structure impacts keep their own position rather than inheriting the activation aim or a unit contact.");
            await Drain(board, task);
        }
        task = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), ActivationScope(source)));
        Require(Frame(Actor(board, source)) == 3 && !task.IsCompleted,
            "An empty explicit scope still performs its source action without inferring a first victim.");
        await Tick(board, .191);
        Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "An empty scope waits for recovery without inventing an impact.");
        await Tick(board, .002);
        Require(task.IsCompleted && await task && Frame(Actor(board, source)) == 0,
            "An empty scope finishes exactly after the one source clip.");

        var activation = Activation(source, ReleaseLungeWeapon);
        activation["at"] = At(12, 17);
        task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), new[]
        {
            activation, ReleaseStrike("areaAttack", source, target, ReleaseLungeWeapon),
            ActivationCompleted(source, ReleaseLungeWeapon)
        }));
        var origin = Actor(board, source).Position;
        await Tick(board, .032);
        var direction = (board.Projection.CellContact(new(12, 17)) - origin).Normalized();
        Require(Actor(board, source).Position.IsEqualApprox(origin + direction * 7)
            && board.GetNode<Node2D>("BattleEffects/Hit").Position == Actor(board, target).Position,
            "Source motion follows explicit activation aim even when the first victim is elsewhere.");
        await Drain(board, task);
    }

    private async Task CheckActivationVisibility(BoardView board, AssetCatalog catalog, JsonObject state, int source, int target)
    {
        var events = ActivationScope(source, ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon));
        foreach (var visible in new[] { new HashSet<int> { source }, new HashSet<int> { target }, new HashSet<int>() })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events, visible), visible);
            if (visible.Contains(source))
                Require(Frame(Actor(board, source)) == 3 && !task.IsCompleted && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "A visible source acts even when every victim is hidden.");
            else if (visible.Contains(target))
                Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{source}") is null && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "A hidden source introduces no pose or delay to its visible victim's impact.");
            else
                Require(task.IsCompleted && await task && board.GetNodeOrNull("BattleEffects") is null,
                    "An all-hidden scope completes without effects or hidden timing.");
            await Drain(board, task);
        }
        var (manifest, bindings) = ReleaseFixture();
        bindings.WeaponActions.Remove(ReleaseCastWeapon);
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        var targetOnly = new HashSet<int> { target };
        var playing = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), events, targetOnly), targetOnly);
        Require(!playing.IsFaulted && board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "A hidden source never resolves its unavailable weapon action policy.");
        await Drain(board, playing);
        ConfigureActorClipBoard(board, catalog);
    }

    private async Task CheckActivationLifetime(BoardView board, AssetCatalog catalog, JsonObject state, int source, int target)
    {
        var transition = Transition(state, state, IdleAction(), ActivationScope(source,
            ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon)));
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var time in new[] { .016, .064, .300 })
            {
                completed.Clear();
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, time);
                var published = completed.ToArray();
                board.CancelPresentation();
                Require(!await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation before and after release retires both action and impact ownership.");
                await Tick(board, 1);
                Require(completed.SequenceEqual(published) && Frame(Actor(board, source)) == 0,
                    "Cancellation cannot retract released events or publish later events or revive a source pose.");
            }
            completed.Clear();
            var old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            ConfigureActorClipBoard(board, catalog);
            board.RenderState(state);
            await Tick(board, 1);
            Require(!await old && completed.SequenceEqual(new[] { 0 }) && !board.IsPresenting
                && Frame(Actor(board, source)) == 0 && board.GetNodeOrNull("BattleEffects") is null,
                "Rebinding a released activation cancels recovery and impact without later event publication.");
            completed.Clear();
            old = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .064);
            RemoveChild(board);
            Require(!await old && !board.IsPresenting, "Tree exit cancels the active explicit scope.");
            AddChild(board);
            ConfigureActorClipBoard(board, catalog);
            board.RenderState(state);
            await Tick(board, 1);
            Require(completed.SequenceEqual(new[] { 0 }) && board.GetNodeOrNull("BattleEffects") is null,
                "Reattachment cannot revive a retired activation.");
            completed.Clear();
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && completed.SequenceEqual(new[] { 0, 1, 2 })
                && Frame(Actor(board, source)) == 0 && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion settles the explicit bracket synchronously with each event published once.");
        }
        finally { board.AnimationsEnabled = true; board.EventPresented -= Presented; }
    }

    private async Task CheckActivationFailures(BoardView board, JsonObject state, int source, int target)
    {
        foreach (var events in new[]
        {
            new[] { ActivationCompleted(source) },
            new[] { Activation(source) },
            new[] { Activation(source), Activation(source), ActivationCompleted(source) },
            new[] { Activation(source), ActivationCompleted(target) },
            new[] { Activation(source), ActivationCompleted(source, ReleaseLungeWeapon) }
        })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
            for (var tick = 0; !task.IsCompleted && tick < 5; tick++)
                await Tick(board, 1);
            Require(task.IsCompleted, "Malformed activation brackets must not leave an unresolved presentation gate.");
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                "Missing, nested or mismatched activation brackets fail explicitly and release presentation ownership.");
        }
    }

    private async Task CheckActivationUnrepresentableImpact(JsonObject state, int source, int target)
    {
        var (manifest, bindings) = ReleaseFixture();
        manifest.ActorClips[ReleaseCastAction] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            ReleaseMs = 1e20,
            Frames = new[] { new ActorClipFrameRecord { Frame = "attack", Span = 2e20 } }
        };
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                ActivationScope(source, ReleaseStrike("attack", source, target, ReleaseCastWeapon))));
            await Tick(board, 1e17);
            Require(task.IsCompleted, "An unrepresentable impact interval must not hang after a far-future release.");
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                && Frame(Actor(board, source)) == 0,
                "A positive impact that cannot advance the timeline fails explicitly and settles all actor ownership.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckActivationLongRecovery(JsonObject state, int source, int target)
    {
        var (manifest, bindings) = ReleaseFixture();
        manifest.ActorClips[ReleaseCastAction] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            ReleaseMs = 32,
            Frames = new[]
            {
                new ActorClipFrameRecord { Frame = "walk-b", Span = 32 },
                new ActorClipFrameRecord { Frame = "attack", Span = 64 },
                new ActorClipFrameRecord { Frame = "walk-a", Span = 544 }
            }
        };
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            var events = ActivationScope(source, ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon))
                .Append(ReleaseStrike("counter", target, source, ReleaseCastWeapon)).ToArray();
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
            await Tick(board, .500);
            Require(completed.SequenceEqual(new[] { 0, 1 }) && Frame(Actor(board, source)) == 1
                && Frame(Actor(board, target)) == 0 && board.GetNodeOrNull("BattleEffects/Hit") is null && !task.IsCompleted,
                "Long recovery overlaps and outlasts impact; completion holds the following counter back.");
            await Tick(board, .1401);
            Require(completed.SequenceEqual(new[] { 0, 1, 2 }) && Frame(Actor(board, source)) == 0
                && Frame(Actor(board, target)) == 3,
                "Counter begins only after the original source recovers, not after the last victim finishes.");
            await Drain(board, task);
            Require(completed.SequenceEqual(new[] { 0, 1, 2, 3 }), "The long-recovery bracket and counter publish once in order.");

            completed.Clear();
            var after = (JsonObject)state.DeepClone();
            var dead = after["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == source);
            after["units"]!.AsArray().Remove(dead);
            events = ActivationScope(source, ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon))
                .Append(new JsonObject { ["type"] = "death", ["unit"] = source, ["at"] = At(12, 16) }).ToArray();
            task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            await Tick(board, .500);
            Require(Frame(Actor(board, source)) == 1 && Actor(board, source).SelfModulate == Colors.White
                && completed.SequenceEqual(new[] { 0, 1 }),
                "A source absent from final state remains alive in presentation until its recovery finishes.");
            await Tick(board, .250);
            Require(Mathf.IsEqualApprox(Actor(board, source).SelfModulate.A, .5f)
                && completed.SequenceEqual(new[] { 0, 1, 2 }),
                "Coarse time crossing completion advances the later death by only its true 110ms overshoot.");
            await Drain(board, task);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{source}") is null
                && completed.SequenceEqual(new[] { 0, 1, 2, 3 }), "Source death settles only after recovery and publishes once.");


            completed.Clear();
            events = ActivationScope(source,
                ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon),
                new JsonObject { ["type"] = "death", ["unit"] = source, ["at"] = At(12, 16) });
            task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            await Tick(board, .500);
            Require(Frame(Actor(board, source)) == 1 && Actor(board, source).SelfModulate == Colors.White
                && completed.SequenceEqual(new[] { 0, 1 }) && !task.IsCompleted,
                "Source death inside an open scope waits for the existing recovery before touching its actor.");
            await Tick(board, .139);
            Require(Actor(board, source).SelfModulate == Colors.White && completed.SequenceEqual(new[] { 0, 1 }),
                "The inside-scope death cannot fade or retire the source even just before recovery ends.");
            await Tick(board, .111);
            Require(Frame(Actor(board, source)) == 0 && Mathf.IsEqualApprox(Actor(board, source).SelfModulate.A, .5f)
                && completed.SequenceEqual(new[] { 0, 1 }) && !task.IsCompleted,
                "Inside-scope death starts at recovery completion and receives exactly its 110ms coarse-tick overshoot.");
            await Tick(board, .111);
            Require(task.IsCompleted && await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{source}") is null
                && completed.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "Death retires the recovered source before closing the bracket without accessing a freed actor.");
            await Tick(board, 1);
            Require(completed.SequenceEqual(new[] { 0, 1, 2, 3 }) && board.GetNodeOrNull("BattleEffects") is null,
                "Retired source recovery cannot resurrect a pose or publish again after inside-scope death.");

            completed.Clear();
            after = (JsonObject)state.DeepClone();
            dead = after["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == target);
            after["units"]!.AsArray().Remove(dead);
            events = ActivationScope(source,
                ReleaseStrike("areaAttack", source, target, ReleaseCastWeapon),
                new JsonObject { ["type"] = "death", ["unit"] = target, ["at"] = At(13, 16) });
            task = board.PresentTransitionAsync(viewportFeedback, Transition(state, after, IdleAction(), events));
            await Tick(board, .500);
            Require(Frame(Actor(board, source)) == 1 && Actor(board, source).SelfModulate == Colors.White
                && Mathf.IsEqualApprox(Actor(board, target).SelfModulate.A, (float)(1 - 48.0 / 220))
                && completed.SequenceEqual(new[] { 0, 1 }),
                "A different victim's death begins immediately after its impact and does not wait for source recovery.");
            await Drain(board, task);
            Require(completed.SequenceEqual(new[] { 0, 1, 2, 3 }),
                "Independent victim death and source recovery still close the explicit bracket once in order.");
        }
        finally { board.EventPresented -= Presented; RemoveChild(board); board.Free(); }
    }

    private async Task CheckActivationLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonObject>("res://tests/native/c01-09-structure-collateral.json");
        var levelId = witness["levelId"]!.GetValue<string>();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        foreach (var order in witness["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(order);
        var action = witness["action"]!.AsObject();
        var receipt = battle.Dispatch(action);
        var kinds = receipt.Events.Select(value => value["type"]!.GetValue<string>()).ToArray();
        var activated = Array.IndexOf(kinds, "attackActivated");
        var completedIndex = Array.IndexOf(kinds, "attackCompleted");
        Require(activated >= 0 && completedIndex > activated && kinds.Count(value => value == "attackActivated") == 1
            && kinds.Count(value => value == "attackCompleted") == 1
            && Array.IndexOf(kinds, "areaAttack") > activated
            && Array.IndexOf(kinds, "attackStructure") > Array.IndexOf(kinds, "areaAttack")
            && Array.IndexOf(kinds, "attackStructure") < completedIndex,
            "The lawful C01-09 scope contains unit collateral before its primary structure impact.");
        var after = battle.Snapshot();
        var stateHash = battle.StateHash;
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
            var published = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => published.Add(index);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, (action["path"]!.AsArray().Count - 1) * .160);
                Require(Frame(Actor(board, Int(action, "unit"))) == 2 && board.GetNodeOrNull("BattleEffects/Hit") is null
                    && !published.Contains(activated),
                    "The real moving engineer starts one source action before the first collateral impact.");
                await Tick(board, .074);
                Require(board.GetNodeOrNull("BattleEffects/Hit") is null && !published.Contains(activated),
                    "Real structure-primary collateral cannot hit before the source's authored release.");
                await Tick(board, .002);
                Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && published.Contains(activated)
                    && !published.Contains(Array.IndexOf(kinds, "areaAttack")),
                    "The real unit collateral starts at release, before the later primary structure impact.");
                await Drain(board, task);
                Require(published.SequenceEqual(Enumerable.Range(0, receipt.Events.Count)) && battle.StateHash == stateHash
                    && StateDigest.Hash(after) == stateHash && original == transition.Before.GetRawText() + transition.After.GetRawText()
                        + transition.Action.GetRawText() + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "C01-09 presentation publishes the new receipt once in order without mutating canonical state or event bytes.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckActivationDuplicateReceipt(engine, recordedLevel, receipt.Before, action);
    }

    private async Task CheckActivationDuplicateReceipt(BattleEngine engine, JsonObject recordedLevel, JsonObject before, JsonObject action)
    {
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(before));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                recordedLevel, AuthoredScene.Load("res://data/scenes/c01-09.json"));
            var published = new List<int>();
            screen.Board.EventPresented += (_, index) => published.Add(index);
            Require(session.Dispatch(EcmaJson.ToElement(action), session.Revision).Accepted,
                "The real session accepts the lawful C01-09 collateral attack.");
            screen.Refresh();
            screen.Refresh();
            var transition = session.GetSnapshot().Transition!;
            var stateHash = StateDigest.Hash(session.Snapshot());
            for (var tick = 0; screen.IsPresenting && tick < 40; tick++)
                await Tick(screen.Board, 1);
            Require(!screen.IsPresenting && published.SequenceEqual(Enumerable.Range(0, transition.Events.Count)),
                "Refreshing an active receipt cannot duplicate its activation or event publications.");
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            await Tick(screen.Board, 1);
            Require(!screen.IsPresenting && published.Count == transition.Events.Count
                && screen.RecentMessages.SequenceEqual(messages) && StateDigest.Hash(session.Snapshot()) == stateHash,
                "The existing screen receipt contract prevents a settled activation from replaying on refresh.");
        }
        finally { RemoveChild(screen); screen.Free(); }
    }
}
