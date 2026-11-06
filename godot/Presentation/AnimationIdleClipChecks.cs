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
    private const string IdleFixtureId = "fixture.idle-three-poses";

    private static (AssetManifest Manifest, ArtBindings Bindings) IdleClipFixture()
    {
        var (manifest, bindings) = ActorClipFixture();
        // Synthetic timing infrastructure over existing trusted pixels, not authored idle animation.
        manifest.ActorClips[IdleFixtureId] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            Frames = new[]
            {
                new ActorClipFrameRecord { Frame = "walk-a", Span = 40 },
                new ActorClipFrameRecord { Frame = "attack", Span = 60 },
                new ActorClipFrameRecord { Frame = "walk-b", Span = 100 }
            }
        };
        bindings.Units["c01.swordsman"].ActionClips["idle"] = IdleFixtureId;
        return (manifest, bindings);
    }

    private JsonObject IdleClipState()
    {
        var state = Copy();
        foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>().Take(2))
        {
            unit["type"] = "c01.swordsman";
            unit.Remove("key");
        }
        return state;
    }

    private async Task CheckIdleClipPlayback()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            var clip = assets.ResolveUnitClip(type, "idle", ActorClipUnit.Milliseconds, key);
            var asset = assets.ResolveUnit(type, key);
            Require(clip.Asset == asset && clip.FrameAt(0) == AssetCatalog.FrameIndex(asset, "idle")
                && clip.FrameAt(clip.Length - .001) == clip.FrameAt(0),
                "Shipped idle declarations preserve the selected existing static idle picture.");
        }
        var (manifest, bindings) = IdleClipFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        var state = IdleClipState();
        var ids = state["units"]!.AsArray().OfType<JsonObject>().Take(2).Select(unit => Int(unit, "id")).ToArray();
        var completed = 0;
        void Presented(BattlePresentationTransition _, int index) => completed++;
        board.EventPresented += Presented;
        try
        {
            board.RenderState(state);
            var actor = Actor(board, ids[0]);
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var clip = catalog.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds);
            CheckClipSprite(sprite, clip.Asset, 1, sprite.Scale.X < 0 ? -1 : 1);
            CheckClipShadow(sprite);
            var texture = sprite.Texture!.GetInstanceId();
            var cues = board.GetNode<Node2D>("CanonicalProjection/OccludedUnitCues").GetInstanceId();
            await Tick(board, .039);
            Require(Frame(actor) == 1 && sprite.Texture!.GetInstanceId() == texture
                && board.GetNode<Node2D>("CanonicalProjection/OccludedUnitCues").GetInstanceId() == cues,
                "An unchanged idle pose neither replaces its texture nor rebuilds occlusion cues.");
            await Tick(board, .002);
            Require(Frame(actor) == 2, "Idle sampling crosses the first half-open frame boundary.");
            CheckClipShadow(sprite);
            await Tick(board, .060);
            Require(Frame(actor) == 3, "Idle sampling retains overshoot when crossing a second frame boundary.");
            await Tick(board, .100);
            Require(Frame(actor) == 1, "Idle sampling wraps at the sum of declared frame durations.");
            await Tick(board, 20.05);
            Require(Frame(actor) == 2 && !board.IsPresenting && completed == 0
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Large idle advances sample modulo duration without taking the finite receipt gate or publishing events.");

            board.Hide();
            await Tick(board, 10.08);
            board.Show();
            await Tick(board, .001);
            Require(Frame(actor) == 2, "A hidden board freezes idle phase without catching up when shown.");
            var host = new Node2D();
            AddChild(host);
            board.Reparent(host);
            // Reparenting retires the previous registration; render explicitly before testing ancestor visibility.
            board.RenderState(state);
            await Tick(board, .05);
            host.Hide();
            await Tick(board, 10.08);
            host.Show();
            await Tick(board, .001);
            Require(Frame(Actor(board, ids[0])) == 2, "A hidden ancestor freezes idle just like the board itself.");
            board.Reparent(this);
            RemoveChild(host);
            host.Free();

            board.RenderState(state);
            await Tick(board, .11);
            board.AnimationsEnabled = false;
            Require(Frame(Actor(board, ids[0])) == 1, "Reduced motion immediately restores the declared first idle pose.");
            await Tick(board, 20.05);
            Require(Frame(Actor(board, ids[0])) == 1, "Reduced motion does not advance idle phases.");
            board.AnimationsEnabled = true;
            await Tick(board, .039);
            Require(Frame(Actor(board, ids[0])) == 1, "Re-enabled idle starts a new phase at zero.");
            await Tick(board, .002);
            Require(Frame(Actor(board, ids[0])) == 2, "Re-enabled idle uses its declared timing again.");

            board.RenderState(state, new HashSet<int> { ids[0] });
            await Tick(board, .11);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[1]}") is null && board.RenderedUnitCount == 1,
                "Fog-filtered actors cannot be resurrected by persistent idle registration.");
            board.RenderState(state);
            Require(Frame(Actor(board, ids[0])) == 1 && Frame(Actor(board, ids[1])) == 1,
                "A fresh canonical render atomically replaces actors and resets their phases.");
            await CheckIdleFiniteOwnership(board, catalog, state, ids[0], ids[1]);
            await CheckIdleSpawnAndDeath(board, state, ids[0], ids[1]);
            await CheckIdleRetirement(board, catalog, state, ids[0], ids[1]);
        }
        finally
        {
            board.EventPresented -= Presented;
            RemoveChild(board);
            board.Free();
        }
        await CheckIdleLegalReceipt();
        await CheckIdleDepthRetention();
        await CheckIdleLargeTick();
    }

    private async Task CheckIdleLargeTick()
    {
        var (manifest, bindings) = IdleClipFixture();
        manifest.ActorClips[IdleFixtureId] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            Frames = [new() { Frame = "walk-a", Span = 8e307 }, new() { Frame = "walk-b", Span = 8e307 }]
        };
        var board = NewActorClipBoard(new AssetCatalog(manifest, bindings));
        try
        {
            var state = IdleClipState();
            var id = Int(state["units"]![0]!.AsObject(), "id");
            board.RenderState(state);
            await Tick(board, 1e305);
            Require(Frame(Actor(board, id)) == 3, "A large finite tick enters the second admitted idle frame.");
            await Tick(board, 1e305);
            Require(Frame(Actor(board, id)) == 1 && !board.IsPresenting,
                "Loop phase wraps without overflowing even when an unbounded addition would exceed a finite double.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckIdleFiniteOwnership(BoardView board, AssetCatalog catalog, JsonObject state, int aid, int did)
    {
        var transition = Transition(state, (JsonObject)state.DeepClone(), IdleAction(), [Strike("attack", aid, did)]);
        var task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .041);
        var attacker = Actor(board, aid);
        var sprite = attacker.GetNode<Sprite2D>("AuthoredSprite");
        Require(Frame(attacker) == 2 && Frame(Actor(board, did)) == 2,
            "A finite attack and another actor's idle advance independently on the board clock.");
        board.AnimationsEnabled = false;
        Require(Frame(attacker) == 2 && Frame(Actor(board, did)) == 1,
            "Changing reduced motion resets eligible idlers without changing an existing finite receipt's pose.");
        board.AnimationsEnabled = true;
        await Tick(board, .060);
        Require(Frame(attacker) == 3 && Frame(Actor(board, did)) == 2,
            "Only the acting unit is excluded from idle sampling while its finite clip owns it.");
        await Tick(board, .028);
        var idle = catalog.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds);
        CheckClipSprite(sprite, idle.Asset, idle.FrameAt(0), sprite.Scale.X < 0 ? -1 : 1);
        CheckClipShadow(sprite);
        Require(board.GetNodeOrNull("BattleEffects/Hit") is not null && !task.IsCompleted,
            "A surviving attacker returns to declared idle frame zero before impact playback completes.");
        board.Hide();
        await Tick(board, .421);
        board.Show();
        Require(await task && !board.IsPresenting, "Hiding the board freezes idle, not the finite receipt clock.");

        task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .05);
        board.CancelPresentation();
        Require(!await task && Frame(Actor(board, aid)) == 1 && Frame(Actor(board, did)) == 1,
            "Cancellation settles committed actors and replaces partial phases with first idle poses.");
        await Tick(board, .11);
        Require(Frame(Actor(board, aid)) == 3, "Settled idle remains active after finite cancellation.");

        Task<bool>? replacement = null;
        void Replace(BattlePresentationTransition receipt, int index)
        {
            if (!ReferenceEquals(receipt, transition))
                return;
            board.CancelPresentation();
            replacement = board.PresentTransitionAsync(viewportFeedback,
                Transition(state, (JsonObject)state.DeepClone(), IdleAction(), [Strike("attack", did, aid)]));
        }
        board.EventPresented += Replace;
        try
        {
            task = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .129);
            await Tick(board, .421);
            Require(replacement is not null && board.IsPresenting,
                "An event callback can replace playback without the retired sequence clearing new idle ownership.");
            await task;
            await Tick(board, .05);
            Require(Frame(Actor(board, aid)) == 2 && Frame(Actor(board, did)) == 2,
                "The replacement receipt owns only its attacker while the former attacker idles.");
            await Drain(board, replacement!);
        }
        finally { board.EventPresented -= Replace; }
    }

    private async Task CheckIdleSpawnAndDeath(BoardView board, JsonObject state, int aid, int did)
    {
        var afterDeath = (JsonObject)state.DeepClone();
        var dying = afterDeath["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == aid);
        afterDeath["units"]!.AsArray().Remove(dying);
        var death = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, afterDeath, IdleAction(), [new JsonObject { ["type"] = "death", ["unit"] = aid, ["at"] = At(Int(dying, "x"), Int(dying, "y")) }]));
        await Tick(board, .05);
        Require(Frame(Actor(board, aid)) == 0 && Actor(board, aid).SelfModulate.A < 1 && Frame(Actor(board, did)) == 2,
            "Death owns the canonical base pose and fade while an unrelated actor continues its idle clip.");
        await Drain(board, death);
        await Tick(board, .05);
        Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null,
            "Completed death never leaves an idle registration that can resurrect the retired actor.");

        var afterSpawn = (JsonObject)state.DeepClone();
        var recruit = (JsonObject)state["units"]![0]!.DeepClone();
        recruit["id"] = 1000;
        recruit["x"] = 1;
        recruit["y"] = 1;
        afterSpawn["units"]!.AsArray().Add(recruit);
        var spawn = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, afterSpawn, IdleAction(), [new JsonObject { ["type"] = "recruit", ["unit"] = 1000, ["at"] = At(1, 1) }]));
        await Tick(board, .05);
        var spawnedSprite = Actor(board, 1000).GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(spawnedSprite, assets.ResolveUnit("c01.swordsman"), 0, spawnedSprite.Scale.X < 0 ? -1 : 1);
        Require(Actor(board, 1000).SelfModulate.A < 1 && Frame(Actor(board, did)) == 2,
            "Spawn owns the recruit's canonical base pose and alpha while existing actors keep idling.");
        await Tick(board, .191);
        Require(await spawn && Frame(Actor(board, 1000)) == 1 && Actor(board, 1000).Scale == Vector2.One,
            "A surviving recruit joins persistent idle on its declared first pose after finite spawn completion.");
    }

    private async Task CheckIdleRetirement(BoardView board, AssetCatalog catalog, JsonObject state, int aid, int did)
    {
        var transition = Transition(state, (JsonObject)state.DeepClone(), IdleAction(), [Strike("attack", aid, did)]);
        var task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .05);
        ConfigureActorClipBoard(board, assets);
        await Tick(board, .11);
        Require(!await task && !board.IsPresenting, "Successful rebind cancels finite playback and retires old idle registrations.");
        board.RenderState(state);
        await Tick(board, .11);
        Require(Frame(Actor(board, aid)) == 0, "A rebound static idle cannot inherit the old three-pose sequence.");
        ConfigureActorClipBoard(board, catalog);
        board.RenderState(state);
        await Tick(board, .11);
        RemoveChild(board);
        AddChild(board);
        var sprite = Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite");
        var retiredTexture = sprite.Texture!.GetInstanceId();
        await Tick(board, .14);
        Require(Frame(Actor(board, aid)) == 3 && sprite.Texture!.GetInstanceId() == retiredTexture,
            "Reparenting alone cannot resume idle registrations retired by tree exit.");
        board.RenderState(state);
        await Tick(board, .05);
        Require(Frame(Actor(board, aid)) == 2, "A fresh render after reparent explicitly registers new idle playback.");
        task = board.PresentTransitionAsync(viewportFeedback, transition);
        await Tick(board, .05);
        RemoveChild(board);
        Require(!await task, "Tree exit cancels an owned finite pose synchronously.");
        AddChild(board);
        sprite = Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite");
        retiredTexture = sprite.Texture!.GetInstanceId();
        await Tick(board, .11);
        Require(Frame(Actor(board, aid)) == 2 && sprite.Texture!.GetInstanceId() == retiredTexture,
            "A reattached partial finite pose cannot be animated by stale idle registrations.");
        board.RenderState(state);
        Require(Frame(Actor(board, aid)) == 1, "Explicit rendering replaces a retired partial pose with declared idle frame zero.");
    }

    private async Task CheckIdleLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == witness["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(),
            "Idle integration begins with the recorded legal C06 campaign state.");
        var orders = witness["actions"]!.AsArray();
        battle.Dispatch(orders[0]!.AsObject());
        battle.Dispatch(orders[1]!.AsObject());
        var action = orders[2]!.AsObject();
        var receipt = battle.Dispatch(action);
        var attack = receipt.Events.First(value => value["type"]!.GetValue<string>() == "attack");
        var aid = Int(attack, "attacker");
        var units = receipt.Before["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var bystander = units.First(unit => Int(unit, "id") != aid && Int(unit, "id") != Int(attack, "defender"));
        var bid = Int(bystander, "id");
        var (manifest, bindings) = IdleClipFixture();
        manifest.ActorClips["fixture.legal-idle"] = new()
        {
            Unit = "milliseconds",
            Frames = manifest.ActorClips[IdleFixtureId].Frames
        };
        manifest.ActorClips["fixture.legal-strike"] = new()
        {
            Unit = "milliseconds",
            Frames = ClipFixtureFrames()
        };
        foreach (var unit in new[] { units.Single(unit => Int(unit, "id") == aid), bystander })
        {
            var type = unit["type"]!.GetValue<string>();
            var key = unit["key"]?.GetValue<string>();
            var binding = key is not null && bindings.Actors.TryGetValue(key, out var named) ? named.UnitArt : bindings.Units[type];
            binding.ActionClips["idle"] = "fixture.legal-idle";
            binding.ActionClips["attack"] = "fixture.legal-strike";
        }
        var catalog = new AssetCatalog(manifest, bindings);
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(catalog, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric),
                recordedLevel, AuthoredScene.Load($"res://data/scenes/{witness["levelId"]!.GetValue<string>()}.json"));
            board.ConfigureDynamicState();
            var after = battle.Snapshot();
            var hash = battle.StateHash;
            var transition = Transition(receipt.Before, after, action, receipt.Events);
            var source = transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
                + string.Concat(transition.Events.Select(value => value.GetRawText()));
            var completed = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .05);
                Require(Frame(Actor(board, aid)) == 1 && Frame(Actor(board, bid)) == 2,
                    "A real accepted path owns its walking actor while an independent actor keeps idling.");
                await Tick(board, .06);
                Require(Frame(Actor(board, aid)) == 1 && Frame(Actor(board, bid)) == 3,
                    "Idle pose changes cannot overwrite the accepted path's current walk pose.");
                var movementMs = (action["path"]!.AsArray().Count - 1) * 160;
                await Tick(board, (movementMs - 110 + 1) / 1000.0);
                Require(Frame(Actor(board, aid)) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The accepted movement hands ownership directly to its declared finite attack clip.");
                await Tick(board, .033);
                Require(Frame(Actor(board, aid)) == 2, "Persistent idle cannot overwrite the legal receipt's second strike pose.");
                await Tick(board, .096);
                Require(Frame(Actor(board, aid)) == 1
                    && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-" + attack["damage"]!.ToJsonString(),
                    "The accepted strike returns to the declared noncanonical first idle pose before its unchanged damage impact.");
                await Drain(board, task);
                Require(completed.SequenceEqual(Enumerable.Range(0, receipt.Events.Count)) && battle.StateHash == hash
                    && StateDigest.Hash(after) == hash && source == transition.Before.GetRawText() + transition.After.GetRawText()
                        + transition.Action.GetRawText() + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "Persistent idle leaves the actual C06 receipt, canonical state, event order and completion unchanged.");
                await Tick(board, .05);
                Require(!board.IsPresenting && completed.Count == receipt.Events.Count,
                    "Post-receipt idle never reopens input gating or republishes an event.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckIdleDepthRetention()
    {
        var fixtureLevel = MaterialSceneFixtureChecks.Level;
        fixtureLevel["id"] = "idle-depth";
        fixtureLevel["terrain"] = new JsonArray("........", "........", "........", "........", "........");
        fixtureLevel["elevation"] = new JsonArray(Enumerable.Range(0, 40).Select(_ => (JsonNode)JsonValue.Create(0)!).ToArray());
        fixtureLevel["cliffs"] = new JsonArray();
        var scene = new AuthoredScene { LevelId = "idle-depth", MapSize = new[] { 8, 5 }, RuntimeReady = true, FramePlacements = new() };
        var engine = new BattleEngine(packs);
        var before = engine.CreateState(fixtureLevel);
        var units = before["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var aid = Int(units[0], "id");
        var did = Int(units[1], "id");
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        void ConfigureDepth(AssetCatalog catalog)
        {
            board.Configure(new(catalog, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric), fixtureLevel, scene);
            board.ConfigureDynamicState();
        }
        try
        {
            ConfigureDepth(assets);
            board.RenderState(before);
            var bystander = Actor(board, did);
            var cut = (int)Math.Floor(board.Projection.Depth(bystander.GetMeta("logical_contact").AsVector2())) + 2;
            Require(board.Projection.Faces.Count == 0 && board.RenderedCoverPropCount == 0
                && cut > board.Projection.DepthPartitions.Min(partition => partition.StartDepth)
                && cut < board.Projection.DepthPartitions.Max(partition => partition.EndDepthExclusive)
                && !board.Projection.DepthPartitions.Any(partition => partition.StartDepth == cut || partition.EndDepthExclusive == cut),
                "The idle depth witness has a missing interior cut on flat terrain, not an existing cliff boundary.");
            var (manifest, bindings) = IdleClipFixture();
            var trusted = manifest.Assets.Single(record => record.Id == "torren-stage01");
            // Contact geometry over an existing trusted sheet, not a new visual asset.
            manifest.Assets.Add(new AssetRecord
            {
                Id = "fixture.idle-offset-contact",
                TopicId = trusted.TopicId,
                ResourcePath = trusted.ResourcePath,
                Width = trusted.PhysicalWidth,
                Height = trusted.PhysicalHeight,
                Frames = 1,
                FrameOrder = new[] { "idle" },
                PhysicalWidth = trusted.PhysicalWidth,
                PhysicalHeight = trusted.PhysicalHeight,
                PixelDensity = 1,
                Anchor = new[] { trusted.PhysicalWidth / 2.0, 100.0 },
                GroundFront = new[] { trusted.PhysicalWidth / 2.0, 116.0 },
                PresentationScale = 1.0 / 16
            });
            manifest.ActorClips[IdleFixtureId] = new()
            {
                Unit = "milliseconds",
                AppearanceId = "fixture.idle-offset-contact",
                Frames = new[] { new ActorClipFrameRecord { Frame = "idle", Span = 200 } }
            };
            manifest.Assets.Add(new AssetRecord
            {
                Id = "fixture.strike-offset-contact",
                TopicId = trusted.TopicId,
                ResourcePath = trusted.ResourcePath,
                Width = trusted.PhysicalWidth,
                Height = trusted.PhysicalHeight,
                Frames = 1,
                FrameOrder = new[] { "attack" },
                PhysicalWidth = trusted.PhysicalWidth,
                PhysicalHeight = trusted.PhysicalHeight,
                PixelDensity = 1,
                Anchor = new[] { trusted.PhysicalWidth / 2.0, 100.0 },
                GroundFront = new[] { trusted.PhysicalWidth / 2.0, 132.0 },
                PresentationScale = 1.0 / 16
            });
            manifest.ActorClips[ClipFixtureId] = new()
            {
                Unit = "milliseconds",
                AppearanceId = "fixture.strike-offset-contact",
                Frames = new[] { new ActorClipFrameRecord { Frame = "attack", Span = 128 } }
            };
            ConfigureDepth(new AssetCatalog(manifest, bindings));
            board.RenderState(before);
            void CheckRetained()
            {
                Require(SpriteGroundContact.Offset(Actor(board, did).GetNode<Sprite2D>("AuthoredSprite")).Y == 1
                    && board.Projection.DepthPartitions.Any(partition => partition.StartDepth == cut || partition.EndDepthExclusive == cut),
                    "Another actor's finite preparation retains the independent idle actor's authored ground-front cut.");
                CheckActorDepthCuts(board);
            }
            CheckRetained();
            var attack = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, (JsonObject)before.DeepClone(), IdleAction(), [Strike("attack", aid, did)]));
            CheckRetained();
            await Tick(board, .05);
            CheckRetained();
            await Drain(board, attack);
            var afterMove = (JsonObject)before.DeepClone();
            afterMove["units"]![0]!["x"] = 2;
            var command = new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = aid,
                ["path"] = new JsonArray(At(1, 3), At(2, 3)),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            };
            var move = board.PresentTransitionAsync(viewportFeedback, Transition(before, afterMove, command, Array.Empty<JsonObject>()));
            CheckRetained();
            await Tick(board, .08);
            CheckRetained();
            await Drain(board, move);
            var recruited = (JsonObject)before.DeepClone();
            var recruit = (JsonObject)units[0].DeepClone();
            recruit["id"] = 1000;
            recruit["x"] = 3;
            recruit["y"] = 2;
            recruited["units"]!.AsArray().Add(recruit);
            var spawn = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, recruited, IdleAction(), [new JsonObject { ["type"] = "recruit", ["unit"] = 1000, ["at"] = At(3, 2) }]));
            CheckRetained();
            await Tick(board, .12);
            CheckRetained();
            await Drain(board, spawn);
            CheckRetained();

            // Synthetic ownership witness: one actor attacks and then dies without an intervening canonical render.
            var afterDeath = (JsonObject)before.DeepClone();
            afterDeath["units"]!.AsArray().RemoveAt(0);
            var attackThenDeath = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, afterDeath, IdleAction(),
                    [Strike("attack", aid, did), new JsonObject { ["type"] = "death", ["unit"] = aid, ["at"] = At(1, 3) }]));
            var dyingActor = Actor(board, aid);
            var baseDepth = board.Projection.Depth(dyingActor.GetMeta("logical_contact").AsVector2());
            var baseCut = (int)Math.Floor(baseDepth) + 1;
            Require(SpriteGroundContact.Offset(dyingActor.GetNode<Sprite2D>("AuthoredSprite")).Y == 2,
                "The resumed-cache witness begins on a finite sheet with a different front from both base and idle.");
            await Tick(board, .129);
            Require(SpriteGroundContact.Offset(dyingActor.GetNode<Sprite2D>("AuthoredSprite")).Y == 1
                && board.Projection.DepthPartitions.Any(partition => partition.StartDepth == baseCut || partition.EndDepthExclusive == baseCut),
                "Resumed alternate idle retains the canonical base depth, not just its own and the previous strike's depths.");
            var expectedBaseZ = board.Projection.LayerAtDepth(baseDepth, BoardDepthLayer.Standing);
            Require(dyingActor.ZIndex != expectedBaseZ,
                "The alternate idle must have a genuinely different draw layer from its canonical base pose.");
            await Tick(board, .421);
            Require(!attackThenDeath.IsCompleted
                && SpriteGroundContact.Offset(dyingActor.GetNode<Sprite2D>("AuthoredSprite")).IsEqualApprox(Vector2.Zero)
                && dyingActor.ZIndex == expectedBaseZ,
                "Starting death immediately sorts the restored canonical base pose without waiting for another idle frame or tween tick.");
            CheckRetained();
            await Drain(board, attackThenDeath);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null,
                "The same-receipt attack-to-death witness retires its actor after the finite fade.");
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
