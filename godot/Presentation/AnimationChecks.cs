#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

/// <summary>Native resource and controllable-clock contracts from the source board/presenters.</summary>
public partial class AnimationChecks : Node
{
    private int assertions;
    private AssetCatalog assets = null!;
    private JsonObject initial = null!;
    private string initialBytes = "";
    private JsonObject level = null!;
    private JsonArray packs = null!;
    private long revision;

    public override async void _Ready()
    {
        try
        {
            assets = AssetCatalog.Load();
            level = BattleTextChecks.OpeningLevel();
            packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            initial = new Empire.Core.Engine.BattleEngine(packs).CreateState(level);
            initialBytes = initial.ToJsonString();
            CheckAssets();
            CheckTransitionOwnership();
            await CheckClock();
            var board = NewBoard();
            await CheckMovement(board);
            await CheckStrikeAndDeath(board);
            await CheckSourceLookupBoundaries(board);
            await CheckHealAndSpawn(board);
            await CheckAnnouncements(board);
            await CheckUnhandledAndVisibility(board);
            await CheckCancellation(board);
            board.QueueFree();
            GD.Print($"Animation presentation checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private BoardView NewBoard()
    {
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        board.Configure(new(assets, EnvironmentCatalog.Load(), new Empire.Core.Geometry.Square4Grid(), new Empire.Core.State.ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")), BoardPlane.Dimetric), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        board.ConfigureDynamicState();
        board.RenderState(initial);
        return board;
    }

    private void CheckAssets()
    {
        foreach (var n in Enumerable.Range(1, 8).Append(17))
        {
            var asset = assets.ResolveEffect($"C01-FX-{n:00}");
            Require(asset.FrameCount == 4 && asset.FrameOrder.Count == 0 && asset.FramesPerSecond == 12 && !asset.Loop,
                "Source FX use numeric four-frame strips at 12 fps without invented named roles");
            Require(asset.Anchor == new Vector2(16, 16) && asset.FrameWidth == 32 && asset.FrameHeight == 32
                && asset.PixelDensity == 2 && asset.BlendMode == "normal", "Source logical FX geometry and blend are preserved");
            var pixels = new HashSet<string>();
            for (var frame = 0; frame < 4; frame++)
            {
                var texture = assets.CreateFrameTexture(asset, frame);
                using var image = texture.GetImage();
                Require(texture.Region == new Rect2(frame * 64, 0, 64, 64) && image.GetUsedRect().HasArea(),
                    "Each FX frame is a nonempty correctly cropped original HD source");
                pixels.Add(Convert.ToBase64String(image.GetData()));
            }
            Require(pixels.Count == 4, "FX phases must be four different original pictures");
            Require(BoardView.StripFrame(asset, 0) == 0 && BoardView.StripFrame(asset, 84) == 1
                && BoardView.StripFrame(asset, 167) == 2 && BoardView.StripFrame(asset, 250) == 3
                && BoardView.StripFrame(asset, 600) == 3, "Nonlooping source strips hold their last frame");
            Require(BoardView.StripFrame(asset with
            {
                Loop = true
            }, 500) == 2,
                "Source loop metadata repeats numeric frames when explicitly enabled");
        }
        Require(assets.ResolveWeaponEffect("c01.border-blade")?.TopicId == "C01-FX-01"
            && assets.ResolveWeaponEffect("c01.gray-oath")?.TopicId == "C01-FX-06"
            && assets.ResolveWeaponEffect("c01.colossus-sweep")?.TopicId == "C01-FX-08", "Typed weapon bindings select original effect topics");
        Require(assets.ResolveWeaponEffect("mage_meteor") is null, "Weapons with no source binding receive no invented themed effect");
        Require(assets.ResolveHealEffect()?.TopicId == "C01-FX-17", "Heal topic is injected from the source presentation binding, not owned by the board");
        Throws(() => assets.ResolveEffect("C01-UNIT-SWORDSMAN"), "An actor topic cannot be used as an effect strip");

        var records = PresentationJson.Load<JsonObject>("res://data/selected-assets.json");
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        void Reject(Action<JsonObject> edit, string why)
        {
            var copy = (JsonObject)records.DeepClone();
            edit(copy);
            Throws(() => new AssetCatalog(JsonSerializer.Deserialize<AssetManifest>(copy.ToJsonString(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!, bindings), why);
        }
        JsonObject Record(JsonObject manifest, string topic) => manifest["assets"]!.AsArray().OfType<JsonObject>()
            .Single(value => value["topicId"]!.GetValue<string>() == topic && value["id"] is null);
        Reject(value => Record(value, "C01-UNIT-SWORDSMAN").Remove("frameOrder"), "Numeric FX support must not weaken actor role validation");
        Reject(value => Record(value, "C01-FX-01").Remove("anchor"), "FX cannot silently invent a centered pivot");
        Reject(value => Record(value, "C01-FX-01")["anchor"] = new JsonArray(32, 16), "FX pivot must be within, not on the far frame edge");
        Reject(value => Record(value, "C01-FX-01")["fps"] = 0, "Nonpositive FX frame rates must fail explicitly");
        Reject(value => Record(value, "C01-FX-01")["blendMode"] = "additive", "Unimplemented FX blend contracts must not silently become normal");
    }

    private async Task CheckClock()
    {
        var clock = new PresentationTimeline();
        var progress = -1.0;
        var tween = clock.TweenAsync(100, t => progress = t);
        clock.Advance(0.025);
        Require(progress == 0.25 && !tween.IsCompleted, "Timeline begins at its own play time");
        clock.Advance(0.075);
        Require(await tween && progress == 1 && clock.PendingCount == 0, "Timeline completes exactly once at its duration");
        var a = clock.WaitAsync(20);
        var b = clock.WaitAsync(50);
        clock.Cancel();
        Require(!await a && !await b && clock.PendingCount == 0, "Cancellation must resolve every waiter, not leave an await hanging");
        Require(!await clock.WaitAsync(1), "Cancelled clocks cannot start another transition");
        var reduced = new PresentationTimeline { MotionEnabled = false };
        Require(await reduced.TweenAsync(100, t => progress = t) && progress == 1 && reduced.PendingCount == 0,
            "Reduced motion settles at the final value without a clock wait");
        var broken = new PresentationTimeline();
        var failed = broken.TweenAsync(10, _ => throw new InvalidOperationException("test step defect"));
        broken.Advance(0.010);
        try
        {
            await failed;
            throw new Exception("Step error was swallowed");
        }
        catch (InvalidOperationException error) { Require(error.Message == "test step defect", "Clock errors must reach the playback boundary"); }
    }

    private void CheckTransitionOwnership()
    {
        var doc = JsonDocument.Parse("{\"type\":\"gameOver\"}");
        var values = new List<JsonElement> { doc.RootElement };
        var visible = new HashSet<int> { 1, 2 };
        var transition = new BattlePresentationTransition(9, doc.RootElement, doc.RootElement, values, doc.RootElement, false, visible);
        values.Clear();
        visible.Clear();
        doc.Dispose();
        Require(transition.Events.Count == 1 && transition.BeforeVisibleUnitIds!.SetEquals(new[] { 1, 2 }),
            "Transition collections must not retain caller-owned mutable lists or sets");
        Require(transition.Before.GetProperty("type").GetString() == "gameOver" && transition.After.GetProperty("type").GetString() == "gameOver"
            && transition.Action.GetProperty("type").GetString() == "gameOver" && transition.Events[0].GetProperty("type").GetString() == "gameOver",
            "Every transition JsonElement remains valid after the caller disposes its JsonDocument");
        try
        {
            ((IList<JsonElement>)transition.Events).Clear();
            throw new Exception("Events remained mutable");
        }
        catch (NotSupportedException) { Require(transition.Events.Count == 1, "Downcasting cannot mutate the transition event list"); }
        Require(transition.BeforeVisibleUnitIds is not HashSet<int>, "Transition visibility is a frozen set, not a mutable set behind a read-only interface");
    }

    private async Task CheckMovement(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var unit = after["units"]![0]!.AsObject();
        var id = Int(unit, "id");
        var x = Int(unit, "x");
        var y = Int(unit, "y");
        unit["x"] = x + 2;
        unit["done"] = true;
        var path = new JsonArray(At(x, y), At(x + 1, y), At(x + 2, y));
        var action = new JsonObject { ["kind"] = "command", ["unit"] = id, ["path"] = path.DeepClone(), ["command"] = new JsonObject { ["ability"] = "wait" } };
        var transition = Transition(before, after, action, [new JsonObject { ["type"] = "move", ["unit"] = id, ["path"] = path }]);
        var input = transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText();
        var task = board.PresentTransitionAsync(transition);
        var actor = Actor(board, id);
        Require(board.IsPresenting && Frame(actor) == 1, "Movement immediately selects source walk-a");
        await Tick(board, 0.0425);
        Require(actor.Position.IsEqualApprox(board.Projection.Project(board.Projection.LogicalCellContact(new(x, y)).Lerp(board.Projection.LogicalCellContact(new(x + 1, y)), .5f)) + new Vector2(0, -2.5f)),
            "Human 85 ms step has source quadratic halfway motion and 2.5 px hop");
        await Tick(board, 0.0426);
        await Tick(board, 0.075);
        Require(Frame(actor) == 3, "Source walk clip alternates walk-a/walk-b at 6.25 fps across path segments");
        await Tick(board, 0.011);
        Require(await task && !board.IsPresenting, "Move event must not replay the path a second time");
        Require(Actor(board, id).Position == board.Projection.CellContact(new(x + 2, y)), "Settled actor uses committed destination");
        Require(input == transition.Before.GetRawText() + transition.After.GetRawText() + transition.Action.GetRawText(), "Animation cannot mutate detached transition JSON");

        var ai = transition with
        {
            IsComputerAction = true
        };
        task = board.PresentTransitionAsync(ai);
        actor = Actor(board, id);
        await Tick(board, 0.0325);
        Require(Mathf.IsEqualApprox(actor.Position.X, board.Projection.Project(board.Projection.LogicalCellContact(new(x, y)).Lerp(board.Projection.LogicalCellContact(new(x + 1, y)), .5f)).X), "AI uses source 65 ms per tile, not the human pace");
        board.CancelPresentation();
        Require(!await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
            "Cancel removes temporary layers and resolves playback false");
        Require(Mathf.IsEqualApprox(Actor(board, id).Position.X, board.Projection.CellContact(new(x + 2, y)).X), "Mounted cancellation settles at committed state");
    }

    private async Task CheckStrikeAndDeath(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var attacker = before["units"]![0]!.AsObject();
        var target = before["units"]!.AsArray().Last()!.AsObject();
        var aid = Int(attacker, "id");
        var did = Int(target, "id");
        var at = board.Projection.CellCenter(new(Int(target, "x"), Int(target, "y")));
        var attack = Strike("attack", aid, did);
        var task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [attack]));
        var actor = Actor(board, aid);
        var origin = actor.Position;
        Require(Frame(actor) == 2, "Strike selects the declared source attack frame");
        await Tick(board, 0.075);
        var direction = (board.Projection.CellContact(new(Int(target, "x"), Int(target, "y")))
            - board.Projection.CellContact(new(Int(attacker, "x"), Int(attacker, "y")))).Normalized();
        Require(actor.Position.IsEqualApprox(origin + direction * 7), "Strike has source 150 ms sine lunge of seven pixels");
        await Tick(board, 0.076);
        var hit = board.GetNode<Node2D>("BattleEffects/Hit");
        Require(hit.Position == at && Frame(actor) == 0 && actor.Position == origin, "Impact follows wind-up and actor returns idle/origin");
        Require(hit.GetNode<Label>("Number").Text == "-12", "Damage number comes directly from emitted damage");
        var numberY = hit.GetNode<Label>("Number").Position.Y;
        await Tick(board, 0.210);
        Require(hit.GetNode<Sprite2D>("Strip").GetMeta("frame").AsInt32() == 2, "Hit plays original numeric FX frames at twelve fps");
        Require(Mathf.IsEqualApprox(hit.GetNode<Label>("Number").Position.Y, numberY - 7)
            && Mathf.IsEqualApprox(hit.GetNode<Label>("Number").Modulate.A, 0.75f), "Damage number follows source 14 px rise and quadratic fade");
        await Tick(board, 0.211);
        Require(await task && board.GetNodeOrNull("BattleEffects") is null, "420 ms hit removes every transient node");

        foreach (var kind in new[] { "areaAttack", "partingShot", "counter", "supportAttack" })
        {
            var item = Strike(kind, aid, did);
            if (kind == "partingShot")
                item["at"] = At(1, 2);
            task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [item]));
            if (kind != "areaAttack")
                await Tick(board, 0.151);
            hit = board.GetNode<Node2D>("BattleEffects/Hit");
            Require(hit.Position == (kind == "partingShot" ? board.Projection.CellCenter(new(1, 2)) : at),
                $"{kind}: source impact target and area no-extra-wind-up contract");
            await Drain(board, task);
        }

        after["units"]!.AsArray().RemoveAt(after["units"]!.AsArray().Count - 1);
        attack["killed"] = true;
        task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [attack,
            new JsonObject { ["type"] = "death", ["unit"] = did, ["at"] = At(Int(target, "x"), Int(target, "y")) }]));
        Require(Actor(board, did) is not null, "Dead units retain only a before-state view ghost until departure playback");
        await Tick(board, 0.151);
        Require(board.GetNode<Node2D>("BattleEffects/Hit").Position == at, "Fatal hit lands on last-seen position despite absence from After");
        await Tick(board, 0.421);
        Require(board.GetNodeOrNull("BattleEffects/Hit") is null && Actor(board, did) is CanvasGroup && Actor(board, did).SelfModulate.A == 1,
            "Fatal hit has a forty-millisecond beat before death");
        await Tick(board, 0.041);
        await Tick(board, 0.110);
        var ghost = Actor(board, did);
        Require(ghost is CanvasGroup && Mathf.IsEqualApprox(ghost.SelfModulate.A, 0.5f) && Mathf.IsEqualApprox(ghost.Scale.X, 0.825f),
            "Death uses source 220 ms fade and 35 percent shrink");
        Require(Mathf.IsEqualApprox(ghost.Position.Y + ghost.Scale.Y * -15, board.Projection.CellContact(new(Int(target, "x"), Int(target, "y"))).Y - 15),
            "Death scaling preserves its screen-space billboard pivot above the projected foot");
        await Tick(board, 0.111);
        Require(await task && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
            "Death removes the ghost and final settled state has no dead actor");
    }

    private async Task CheckHealAndSpawn(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var target = before["units"]![0]!.AsObject();
        var id = Int(target, "id");
        var task = board.PresentTransitionAsync(Transition(before, after, IdleAction(),
            [new JsonObject { ["type"] = "heal", ["source"] = id, ["target"] = id, ["amount"] = 20 }]));
        var heal = board.GetNode<Node2D>("BattleEffects/Heal");
        Require(heal.GetNode<Sprite2D>("Strip").GetMeta("topic_id").AsString() == "C01-FX-17"
            && heal.GetNode<Label>("Number").Text == "+20", "Heal uses the original source topic and emitted amount");
        var origin = heal.GetNode<Label>("Number").Position;
        await Tick(board, 0.250);
        Require(heal.GetNode<Label>("Number").Position == origin + new Vector2(0, -8), "Heal rises sixteen pixels over 500 ms");
        await Tick(board, 0.251);
        Require(await task, "Heal completes without a synthetic attack wind-up");

        var spawned = (JsonObject)target.DeepClone();
        spawned["id"] = 1000;
        spawned["key"] = "animation-recruit";
        spawned["x"] = 1;
        spawned["y"] = 1;
        after["units"]!.AsArray().Add(spawned);
        var repaints = 0;
        void Repaint() => repaints++;
        board.PresentationRepaintRequested += Repaint;
        task = board.PresentTransitionAsync(Transition(before, after, IdleAction(),
            [new JsonObject { ["type"] = "recruit", ["unit"] = 1000, ["at"] = At(1, 1) }]));
        Require(repaints == 1 && board.ProjectionUnitCount == after["units"]!.AsArray().Count,
            "Recruit repaints committed board and requests header refresh before spawn animation");
        await Tick(board, 0.120);
        Require(Actor(board, 1000) is CanvasGroup && Mathf.IsEqualApprox(Actor(board, 1000).SelfModulate.A, 0.5f) && Mathf.IsEqualApprox(Actor(board, 1000).Scale.X, 0.8f),
            "Recruit uses source 240 ms alpha and 0.6-to-1 scale");
        await Tick(board, 0.121);
        Require(await task && Actor(board, 1000) is CanvasGroup && Actor(board, 1000).SelfModulate == Colors.White && Actor(board, 1000).Scale == Vector2.One,
            "Spawn finishes with canonical opacity and scale");
        board.PresentationRepaintRequested -= Repaint;
    }

    private async Task CheckSourceLookupBoundaries(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var attacker = before["units"]![0]!.AsObject();
        var target = before["units"]!.AsArray().Last()!.AsObject();
        var aid = Int(attacker, "id");
        var did = Int(target, "id");
        after["units"]!.AsArray().RemoveAt(0);
        var task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [Strike("attack", aid, did)]));
        Require(board.GetNodeOrNull("BattleEffects/Hit") is not null,
            "Source skips wind-up when the acting unit was killed later in the committed transaction, but still paints its emitted hit");
        await Drain(board, task);

        before = Copy();
        after = Copy();
        var structure = new JsonObject
        {
            ["id"] = "animation-node",
            ["type"] = "command_node",
            ["owner"] = 2,
            ["x"] = 1,
            ["y"] = 2,
            ["hp"] = 100,
            ["disabled"] = false
        };
        before["structures"]!.AsArray().Add(structure);
        var destroyed = (JsonObject)structure.DeepClone();
        destroyed["hp"] = 0;
        destroyed["disabled"] = true;
        after["structures"]!.AsArray().Add(destroyed);
        var impact = new JsonObject
        {
            ["type"] = "attackStructure",
            ["attacker"] = aid,
            ["structure"] = "animation-node",
            ["weapon"] = "c01.border-blade",
            ["damage"] = 100,
            ["destroyed"] = true
        };
        task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [impact]));
        await Tick(board, 0.151);
        Require(board.GetNode<Node2D>("BattleEffects/Hit").Position == board.Projection.CellCenter(new(1, 2)),
            "Source destroyed structures remain in committed state and retain their impact coordinates");
        await Drain(board, task);

        after["structures"]!.AsArray().Clear();
        task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [impact]));
        var actor = Actor(board, aid);
        var origin = actor.Position;
        await Tick(board, 0.075);
        Require(Frame(actor) == 2 && actor.Position == origin,
            "An actually absent structure gives source wind-up toward self, not an invented last-seen structure location");
        await Tick(board, 0.076);
        Require(task.IsCompleted && await task && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "Source absent-structure impact is skipped instead of fabricating an explosion");

        // A committed delayed cast already emits an ordinary attack; its marker
        // is not another hit or a second cast-start animation in the source.
        task = board.PresentTransitionAsync(Transition(Copy(), Copy(), IdleAction(), [
            new JsonObject { ["type"] = "castResolved", ["unit"] = aid, ["weapon"] = "c01.border-blade", ["at"] = At(Int(target, "x"), Int(target, "y")) },
            Strike("attack", aid, did)]));
        Require(Frame(Actor(board, aid)) == 2 && board.GetNode<Node2D>("BattleEffects").GetChildCount() == 0,
            "castResolved does not produce a duplicate impact before the actual attack presenter");
        await Tick(board, 0.151);
        await Tick(board, 0.421);
        Require(task.IsCompleted && await task, "Delayed cast resolution takes exactly the ordinary strike-plus-hit sequence");
    }

    private async Task CheckAnnouncements(BoardView board)
    {
        var after = Copy();
        after["turn"] = 3;
        var repaints = 0;
        void Repaint() => repaints++;
        board.PresentationRepaintRequested += Repaint;
        var task = board.PresentTransitionAsync(Transition(Copy(), after, IdleAction(),
            [new JsonObject { ["type"] = "turnStart", ["player"] = 1, ["turn"] = 3 }, new JsonObject { ["type"] = "gameOver", ["team"] = 1, ["reason"] = "fixture" }]));
        Require(repaints == 1 && board.GetNode<Label>("BattleEffects/TurnAnnouncement/Band/Number").Text == "Localized announcement",
            "Turn banner displays the supplied localized receipt text and repaints the header before appearing");
        await Tick(board, 0.2201);
        await Tick(board, 0.4201);
        Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/TurnAnnouncement") is not null, "Turn banner has source 220 ms entrance and 420 ms hold");
        await Tick(board, 0.2201);
        Require(!task.IsCompleted && board.GetNodeOrNull("BattleEffects/TurnAnnouncement") is null, "Banner leaves in 220 ms before separate game-over beat");
        await Tick(board, 0.2201);
        Require(await task, "Game-over beat is 220 ms, then caller may show its result modal");
        board.PresentationRepaintRequested -= Repaint;
    }

    private async Task CheckUnhandledAndVisibility(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var unit = before["units"]![0]!.AsObject();
        var id = Int(unit, "id");
        var kinds = new[] { "move", "castBegan", "castResolved", "castCancelled", "forcedMove", "collisionDamage", "statusTick", "regen",
            "unitWithdrawn", "unitEmbarked", "unitDisembarked", "unitRouted", "unitSurrendered", "unitSpawned", "unitRevived", "structureDamaged", "structureDestroyed", "structureRepaired", "structureDisabled", "structureEnabled" };
        foreach (var kind in kinds)
        {
            var task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [new JsonObject { ["type"] = kind }]));
            Require(task.IsCompleted && await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                $"Source-unhandled {kind} must not invent a visual event or delay");
        }
        var defender = before["units"]!.AsArray().Last()!.AsObject();
        var did = Int(defender, "id");
        var hidden = Transition(before, after, IdleAction(), [Strike("attack", id, did)], new HashSet<int> { id });
        var hiddenTask = board.PresentTransitionAsync(hidden, new HashSet<int> { id });
        Require(hiddenTask.IsCompleted && await hiddenTask && board.RenderedUnitCount == 1,
            "Before visibility gates impact positions and ghosts without new rule-side visibility inference");
        board.AnimationsEnabled = false;
        var reduced = board.PresentTransitionAsync(Transition(before, after, IdleAction(), [Strike("attack", id, did)]));
        Require(reduced.IsCompleted && await reduced && board.GetNodeOrNull("BattleEffects") is null,
            "Reduced-motion playback settles synchronously without residual effects");
        board.AnimationsEnabled = true;
    }

    private async Task CheckCancellation(BoardView board)
    {
        var before = Copy();
        var after = Copy();
        var aid = Int(before["units"]![0]!.AsObject(), "id");
        var did = Int(before["units"]!.AsArray().Last()!.AsObject(), "id");
        after["units"]!.AsArray().RemoveAt(after["units"]!.AsArray().Count - 1);
        var attack = Strike("attack", aid, did);
        attack["killed"] = true;
        var fatal = Transition(before, after, IdleAction(), [attack, new JsonObject { ["type"] = "death", ["unit"] = did, ["at"] = At(0, 0) }]);
        var interrupted = board.PresentTransitionAsync(fatal);
        await Tick(board, 0.151);
        Require(board.GetNodeOrNull("BattleEffects/Hit") is not null, "Cancellation fixture interrupts a live FX strip");
        board.CancelPresentation();
        Require(!await interrupted && board.GetNodeOrNull("BattleEffects") is null
            && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
            "Cancelling mid-impact removes FX and ghost while leaving the committed death settled");
        interrupted = board.PresentTransitionAsync(fatal);
        await Tick(board, 0.151);
        await Tick(board, 0.421);
        await Tick(board, 0.041);
        await Tick(board, 0.05);
        board.CancelPresentation();
        Require(!await interrupted && board.GetNodeOrNull($"CanonicalProjection/Unit_{did}") is null,
            "Cancelling a partial death cannot leave a shrunken stale actor");

        var transition = Transition(Copy(), Copy(), IdleAction(), [new JsonObject { ["type"] = "gameOver", ["reason"] = "fixture", ["team"] = 1 }]);
        var first = board.PresentTransitionAsync(transition);
        try
        {
            await board.PresentTransitionAsync(transition);
            throw new Exception("Concurrent playback accepted");
        }
        catch (InvalidOperationException) { Require(board.IsPresenting, "A second presentation cannot replace the active sequence"); }
        board.CancelPresentation();
        Require(!await first, "Explicit cancellation resolves the active presentation");
        var second = board.PresentTransitionAsync(transition);
        RemoveChild(board);
        Require(!await second && !board.IsPresenting, "ExitTree cancels without waiting for another process frame");
        AddChild(board);
        board.Configure(new(assets, EnvironmentCatalog.Load(), new Empire.Core.Geometry.Square4Grid(), new Empire.Core.State.ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")), BoardPlane.Dimetric), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        board.ConfigureDynamicState();
        var reattached = board.PresentTransitionAsync(transition);
        Require(board.IsPresenting && !reattached.IsCompleted, "Explicit reattach and reconfigure restores the presentation lifecycle");
        await Tick(board, 0.221);
        Require(await reattached, "Reattached board can settle a new transition normally");
        Require(initial.ToJsonString() == initialBytes,
            "All presentation tests leave original canonical fixture data unchanged");
    }

    private BattlePresentationTransition Transition(JsonObject before, JsonObject after, JsonObject action, IReadOnlyList<JsonObject> events,
        IReadOnlySet<int>? visible = null) =>
        new(++revision, EcmaJson.ToElement(before), EcmaJson.ToElement(action), events.Select(EcmaJson.ToElement).ToArray(), EcmaJson.ToElement(after), false, visible, events.Select(_ => "Localized announcement").ToArray());
    private async Task Tick(BoardView board, double seconds)
    {
        board.AdvancePresentation(seconds);
        await Task.Yield();
    }
    private async Task Drain(BoardView board, Task<bool> task)
    {
        for (var index = 0; !task.IsCompleted && index < 20; index++)
            await Tick(board, 1);
        Require(task.IsCompleted && await task, "Playback must reach its final state under a bounded deterministic clock");
    }
    private JsonObject Copy() => (JsonObject)initial.DeepClone();
    private static JsonObject At(int x, int y) => new() { ["x"] = x, ["y"] = y };
    private static JsonObject IdleAction() => new() { ["kind"] = "endTurn" };
    private static JsonObject Strike(string kind, int attacker, int defender) => new()
    {
        ["type"] = kind,
        ["attacker"] = attacker,
        ["defender"] = defender,
        ["weapon"] = "c01.border-blade",
        ["damage"] = 12,
        ["killed"] = false
    };
    private static int Int(JsonObject value, string key) => value[key]!.GetValue<int>();
    private static Node2D Actor(BoardView board, int id) => board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}");
    private static int Frame(Node2D actor) => actor.GetNode<Sprite2D>("AuthoredSprite").GetMeta("frame").AsInt32();
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private void Throws(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException) { assertions++; return; }
        throw new InvalidOperationException(message);
    }
}
