#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const string ClipFixtureId = "fixture.three-poses";

    private void CheckActorClipDefinitions()
    {
        var shippedBindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var type in shippedBindings.Units.Keys)
            CheckStaticActorClip(type, null);
        foreach (var (key, actor) in shippedBindings.Actors)
            CheckStaticActorClip(actor.ExpectedUnitType, key);

        var (manifest, bindings) = ActorClipFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var clip = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds);
        var selected = catalog.ResolveUnit("c01.swordsman");
        Require(clip.Asset.Identity == "torren-stage01" && selected.Identity != clip.Asset.Identity
            && clip.Asset.TopicId == selected.TopicId && clip.Length == 128,
            "An explicit alternate clip sheet preserves the selected actor's canonical topic and owns the sum of its durations.");
        foreach (var (time, frame) in new (double, int)[]
        {
            (0, 1), (31.999, 1), (32, 2), (95.999, 2), (96, 3), (127.999, 3), (128, 3), (10000, 3)
        })
            Require(clip.FrameAt(time) == frame, "Finite actor clips use half-open frame intervals and hold their last pose after overshoot.");
        foreach (var invalid in new[] { -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            try
            {
                clip.FrameAt(invalid);
            }
            catch (ArgumentOutOfRangeException) { assertions++; continue; }
            throw new InvalidOperationException("Invalid actor clip sample time was accepted.");
        }
        var named = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds, "campaign-torren");
        Require(named.Asset.Identity == "torren-stage01" && named.Length == 150 && named.FrameAt(32) == 2,
            "Actor-key selection owns its action map instead of inheriting the generic unit's alternate clip.");
        Throws(() => catalog.ResolveUnitClip("c01.archer", "attack", ActorClipUnit.Milliseconds, "campaign-torren"),
            "Actor-key clip resolution must enforce the expected canonical unit type.");
        Throws(() => catalog.ResolveUnitClip("c01.swordsman", "undeclared", ActorClipUnit.Milliseconds),
            "Missing action clips fail instead of inventing a static or generic fallback.");

        var originalRegion = clip.Asset.RegionAt(1);
        var originalAnchor = clip.Asset.AnchorAt(1);
        manifest.ActorClips[ClipFixtureId].Frames[0] = new()
        {
            Frame = "idle",
            Span = 999
        };
        var alternate = manifest.Assets.Single(record => record.Id == "torren-stage01");
        alternate.FrameOrder![1] = "changed-after-admission";
        alternate.FrameRegions![1][0] = 0;
        alternate.FrameAnchors![1][0] = 0;
        bindings.Units["c01.swordsman"].ActionClips.Clear();
        bindings.Actors["campaign-torren"].UnitArt.ActionClips.Clear();
        bindings.Units.Clear();
        bindings.Actors.Clear();
        manifest.ActorClips.Clear();
        Require(clip.Length == 128 && clip.FrameAt(0) == 1 && clip.FrameAt(32) == 2
            && clip.Asset.RegionAt(1) == originalRegion && clip.Asset.AnchorAt(1) == originalAnchor
            && clip.Asset.FrameOrder[1] == "walk-a"
            && catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds).Length == 128
            && catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds, "campaign-torren").Length == 150,
            "Admitted clips detach caller-owned frame arrays, geometry, definitions and both unit/actor action maps.");

        void Reject(Action<AssetManifest, ArtBindings> edit, string reason)
        {
            var (invalidManifest, invalidBindings) = ActorClipFixture();
            edit(invalidManifest, invalidBindings);
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings), reason);
        }
        void RejectFrames(ActorClipFrameRecord[] frames, string reason) => Reject((m, _) =>
            m.ActorClips[ClipFixtureId] = new()
            {
                Unit = "milliseconds",
                Frames = frames
            }, reason);
        Reject((m, _) => m.ActorClips.Remove(ClipFixtureId), "Unknown bound clip identities fail during catalog admission.");
        Reject((m, _) => m.ActorClips[ClipFixtureId] = null!, "Null bound clip definitions fail explicitly.");
        RejectFrames(null!, "Null clip frame arrays fail explicitly.");
        RejectFrames(Array.Empty<ActorClipFrameRecord>(), "An empty clip cannot become a zero-duration fallback.");
        RejectFrames(new ActorClipFrameRecord[] { null! }, "Null frame records fail explicitly.");
        RejectFrames(new[] { new ActorClipFrameRecord { Frame = "missing", Span = 1 } }, "Unknown frame roles fail before playback.");
        foreach (var invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            RejectFrames(new[] { new ActorClipFrameRecord { Frame = "attack", Span = invalid } },
                "Actor frame durations must be finite and strictly positive.");
        RejectFrames(new[]
        {
            new ActorClipFrameRecord { Frame = "attack", Span = double.MaxValue },
            new ActorClipFrameRecord { Frame = "idle", Span = double.MaxValue }
        }, "Overflowing total clip durations fail during admission.");
        RejectFrames(new[]
        {
            new ActorClipFrameRecord { Frame = "attack", Span = 1e20 },
            new ActorClipFrameRecord { Frame = "idle", Span = 1 }
        }, "Every frame must advance representable cumulative time.");
        foreach (var appearance in new[] { "absent-appearance", "laiya-stage01" })
            Reject((m, _) => m.ActorClips[ClipFixtureId] = new()
            {
                Unit = "milliseconds",
                AppearanceId = appearance,
                Frames = ClipFixtureFrames()
            },
                "Alternate clip sheets must exist and belong to the bound actor's canonical topic.");
        Reject((m, _) => m.Assets.Single(record => record.Id == "torren-stage01").FrameRegions![1][2] = 100000,
            "An alternate clip cannot bypass physical frame-region bounds.");
        Reject((m, _) => m.Assets.Single(record => record.Id == "torren-stage01").FrameAnchors![1][0] = -1,
            "An alternate clip cannot bypass authored contact validation.");
        Reject((_, b) => b.Units["c01.swordsman"].ActionClips[" attack"] = ClipFixtureId,
            "Whitespace action identities cannot create a second binding spelling.");
        Reject((_, b) => b.Actors["campaign-torren"].UnitArt.ActionClips["attack"] = "missing",
            "Actor overrides receive the same eager clip admission as generic units.");
        Reject((m, b) =>
        {
            m.ActorClips[ClipFixtureId] = new()
            {
                Unit = "milliseconds",
                Frames = ClipFixtureFrames()
            };
            b.Units["c01.swordsman"] = new()
            {
                TopicId = "C01-FX-01",
                ActionClips = new()
                {
                    ["attack"] = ClipFixtureId
                }
            };
        }, "Actor clips cannot use an FX topic.");
        var (idleManifest, idleBindings) = ActorClipFixture();
        idleBindings.Actors["campaign-torren"].UnitArt.ActionClips.Clear();
        var missingOverride = new AssetCatalog(idleManifest, idleBindings);
        Require(missingOverride.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds).Length == 128,
            "The generic action remains available in the missing-override fixture.");
        Throws(() => missingOverride.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds, "campaign-torren"),
            "Named actors without an action cannot fall back to the generic action map.");
        idleBindings.Units["c01.swordsman"].ActionClips.Clear();
        var idleOnly = new AssetCatalog(idleManifest, idleBindings);
        Require(idleOnly.ResolveUnit("c01.swordsman").TopicId == "C01-UNIT-SWORDSMAN",
            "Catalog geometry can be inspected without selecting an actor action.");
        Throws(() => idleOnly.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds), "Idle-only actors cannot silently acquire an attack clip.");
        CheckActorClipFrameApplication(catalog, selected, clip.Asset);
    }

    private void CheckStaticActorClip(string type, string? key)
    {
        var asset = assets.ResolveUnit(type, key);
        var clip = assets.ResolveUnitClip(type, "attack", ActorClipUnit.Milliseconds, key);
        var frame = AssetCatalog.FrameIndex(asset, asset.Category == "mission-unit" ? "carry-or-work" : "attack");
        Require(clip.Asset == asset && clip.Length == 150 && clip.FrameAt(0) == frame && clip.FrameAt(150) == frame,
            "Shipped combat and mission bindings preserve their existing selected one-pose, 150 ms strike.");
    }

    private void CheckActorClipFrameApplication(AssetCatalog catalog, RuntimeAsset original, RuntimeAsset alternate)
    {
        var sprite = catalog.CreateSprite(original);
        try
        {
            sprite.Position = new(13, 29);
            foreach (var facing in new[] { -1, 1 })
            {
                sprite.Scale = new(facing * original.PresentationScale / original.PixelDensity,
                    original.PresentationScale / original.PixelDensity);
                foreach (var asset in new[] { alternate, original })
                    foreach (var frame in Enumerable.Range(0, asset.FrameCount))
                    {
                        catalog.ApplyFrame(sprite, asset, frame);
                        CheckClipSprite(sprite, asset, frame, facing);
                        Require(sprite.Position == new Vector2(13, 29), "Changing a clip sheet never changes the sprite's parent contact.");
                    }
            }
        }
        finally { sprite.Free(); }
    }

    private async Task CheckActorClipPlayback()
    {
        var (manifest, bindings) = ActorClipFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        try
        {
            // Synthetic presentation fixture: these facts exercise playback, not a legal battle order.
            var before = Copy();
            var attacker = before["units"]![0]!.AsObject();
            attacker["type"] = "c01.swordsman";
            attacker.Remove("key");
            attacker["x"] = 3;
            attacker["y"] = 3;
            var defender = before["units"]!.AsArray().Last()!.AsObject();
            defender["x"] = 1;
            defender["y"] = 3;
            var aid = Int(attacker, "id");
            var did = Int(defender, "id");
            var after = (JsonObject)before.DeepClone();
            var transition = Transition(before, after, IdleAction(), [Strike("attack", aid, did), Strike("supportAttack", aid, did)]);
            var unchanged = before.ToJsonString() + after.ToJsonString();
            var completed = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
            board.EventPresented += Presented;
            try
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                var actor = Actor(board, aid);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var origin = actor.Position;
                var logical = actor.GetMeta("logical_contact").AsVector2();
                var destination = board.Projection.CellContact(new(1, 3));
                var direction = (destination - origin).Normalized();
                var facing = direction.X < 0 ? -1 : 1;
                var clip = catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Milliseconds);
                var idle = catalog.ResolveUnit("c01.swordsman");
                CheckClipSprite(sprite, clip.Asset, 1, facing);
                CheckClipShadow(sprite);
                Require(board.GetNodeOrNull("BattleEffects/Hit") is null && completed.Count == 0,
                    "A finite clip starts on its first pose before impact or receipt publication.");
                await Tick(board, .032);
                CheckClipSprite(sprite, clip.Asset, 2, facing);
                CheckClipShadow(sprite);
                await Tick(board, .032);
                Require(actor.Position.IsEqualApprox(origin + direction * 7)
                    && actor.GetMeta("logical_contact").AsVector2() == logical
                    && SpriteGroundContact.Offset(sprite).IsEqualApprox(Vector2.Zero),
                    "The clip's own midpoint drives the original seven-pixel lunge without changing logical contact.");
                await Tick(board, .040);
                CheckClipSprite(sprite, clip.Asset, 3, facing);
                CheckClipShadow(sprite);
                Require(board.GetNodeOrNull("BattleEffects/Hit") is null && !task.IsCompleted,
                    "An advance crossing a frame boundary samples the current pose without skipping to impact.");
                await Tick(board, .025);
                CheckClipSprite(sprite, idle, 0, facing);
                CheckClipShadow(sprite);
                Require(actor.Position == origin && actor.GetMeta("logical_contact").AsVector2() == logical
                    && board.GetNode<Node2D>("BattleEffects/Hit").Position == destination
                    && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-12" && completed.Count == 0,
                    "Clip completion restores the original sheet, geometry, filter and shadow before the unchanged impact.");
                await Tick(board, .421);
                Require(completed.SequenceEqual(new[] { 0 }) && Frame(actor) == 1
                    && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The first impact publishes once before the following event starts its own finite clip.");
                await Drain(board, task);
                Require(completed.SequenceEqual(new[] { 0, 1 }) && !board.IsPresenting
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null
                    && before.ToJsonString() + after.ToJsonString() == unchanged,
                    "Clip playback preserves receipt order and immutable battle facts and releases both effect roots.");
                completed.Clear();
                task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .040);
                Require(Frame(Actor(board, aid)) == 2, "Cancellation interrupts the alternate clip while it is visibly active.");
                board.CancelPresentation();
                Require(!await task && completed.Count == 0 && board.GetNodeOrNull("BattleEffects") is null,
                    "Cancellation does not publish a partly played actor clip.");
                CheckSettledClipActor(board, aid, idle, origin);

                task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .040);
                ConfigureActorClipBoard(board, assets);
                board.RenderState(after);
                await Tick(board, 10);
                Require(!await task && completed.Count == 0 && !board.IsPresenting
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Rebinding during an alternate-sheet pose cancels the old clip and cannot publish stale completion.");
                CheckSettledClipActor(board, aid, assets.ResolveUnit("c01.swordsman"), origin);
                ConfigureActorClipBoard(board, catalog);
                board.AnimationsEnabled = false;
                task = board.PresentTransitionAsync(viewportFeedback, transition);
                Require(task.IsCompleted && await task && completed.SequenceEqual(new[] { 0, 1 })
                    && board.GetNodeOrNull("BattleEffects") is null,
                    "Reduced motion settles alternate-sheet clips synchronously and preserves event order.");
                CheckSettledClipActor(board, aid, idle, origin);
                board.AnimationsEnabled = true;
                completed.Clear();
                var visible = new HashSet<int> { did };
                var hidden = Transition(before, after, IdleAction(), [Strike("attack", aid, did)], visible);
                task = board.PresentTransitionAsync(viewportFeedback, hidden, visible);
                Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
                    && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                    "A hidden attacker never appears or starts its clip; the already-visible recipient retains its impact.");
                await Drain(board, task);
                Require(completed.SequenceEqual(new[] { 0 }), "Skipping a hidden actor clip does not duplicate or drop the event.");
            }
            finally { board.EventPresented -= Presented; }
            await CheckActorSpecificClipPlayback(board, catalog, before, aid, did);
            bindings.Units["c01.swordsman"].ActionClips.Remove("attack");
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
            var failed = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, IdleAction(), [Strike("attack", aid, did)]));
            try
            {
                await failed;
                throw new Exception("A missing actor action was silently accepted by playback.");
            }
            catch (InvalidOperationException)
            {
                Require(!board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Clip resolution errors reach the caller and release playback gates and effects.");
            }
            CheckSettledClipActor(board, aid, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(3, 3)));
            await CheckActorClipDepth(board);
            await CheckEffectScaleOwnership(board, before, aid, did);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckActorClipLegalReceipt();
    }

    private async Task CheckActorClipDepth(BoardView board)
    {
        // A flat, isolated geometry witness has no cliff occlusion cuts that could mask missing clip preparation.
        var fixtureLevel = MaterialSceneFixtureChecks.Level;
        fixtureLevel["id"] = "actor-clip-depth";
        fixtureLevel["terrain"] = new JsonArray("........", "........", "........", "........", "........");
        fixtureLevel["elevation"] = new JsonArray(Enumerable.Range(0, 40).Select(_ => (JsonNode)JsonValue.Create(0)!).ToArray());
        fixtureLevel["cliffs"] = new JsonArray();
        var scene = new AuthoredScene
        {
            LevelId = "actor-clip-depth",
            MapSize = new[] { 8, 5 },
            RuntimeReady = true,
            FramePlacements = new()
        };
        var engine = new BattleEngine(packs);
        var before = engine.CreateState(fixtureLevel);
        var units = before["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var aid = Int(units[0], "id");
        var did = Int(units[1], "id");
        void ConfigureDepth(AssetCatalog next)
        {
            board.Configure(new(next, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric), fixtureLevel, scene);
            board.ConfigureDynamicState();
        }
        var (manifest, bindings) = ActorClipFixture();
        ConfigureDepth(new AssetCatalog(manifest, bindings));
        board.RenderState(before);
        var terrainFirst = board.Projection.DepthPartitions.Min(partition => partition.StartDepth);
        var terrainEnd = board.Projection.DepthPartitions.Max(partition => partition.EndDepthExclusive);
        var actor = Actor(board, aid);
        var baseCut = (int)Math.Floor(board.Projection.Depth(actor.GetMeta("logical_contact").AsVector2())) + 1;
        const int offset = 1;
        Require(board.Projection.Faces.Count == 0 && board.RenderedCoverPropCount == 0
            && baseCut + offset > terrainFirst && baseCut + offset < terrainEnd
            && !board.Projection.DepthPartitions.Any(partition => partition.StartDepth == baseCut + offset
                || partition.EndDepthExclusive == baseCut + offset),
            "The flat actor clip geometry fixture requires a genuinely absent cut strictly inside the rendered terrain.");
        var trusted = manifest.Assets.Single(record => record.Id == "torren-stage01");
        // Geometry-only fixture over a trusted sheet, not new or approved character art.
        manifest.Assets.Add(new AssetRecord
        {
            Id = "fixture.offset-contact",
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
            GroundFront = new[] { trusted.PhysicalWidth / 2.0, 100.0 + offset * 16 },
            PresentationScale = 1.0 / 16
        });
        manifest.ActorClips[ClipFixtureId] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "fixture.offset-contact",
            Frames = new[] { new ActorClipFrameRecord { Frame = "attack", Span = 128 } }
        };
        var catalog = new AssetCatalog(manifest, bindings);
        ConfigureDepth(catalog);
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(before, (JsonObject)before.DeepClone(), IdleAction(), [Strike("attack", aid, did)]));
        if (task.IsFaulted)
            await task;
        var currentActor = Actor(board, aid);
        var currentSprite = currentActor.GetNode<Sprite2D>("AuthoredSprite");
        var actualOffset = SpriteGroundContact.Offset(currentSprite);
        var currentDepth = board.Projection.Depth(currentActor.GetMeta("logical_contact").AsVector2())
            + new SpriteStandingVisual(currentSprite).FrontOffset(currentActor).Y;
        var cuts = board.Projection.DepthPartitions.SelectMany(partition => new[] { partition.StartDepth, partition.EndDepthExclusive }).Distinct().Order();
        GD.Print($"ACTOR_CLIP_DEPTH actualOffset={actualOffset}; expectedOffset={offset}; expectedCut={baseCut + offset}; currentDepth={currentDepth}; actualCut={(int)Math.Floor(currentDepth) + 1}; terrain=[{terrainFirst},{terrainEnd}]; cuts=[{string.Join(",", cuts)}]");
        Require(actualOffset.Y == offset
            && board.Projection.DepthPartitions.Any(partition => partition.StartDepth == baseCut + offset
                || partition.EndDepthExclusive == baseCut + offset),
            "A clip with a different authored ground front prepares its own standing depth cut before drawing.");
        CheckActorDepthCuts(board);
        await Tick(board, .129);
        Require(Frame(Actor(board, aid)) == 0
            && SpriteGroundContact.Offset(Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite")).IsEqualApprox(Vector2.Zero),
            "An offset-contact clip restores the bound idle contact before impact.");
        CheckActorDepthCuts(board);
        await Drain(board, task);
    }

    private async Task CheckEffectScaleOwnership(BoardView board, JsonObject before, int aid, int did)
    {
        var document = PresentationJson.Load<JsonObject>("res://data/selected-assets.json");
        document["assets"]!.AsArray().OfType<JsonObject>().Single(record => record["topicId"]!.GetValue<string>() == "C01-FX-01")
            ["presentationScale"] = .5;
        var manifest = JsonSerializer.Deserialize<AssetManifest>(document.ToJsonString(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        var catalog = new AssetCatalog(manifest, PresentationJson.Load<ArtBindings>("res://data/art-bindings.json"));
        ConfigureActorClipBoard(board, catalog);
        var task = board.PresentTransitionAsync(viewportFeedback,
            Transition(before, (JsonObject)before.DeepClone(), IdleAction(), [Strike("areaAttack", aid, did)]));
        var effect = catalog.ResolveEffect("C01-FX-01");
        var sprite = board.GetNode<Sprite2D>("BattleEffects/Hit/Strip");
        var scale = Vector2.One * (effect.PresentationScale / effect.PixelDensity);
        Require(sprite.Scale.IsEqualApprox(scale), "An impact's initial frame honors its authored presentation scale.");
        await Tick(board, .084);
        Require(sprite.GetMeta("frame").AsInt32() == 1 && sprite.Scale.IsEqualApprox(scale),
            "Applying later impact frames cannot change scale ownership or resize the playing effect.");
        await Drain(board, task);
    }

    private async Task CheckActorSpecificClipPlayback(BoardView board, AssetCatalog catalog, JsonObject source, int aid, int did)
    {
        foreach (var (type, key) in new (string, string?)[]
        {
            ("c01.swordsman", "campaign-torren"), ("c01.laborer", null)
        })
        {
            var before = (JsonObject)source.DeepClone();
            var unit = before["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == aid);
            unit["type"] = type;
            if (key is null)
                unit.Remove("key");
            else
                unit["key"] = key;
            var selected = catalog.ResolveUnit(type, key);
            var clip = catalog.ResolveUnitClip(type, "attack", ActorClipUnit.Milliseconds, key);
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, (JsonObject)before.DeepClone(), IdleAction(), [Strike("attack", aid, did)]));
            var sprite = Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite");
            var facing = sprite.Scale.X < 0 ? -1 : 1;
            CheckClipSprite(sprite, selected, clip.FrameAt(0), facing);
            CheckClipShadow(sprite);
            Require(Frame(Actor(board, aid)) == AssetCatalog.FrameIndex(selected,
                selected.Category == "mission-unit" ? "carry-or-work" : "attack"),
                "Board playback resolves the named actor action or mission work pose from its actual binding.");
            await Tick(board, .129);
            Require(board.GetNodeOrNull("BattleEffects/Hit") is null && !task.IsCompleted,
                "Named and mission bindings retain their own 150 ms duration instead of borrowing a generic clip.");
            await Tick(board, .022);
            CheckClipSprite(sprite, selected, 0, facing);
            CheckClipShadow(sprite);
            Require(board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "Named and mission clips restore their selected idle sheet before impact.");
            await Drain(board, task);
        }
    }

    private async Task CheckActorClipLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == witness["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(), "The actor clip receipt witness starts from the unchanged legal campaign state.");
        var orders = witness["actions"]!.AsArray();
        battle.Dispatch(orders[0]!.AsObject());
        battle.Dispatch(orders[1]!.AsObject());
        var action = orders[2]!.AsObject();
        var receipt = battle.Dispatch(action);
        var attack = receipt.Events.First(value => value["type"]!.GetValue<string>() == "attack");
        var id = Int(attack, "attacker");
        var unit = receipt.Before["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == id);
        var type = unit["type"]!.GetValue<string>();
        var (manifest, bindings) = ActorClipFixture();
        manifest.ActorClips["fixture.legal"] = new()
        {
            Unit = "milliseconds",
            Frames = ClipFixtureFrames()
        };
        bindings.Units[type].ActionClips["attack"] = "fixture.legal";
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
                var movementMs = (action["path"]!.AsArray().Count - 1) * 160;
                await Tick(board, (movementMs + 1) / 1000.0);
                Require(Frame(Actor(board, id)) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "A real accepted movement-and-attack receipt enters the declared clip only after its committed path.");
                await Tick(board, .033);
                Require(Frame(Actor(board, id)) == 2 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The real receipt samples the second authored actor pose before impact.");
                await Tick(board, .064);
                Require(Frame(Actor(board, id)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The real receipt samples the final finite pose rather than using a fixed 150 ms attack frame.");
                await Tick(board, .032);
                Require(Frame(Actor(board, id)) == 0 && board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-" + attack["damage"]!.ToJsonString(),
                    "Real receipt impact follows the clip duration with its emitted damage unchanged.");
                await Drain(board, task);
                Require(completed.SequenceEqual(Enumerable.Range(0, receipt.Events.Count)) && battle.StateHash == hash
                    && StateDigest.Hash(after) == hash && source == transition.Before.GetRawText() + transition.After.GetRawText()
                        + transition.Action.GetRawText() + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "Finite actor clips preserve the legal receipt, event order and canonical battle digest.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private static ActorClipFrameRecord[] ClipFixtureFrames() => new[]
    {
        new ActorClipFrameRecord { Frame = "walk-a", Span = 32 },
        new ActorClipFrameRecord { Frame = "attack", Span = 64 },
        new ActorClipFrameRecord { Frame = "walk-b", Span = 32 }
    };

    private static (AssetManifest Manifest, ArtBindings Bindings) ActorClipFixture()
    {
        var document = PresentationJson.Load<JsonObject>("res://data/selected-assets.json");
        // Existing approved pixels only. A fixture-only filter contrast makes cross-sheet restoration observable.
        document["assets"]!.AsArray().OfType<JsonObject>().Single(record => record["id"]?.GetValue<string>() == "torren-stage01")["textureFilter"] = "nearest";
        var manifest = JsonSerializer.Deserialize<AssetManifest>(document.ToJsonString(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        manifest.ActorClips[ClipFixtureId] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "torren-stage01",
            Frames = ClipFixtureFrames()
        };
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        bindings.Units["c01.swordsman"].ActionClips["attack"] = ClipFixtureId;
        return (manifest, bindings);
    }

    private BoardView NewActorClipBoard(AssetCatalog catalog)
    {
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        ConfigureActorClipBoard(board, catalog);
        return board;
    }

    private void ConfigureActorClipBoard(BoardView board, AssetCatalog catalog)
    {
        board.Configure(new(catalog, EnvironmentCatalog.Load(), new Empire.Core.Geometry.Square4Grid(),
            new Empire.Core.State.ReadOnlyContent(packs), BoardPlane.Dimetric), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        board.ConfigureDynamicState();
    }

    private void CheckSettledClipActor(BoardView board, int id, RuntimeAsset asset, Vector2 contact)
    {
        var actor = Actor(board, id);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        CheckClipSprite(sprite, asset, 0, sprite.Scale.X < 0 ? -1 : 1);
        CheckClipShadow(sprite);
        Require(actor.Position == contact && actor.Scale == Vector2.One && actor.SelfModulate == Colors.White,
            "Settled actors retain their canonical contact, body scale and opacity after clip interruption.");
    }

    private void CheckClipSprite(Sprite2D sprite, RuntimeAsset asset, int frame, int facing)
    {
        Require(sprite.Texture is AtlasTexture texture && texture.Region == asset.RegionAt(frame)
            && sprite.Offset == -asset.AnchorAt(frame) * asset.PixelDensity
            && sprite.Scale.IsEqualApprox(new Vector2(facing, 1) * (asset.PresentationScale / asset.PixelDensity))
            && sprite.TextureFilter == asset.TextureFilter
            && sprite.GetMeta("art_identity").AsString() == asset.Identity && sprite.GetMeta("topic_id").AsString() == asset.TopicId
            && SpriteGroundContact.Offset(sprite).IsEqualApprox(Vector2.Zero),
            "Applying an actor frame updates crop, anchor, contact, scale magnitude, filter and art identity while preserving facing.");
    }

    private void CheckClipShadow(Sprite2D sprite)
    {
        var shadow = sprite.GetNode<SpriteShadows>("SourceSpriteShadows");
        Require(shadow.GetMeta("source_texture").AsUInt64() == sprite.Texture!.GetInstanceId()
            && (shadow.Scale * sprite.Scale.Abs()).IsEqualApprox(Vector2.One / 2)
            && shadow.GetMeta("composition_inputs").AsInt32() == 3 && sprite.SelfModulate.A == 0,
            "The composited figure follows the exact clip texture and compensates its new scale without drawing a second body.");
    }
}
