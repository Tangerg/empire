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
    private const string MovementFixtureId = "fixture.distance-three-poses";

    private static ActorClipFrameRecord[] MovementFixtureFrames() =>
    [
        new() { Frame = "walk-a", Span = .25 },
        new() { Frame = "attack", Span = .5 },
        new() { Frame = "walk-b", Span = .75 }
    ];

    private static (AssetManifest Manifest, ArtBindings Bindings) MovementClipFixture()
    {
        var (manifest, bindings) = ActorClipFixture();
        // Synthetic distance intervals over existing trusted pixels, not authored movement art.
        manifest.ActorClips[MovementFixtureId] = new()
        {
            Unit = "cells",
            AppearanceId = "torren-stage01",
            Frames = MovementFixtureFrames()
        };
        bindings.Units["c01.swordsman"].ActionClips["move"] = MovementFixtureId;
        return (manifest, bindings);
    }

    private void CheckMovementClipDefinitions()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            var clip = assets.ResolveUnitClip(type, "move", ActorClipUnit.Cells, key);
            var selected = assets.ResolveUnit(type, key);
            Require(clip.Unit == ActorClipUnit.Cells && clip.Asset == selected && clip.Length == 2
                && clip.FrameAt(0) == AssetCatalog.FrameIndex(selected, "walk-a")
                && clip.FrameAt(.999) == AssetCatalog.FrameIndex(selected, "walk-a")
                && clip.FrameAt(1) == AssetCatalog.FrameIndex(selected, "walk-b"),
                "Every shipped unit and named actor explicitly retains its two existing one-cell support poses.");
        }
        var (manifest, bindings) = MovementClipFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var moving = catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells);
        Require(moving.Asset.Identity == "torren-stage01" && moving.Length == 1.5,
            "A movement declaration owns a nonuniform subcell sequence on its selected alternate sheet.");
        foreach (var (distance, frame) in new (double, int)[]
        {
            (0, 1), (.249, 1), (.25, 2), (.749, 2), (.75, 3), (1.499, 3), (1.5, 3), (1000, 3)
        })
            Require(moving.FrameAt(distance) == frame,
                "Distance frames use half-open intervals; the finite sampler clamps and leaves looping to its caller.");
        foreach (var invalid in new[] { -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            try
            {
                moving.FrameAt(invalid);
            }
            catch (ArgumentOutOfRangeException) { assertions++; continue; }
            throw new InvalidOperationException("An invalid movement sample distance was accepted.");
        }
        Throws(() => catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Milliseconds),
            "A movement clip cannot be sampled as a timed action.");
        Throws(() => catalog.ResolveUnitClip("c01.swordsman", "attack", ActorClipUnit.Cells),
            "A timed action cannot be sampled as movement distance.");
        Require(catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells, "campaign-torren").Length == 2,
            "A named actor owns its movement binding rather than borrowing the generic alternate clip.");
        bindings.Actors["campaign-torren"].UnitArt.ActionClips.Remove("move");
        var missingNamed = new AssetCatalog(manifest, bindings);
        Throws(() => missingNamed.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells, "campaign-torren"),
            "A named actor without a movement declaration cannot fall back to the generic action map.");
        manifest.ActorClips[MovementFixtureId].Frames[0] = new()
        {
            Frame = "idle",
            Span = 9
        };
        bindings.Units["c01.swordsman"].ActionClips.Remove("move");
        Require(moving.Length == 1.5 && moving.FrameAt(.25) == 2
            && catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells) == moving,
            "Admitted distance clips detach mutable source intervals and bindings.");
        foreach (var unit in new[] { "", "seconds", "Cells", " cells" })
        {
            var (invalidManifest, invalidBindings) = MovementClipFixture();
            invalidManifest.ActorClips[MovementFixtureId] = new()
            {
                Unit = unit,
                AppearanceId = "torren-stage01",
                Frames = MovementFixtureFrames()
            };
            Throws(() => new AssetCatalog(invalidManifest, invalidBindings),
                "Clip units must be explicit canonical identities, never inferred or normalized.");
        }
    }

    private async Task CheckMovementClipPlayback()
    {
        CheckMovementClipDefinitions();
        var (manifest, bindings) = MovementClipFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var board = NewActorClipBoard(catalog);
        try
        {
            // Synthetic presentation fixture, deliberately independent of legal battle orders.
            var before = IdleClipState();
            var unit = before["units"]![0]!.AsObject();
            var id = Int(unit, "id");
            unit["x"] = 12;
            unit["y"] = 16;
            var path = new JsonArray(At(12, 16), At(13, 16), At(13, 17), At(12, 17), At(11, 17));
            Require(path.OfType<JsonObject>().All(point => level["terrain"]![Int(point, "y")]!.GetValue<string>()[Int(point, "x")] != '_'),
                "The synthetic turning path stays on actual rendered terrain, not void outside the board surface.");
            var after = (JsonObject)before.DeepClone();
            after["units"]![0]!["x"] = 11;
            after["units"]![0]!["y"] = 17;
            var action = MovementAction(id, path);
            var transition = Transition(before, after, action,
                [new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path.DeepClone() }]);
            await CheckMovementClipPace(board, catalog, transition, id, path);
            await CheckMovementClipLifetime(board, catalog, transition, id);
            await CheckMovementClipFailures(board, before, after, action, id);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckMovementClipDepth();
        await CheckMovementClipLegalReceipt();
    }

    private static JsonObject MovementAction(int id, JsonArray path) => new()
    {
        ["kind"] = "command",
        ["unit"] = id,
        ["path"] = path.DeepClone(),
        ["command"] = new JsonObject { ["ability"] = "wait" }
    };

    private async Task CheckMovementClipPace(BoardView board, AssetCatalog catalog,
        BattlePresentationTransition transition, int id, JsonArray path)
    {
        var clip = catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells);
        var samples = new (double Distance, int Frame)[]
        {
            (.125, 1), (.375, 2), (.625, 2), (.875, 3), (1.125, 3),
            (1.625, 1), (1.875, 2), (3.125, 1), (3.875, 3)
        };
        var human = new List<(Vector2 Contact, Vector2 Position, Vector2 Scale, Rect2 Region)>();
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var computer in new[] { false, true })
            {
                completed.Clear();
                var pace = computer ? .110 : .160;
                var task = board.PresentTransitionAsync(viewportFeedback, transition with
                {
                    IsComputerAction = computer
                });
                var actor = Actor(board, id);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                CheckClipSprite(sprite, clip.Asset, 1, sprite.Scale.X < 0 ? -1 : 1);
                CheckClipShadow(sprite);
                var elapsedDistance = 0.0;
                ulong unchangedTexture = 0;
                ulong unchangedShadow = 0;
                for (var index = 0; index < samples.Length; index++)
                {
                    var (distance, frame) = samples[index];
                    await Tick(board, (distance - elapsedDistance) * pace);
                    elapsedDistance = distance;
                    var segment = (int)Math.Floor(distance);
                    var start = path[segment]!.AsObject();
                    var end = path[segment + 1]!.AsObject();
                    var a = board.Projection.LogicalCellContact(new(Int(start, "x"), Int(start, "y")));
                    var b = board.Projection.LogicalCellContact(new(Int(end, "x"), Int(end, "y")));
                    var logical = a.Lerp(b, (float)(distance - segment));
                    var facing = board.Projection.Project(b).X < board.Projection.Project(a).X ? -1 : 1;
                    CheckClipSprite(sprite, clip.Asset, frame, facing);
                    CheckClipShadow(sprite);
                    Require(actor.GetMeta("logical_contact").AsVector2().IsEqualApprox(logical)
                        && actor.Position.IsEqualApprox(board.Projection.Project(logical))
                        && actor.Scale == Vector2.One && !task.IsCompleted && completed.Count == 0,
                        "Subcell movement preserves contact, linear distance, turns and overshoot without publishing a premature move.");
                    if (distance == .375)
                    {
                        unchangedTexture = sprite.Texture!.GetInstanceId();
                        unchangedShadow = sprite.GetNode<SpriteShadows>("SourceSpriteShadows").Texture!.GetInstanceId();
                    }
                    if (distance == .625)
                        Require(sprite.Texture!.GetInstanceId() == unchangedTexture
                            && sprite.GetNode<SpriteShadows>("SourceSpriteShadows").Texture!.GetInstanceId() == unchangedShadow,
                            "Advancing within one distance pose reuses the exact crop and composited shadow.");
                    var observed = (Contact: actor.GetMeta("logical_contact").AsVector2(), Position: actor.Position,
                        Scale: sprite.Scale, Region: ((AtlasTexture)sprite.Texture!).Region);
                    if (!computer)
                        human.Add(observed);
                    else
                        Require(observed.Contact.IsEqualApprox(human[index].Contact)
                            && observed.Position.IsEqualApprox(human[index].Position)
                            && observed.Scale.IsEqualApprox(human[index].Scale) && observed.Region == human[index].Region,
                            "Human and AI pacing select identical geometry, facing and position at identical travelled distances.");
                    CheckActorDepthCuts(board);
                }
                await Tick(board, (4 - elapsedDistance) * pace + .001);
                Require(await task && completed.SequenceEqual(new[] { 0 }) && !board.IsPresenting,
                    "The unchanged total cell duration completes once; a move event never marches the path again.");
                CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));
            }
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckMovementClipLifetime(BoardView board, AssetCatalog catalog,
        BattlePresentationTransition transition, int id)
    {
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .06);
            Require(Frame(Actor(board, id)) == 2, "The cancellation witness interrupts a visible alternate movement pose.");
            board.CancelPresentation();
            Require(!await task && completed.Count == 0 && !board.IsPresenting
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Movement cancellation retires the partial pose and all gates without publishing the unfinished event.");
            CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));
            await Tick(board, 1);
            CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));

            task = board.PresentTransitionAsync(viewportFeedback, transition);
            await Tick(board, .06);
            ConfigureActorClipBoard(board, assets);
            board.RenderState(EcmaJson.Parse(transition.After.GetRawText())!.AsObject());
            Require(!await task && completed.Count == 0 && !board.IsPresenting,
                "Rebinding during movement synchronously retires the old clip and cannot publish stale completion.");
            await Tick(board, 1);
            CheckSettledClipActor(board, id, assets.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));

            ConfigureActorClipBoard(board, catalog);
            board.AnimationsEnabled = false;
            task = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(task.IsCompleted && await task && completed.SequenceEqual(new[] { 0 })
                && board.GetNodeOrNull("BattleEffects") is null && !board.IsPresenting,
                "Reduced motion settles movement synchronously with the original event order and no pending tween.");
            CheckSettledClipActor(board, id, catalog.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));
            board.AnimationsEnabled = true;
            completed.Clear();
            var visible = transition.Before.GetProperty("units").EnumerateArray()
                .Select(unit => unit.GetProperty("id").GetInt32()).Where(value => value != id).ToHashSet();
            var hidden = new BattlePresentationTransition(transition.Revision, transition.Before, transition.Action,
                transition.Events, transition.After, false, visible, transition.EventDescriptions);
            task = board.PresentTransitionAsync(viewportFeedback, hidden, visible);
            Require(task.IsCompleted && await task && completed.SequenceEqual(new[] { 0 })
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "A fog-hidden mover never acquires a visible clip or delays the otherwise unchanged receipt.");
            await Tick(board, 1);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{id}") is null,
                "Retired movement or idle registrations cannot resurrect a fog-hidden actor.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckMovementClipFailures(BoardView board, JsonObject before, JsonObject after, JsonObject action, int id)
    {
        foreach (var wrongUnit in new[] { false, true })
        {
            var (manifest, bindings) = MovementClipFixture();
            if (wrongUnit)
                bindings.Units["c01.swordsman"].ActionClips["move"] = ClipFixtureId;
            else
                bindings.Units["c01.swordsman"].ActionClips.Remove("move");
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, action, Array.Empty<JsonObject>()));
            var rejected = false;
            try
            {
                await task;
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
                && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Missing or incorrectly dimensioned movement declarations fail explicitly and release playback ownership.");
            CheckSettledClipActor(board, id, assets.ResolveUnit("c01.swordsman"), board.Projection.CellContact(new(11, 17)));
            if (wrongUnit)
                continue;
            foreach (var mode in new[] { "push", "teleport" })
            {
                var forced = new JsonObject
                {
                    ["type"] = "forcedMove",
                    ["unit"] = id,
                    ["mode"] = mode,
                    ["from"] = At(12, 16),
                    ["to"] = At(11, 17),
                    ["path"] = new JsonArray(At(12, 16), At(11, 17))
                };
                task = board.PresentTransitionAsync(viewportFeedback,
                    Transition(before, after, IdleAction(), [forced, new JsonObject { ["type"] = "gameOver" }]));
                if (mode == "push")
                    await Tick(board, .06);
                var actor = Actor(board, id);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                CheckClipSprite(sprite, assets.ResolveUnit("c01.swordsman"), 0, sprite.Scale.X < 0 ? -1 : 1);
                CheckClipShadow(sprite);
                var start = board.Projection.LogicalCellContact(new(12, 16));
                var end = board.Projection.LogicalCellContact(new(11, 17));
                Require(actor.Position.IsEqualApprox(board.Projection.Project(mode == "push" ? start.Lerp(end, .5f) : end)),
                    "Forced slides and teleports preserve canonical base geometry and never resolve the absent movement action.");
                await Drain(board, task);
            }
        }
    }

    private async Task CheckMovementClipDepth()
    {
        var fixtureLevel = MaterialSceneFixtureChecks.Level;
        fixtureLevel["id"] = "movement-clip-depth";
        fixtureLevel["terrain"] = new JsonArray("........", "........", "........", "........", "........");
        fixtureLevel["elevation"] = new JsonArray(Enumerable.Range(0, 40).Select(_ => (JsonNode)JsonValue.Create(0)!).ToArray());
        fixtureLevel["cliffs"] = new JsonArray();
        var scene = new AuthoredScene { LevelId = "movement-clip-depth", MapSize = [8, 5], RuntimeReady = true, FramePlacements = new() };
        var engine = new BattleEngine(packs);
        var before = engine.CreateState(fixtureLevel);
        var units = before["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var id = Int(units[0], "id");
        var bystander = Int(units[1], "id");
        var (manifest, bindings) = MovementClipFixture();
        var trusted = manifest.Assets.Single(record => record.Id == "torren-stage01");
        // Geometry-only witnesses use the same trusted sheet; no new character pixels are introduced.
        foreach (var (name, front) in new[] { ("fixture.move-contact", 132.0), ("fixture.idle-contact", 116.0) })
            manifest.Assets.Add(new AssetRecord
            {
                Id = name,
                TopicId = trusted.TopicId,
                ResourcePath = trusted.ResourcePath,
                Width = trusted.PhysicalWidth,
                Height = trusted.PhysicalHeight,
                Frames = 1,
                FrameOrder = ["idle"],
                PhysicalWidth = trusted.PhysicalWidth,
                PhysicalHeight = trusted.PhysicalHeight,
                PixelDensity = 1,
                Anchor = [trusted.PhysicalWidth / 2.0, 100],
                GroundFront = [trusted.PhysicalWidth / 2.0, front],
                PresentationScale = 1.0 / 16
            });
        manifest.ActorClips[MovementFixtureId] = new()
        {
            Unit = "cells",
            AppearanceId = "fixture.move-contact",
            Frames = [new() { Frame = "idle", Span = .5 }]
        };
        manifest.ActorClips["fixture.movement-bystander-idle"] = new()
        {
            Unit = "milliseconds",
            AppearanceId = "fixture.idle-contact",
            Frames = [new() { Frame = "idle", Span = 200 }]
        };
        bindings.Units["c01.swordsman"].ActionClips["idle"] = "fixture.movement-bystander-idle";
        var catalog = new AssetCatalog(manifest, bindings);
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(catalog, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric), fixtureLevel, scene);
            board.ConfigureDynamicState();
            board.RenderState(before);
            var origin = board.Projection.LogicalCellContact(new(1, 3));
            var missingCut = (int)Math.Floor(board.Projection.Depth(origin)) + 3;
            Require(board.Projection.Faces.Count == 0 && board.RenderedCoverPropCount == 0
                && missingCut > board.Projection.DepthPartitions.Min(partition => partition.StartDepth)
                && missingCut < board.Projection.DepthPartitions.Max(partition => partition.EndDepthExclusive)
                && !board.Projection.DepthPartitions.Any(partition => partition.StartDepth == missingCut || partition.EndDepthExclusive == missingCut),
                "The movement contact witness begins without its required interior cut, so existing terrain cannot mask preparation errors.");
            var path = new JsonArray(At(1, 3), At(2, 3), At(2, 2), At(2, 1));
            var after = (JsonObject)before.DeepClone();
            after["units"]![0]!["x"] = 2;
            after["units"]![0]!["y"] = 1;
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, MovementAction(id, path), Array.Empty<JsonObject>()));
            void CheckGeometry()
            {
                var actor = Actor(board, id);
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var move = catalog.ResolveUnitClip("c01.swordsman", "move", ActorClipUnit.Cells).Asset;
                Require(sprite.Texture is AtlasTexture texture && texture.Region == move.RegionAt(0)
                    && sprite.Offset == -move.AnchorAt(0) && sprite.Scale.Abs().IsEqualApprox(Vector2.One / 16)
                    && SpriteGroundContact.Offset(sprite).IsEqualApprox(new Vector2(0, 2))
                    && actor.ZIndex == board.Projection.LayerAtDepth(board.Projection.Depth(actor.GetMeta("logical_contact").AsVector2()) + 2,
                        BoardDepthLayer.Standing),
                    "Movement applies its full crop, anchor, scale and authored front before immediately sorting the current actor.");
                CheckClipShadow(sprite);
                Require(SpriteGroundContact.Offset(Actor(board, bystander).GetNode<Sprite2D>("AuthoredSprite")).IsEqualApprox(new Vector2(0, 1)),
                    "Preparing movement does not replace an independent actor's alternate idle geometry.");
                CheckActorDepthCuts(board);
            }
            CheckGeometry();
            var depths = path.OfType<JsonObject>().Select(point => board.Projection.Depth(
                board.Projection.LogicalCellContact(new(Int(point, "x"), Int(point, "y"))))).ToArray();
            var first = (int)Math.Floor(depths.Min()) + 1;
            var last = (int)Math.Floor(depths.Max() + 2) + 1;
            Require(Enumerable.Range(first, last - first + 1).All(cut => board.Projection.DepthPartitions
                .Any(partition => partition.StartDepth == cut || partition.EndDepthExclusive == cut)),
                "Before movement starts, the complete turning path reserves both canonical base and alternate front depths.");
            foreach (var seconds in new[] { .06, .12, .16 })
            {
                await Tick(board, seconds);
                CheckGeometry();
            }
            await Drain(board, task);
            Require(SpriteGroundContact.Offset(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite")).IsEqualApprox(new Vector2(0, 1)),
                "Completed movement restores the declared idle geometry rather than retaining its alternate movement front.");
            CheckActorDepthCuts(board);
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private async Task CheckMovementClipLegalReceipt()
    {
        var witness = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var checkpoint = witness["checkpoints"]![0]!.AsObject();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == witness["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == witness["initialStateHash"]!.GetValue<string>(),
            "The movement integration witness starts from the unchanged recorded C06 campaign state.");
        var index = Int(checkpoint, "index");
        foreach (var order in witness["actions"]!.AsArray().Take(index).OfType<JsonObject>())
            battle.Dispatch(order);
        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(),
            "The original accepted prefix reaches the exact recorded charge state.");
        var action = witness["actions"]![index]!.AsObject();
        var receipt = battle.Dispatch(action);
        var attack = receipt.Events.First(value => value["type"]!.GetValue<string>() == "attack");
        var id = Int(attack, "attacker");
        var unit = receipt.Before["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == id);
        var type = unit["type"]!.GetValue<string>();
        var key = unit["key"]?.GetValue<string>();
        var (manifest, bindings) = MovementClipFixture();
        manifest.ActorClips["fixture.legal-distance"] = new()
        {
            Unit = "cells",
            Frames = MovementFixtureFrames()
        };
        manifest.ActorClips["fixture.legal-distance-strike"] = new()
        {
            Unit = "milliseconds",
            Frames = ClipFixtureFrames()
        };
        var binding = key is not null && bindings.Actors.TryGetValue(key, out var named) ? named.UnitArt : bindings.Units[type];
        binding.ActionClips["move"] = "fixture.legal-distance";
        binding.ActionClips["attack"] = "fixture.legal-distance-strike";
        var catalog = new AssetCatalog(manifest, bindings);
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            board.Configure(new(catalog, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric),
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
                await Tick(board, .06);
                Require(Frame(Actor(board, id)) == 2 && completed.Count == 0,
                    "The real accepted charge samples its nonuniform distance clip before publishing any event.");
                await Tick(board, .12);
                Require(Frame(Actor(board, id)) == 3 && completed.Count == 0,
                    "The accepted charge retains movement phase through its first cell and turn.");
                var movementMs = (action["path"]!.AsArray().Count - 1) * 160;
                await Tick(board, (movementMs - 180 + 1) / 1000.0);
                Require(Frame(Actor(board, id)) == 1 && board.GetNodeOrNull("BattleEffects/Hit") is null,
                    "The unchanged total path duration hands pose ownership directly to the millisecond attack clip.");
                await Tick(board, .033);
                Require(Frame(Actor(board, id)) == 2, "The following real attack uses time, not leftover travelled distance.");
                await Tick(board, .064);
                Require(Frame(Actor(board, id)) == 3, "The real attack retains its independent finite final pose.");
                await Tick(board, .032);
                Require(board.GetNode<Label>("BattleEffects/Hit/Number").Text == "-" + attack["damage"]!.ToJsonString(),
                    "The movement-to-strike handoff preserves the recorded damage impact.");
                await Drain(board, task);
                Require(completed.SequenceEqual(Enumerable.Range(0, receipt.Events.Count))
                    && battle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>()
                    && StateDigest.Hash(after) == checkpoint["afterStateHash"]!.GetValue<string>()
                    && EcmaJson.Sha256(new JsonArray(receipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                        == checkpoint["eventsSha256"]!.GetValue<string>()
                    && source == transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText()
                        + string.Concat(transition.Events.Select(value => value.GetRawText())),
                    "Distance clips leave the accepted C06 state hashes, exact event bytes, order and immutable receipt unchanged.");
            }
            finally { board.EventPresented -= Presented; }
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
