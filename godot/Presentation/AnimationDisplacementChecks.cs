#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
    private async Task CheckRecordedDisplacement()
    {
        var fixture = PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json")[0]!.AsObject();
        var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(recordedLevel);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Displacement witness starts from the unchanged formal level.");
        var checkpoint = fixture["checkpoints"]![0]!.AsObject();
        var index = checkpoint["index"]!.GetValue<int>();
        foreach (var action in fixture["actions"]!.AsArray().Take(index).OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), "The original legal prefix reaches the exact recorded charge.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                recordedLevel, AuthoredScene.Load("res://data/scenes/c01-06.json"));
            Require(!session.IsComputerTurn && session.Dispatch(EcmaJson.ToElement(fixture["actions"]![index]!), session.Revision).Accepted,
                "The real human session accepts the unchanged recorded movement-and-charge order.");
            screen.Refresh();
            Require(StateDigest.Hash(session.Snapshot()) == checkpoint["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                    == checkpoint["eventsSha256"]!.GetValue<string>(), "Native feedback cannot change the original state or ordered receipt.");
            var board = screen.Board;
            for (var step = 0; board.GetNodeOrNull<Node2D>("BattleEffects/Hit") is null && step < 100; step++)
                await Tick(board, 0.02);
            var hit = board.GetNode<Node2D>("BattleEffects/Hit");
            var contact = board.Projection.CellContact(new(21, 27));
            GD.Print($"CHARGE_IMPACT actual={hit.Position}; target={Actor(board, 14).Position}; expected={contact}");
            Require(hit.Position.IsEqualApprox(contact) && Actor(board, 14).Position.IsEqualApprox(contact),
                "A committed charge must hit the actor at its pre-displacement position before the ordered forcedMove event.");
            for (var step = 0; screen.IsPresenting && step < 100; step++)
                await Tick(board, 0.1);
            Require(!screen.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                "The real-session charge must finish without residual effects.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
        await CheckRecordedCombatTimelines();
    }

    private async Task CheckRecordedCombatTimelines()
    {
        foreach (var fixture in PresentationJson.Load<JsonArray>("res://tests/native/recorded-displacement.json").OfType<JsonObject>())
        {
            var levelId = fixture["levelId"]!.GetValue<string>();
            var recordedLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == levelId);
            var engine = new BattleEngine(packs);
            var battle = engine.CreateBattle(recordedLevel);
            Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Each combat timeline starts from the unchanged formal level.");
            var checkpoints = fixture["checkpoints"]!.AsArray().OfType<JsonObject>().ToDictionary(value => Int(value, "index"));
            var actions = fixture["actions"]!.AsArray().OfType<JsonObject>().ToArray();
            for (var index = 0; index < actions.Length; index++)
            {
                var receipt = battle.Dispatch(actions[index]);
                if (!checkpoints.TryGetValue(index, out var checkpoint))
                    continue;
                Require(StateDigest.Hash(receipt.Before) == checkpoint["beforeStateHash"]!.GetValue<string>()
                    && battle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>()
                    && EcmaJson.Sha256(new JsonArray(receipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                        == checkpoint["eventsSha256"]!.GetValue<string>(), "Every displacement case retains its exact legal command receipt and final state.");
                var board = new BoardView { AdvanceAnimationsAutomatically = false };
                AddChild(board);
                try
                {
                    board.Configure(new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric),
                        recordedLevel, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
                    board.ConfigureDynamicState();
                    var after = battle.Snapshot();
                    var original = after.ToJsonString();
                    var transition = Transition(receipt.Before, after, actions[index], receipt.Events);
                    await CheckOrderedCombat(board, transition, $"{levelId}:{index}");
                    Require(after.ToJsonString() == original && battle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>(),
                        "Visual contact, damage numbers and death ghosts never advance canonical battle state.");
                    if (levelId == "c01-06" && index == 5)
                        await CheckDisplacementBoundaries(board, transition);
                }
                finally
                {
                    RemoveChild(board);
                    board.Free();
                }
            }
        }
    }

    private async Task CheckOrderedCombat(BoardView board, BattlePresentationTransition transition, string witness)
    {
        board.RenderState(EcmaJson.Parse(transition.Before.GetRawText())!.AsObject());
        var positions = transition.Before.GetProperty("units").EnumerateArray().ToDictionary(value => value.GetProperty("id").GetInt32(),
            value => new Vector2I(value.GetProperty("x").GetInt32(), value.GetProperty("y").GetInt32()));
        if (transition.Action.TryGetProperty("path", out var path))
        {
            var destination = path.EnumerateArray().Last();
            positions[transition.Action.GetProperty("unit").GetInt32()] = new(destination.GetProperty("x").GetInt32(), destination.GetProperty("y").GetInt32());
        }
        var expected = new Dictionary<int, (int Unit, Vector2 At, string Number)>();
        var moving = new HashSet<int>();
        for (var index = 0; index < transition.Events.Count; index++)
        {
            var item = transition.Events[index];
            var kind = item.GetProperty("type").GetString();
            if (kind == "forcedMove")
            {
                var id = item.GetProperty("unit").GetInt32();
                var from = item.GetProperty("from");
                var to = item.GetProperty("to");
                Require(positions[id] == new Vector2I(from.GetProperty("x").GetInt32(), from.GetProperty("y").GetInt32()),
                    "The event's displacement origin agrees with preceding committed positions.");
                positions[id] = new(to.GetProperty("x").GetInt32(), to.GetProperty("y").GetInt32());
                if (item.GetProperty("path").GetArrayLength() > 1 && item.GetProperty("mode").GetString() != "teleport")
                    moving.Add(index);
            }
            else if (kind is "attack" or "counter" or "supportAttack" or "areaAttack" or "partingShot" or "collisionDamage" or "heal" or "regen")
            {
                var healing = kind is "heal" or "regen";
                var id = item.GetProperty(kind is "collisionDamage" or "regen" ? "unit" : kind == "heal" ? "target" : "defender").GetInt32();
                var at = positions[id];
                if (kind == "partingShot")
                    at = new(item.GetProperty("at").GetProperty("x").GetInt32(), item.GetProperty("at").GetProperty("y").GetInt32());
                expected.Add(index, (id, board.Projection.CellCenter(new(at.X, at.Y)),
                    (healing ? "+" : "-") + item.GetProperty(kind == "collisionDamage" || healing ? "amount" : "damage").GetDouble().ToString(CultureInfo.InvariantCulture)));
            }
        }
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        var impacts = new HashSet<ulong>();
        var observed = new List<int>();
        var sliding = new HashSet<int>();
        var checkedDepths = new HashSet<int>();
        try
        {
            var task = board.PresentTransitionAsync(transition);
            for (var step = 0; !task.IsCompleted && step < 1000; step++)
            {
                var eventIndex = completed.Count;
                if ((board.GetNodeOrNull<Node2D>("BattleEffects/Hit") ?? board.GetNodeOrNull<Node2D>("BattleEffects/Heal")) is { } hit
                    && impacts.Add(hit.GetInstanceId()))
                {
                    Require(expected.TryGetValue(eventIndex, out var impact), "Only the currently ordered damage event can own a hit effect.");
                    Require(hit.Position.IsEqualApprox(impact.At) && hit.GetNode<Label>("Number").Text == impact.Number,
                        $"{witness} event {eventIndex}: impact must use its actual amount and the actor's position at that point in the receipt.");
                    Require(Actor(board, impact.Unit).Position.IsEqualApprox(impact.At), "A hit lands on the displayed recipient, including a moved death ghost.");
                    observed.Add(eventIndex);
                    CheckActorDepthCuts(board);
                }
                if (moving.Contains(eventIndex))
                {
                    if (checkedDepths.Add(eventIndex))
                        CheckActorDepthCuts(board);
                    var item = transition.Events[eventIndex];
                    var actor = Actor(board, item.GetProperty("unit").GetInt32());
                    var from = item.GetProperty("from");
                    var to = item.GetProperty("to");
                    var start = board.Projection.LogicalCellContact(new(from.GetProperty("x").GetInt32(), from.GetProperty("y").GetInt32()));
                    var end = board.Projection.LogicalCellContact(new(to.GetProperty("x").GetInt32(), to.GetProperty("y").GetInt32()));
                    var contact = actor.GetMeta("logical_contact").AsVector2();
                    if (!contact.IsEqualApprox(start) && !contact.IsEqualApprox(end))
                    {
                        Require(actor.Position.IsEqualApprox(board.Projection.Project(contact)) && Frame(actor) == 0,
                            "Forced displacement slides the existing idle actor and shadow along the projected ground, without a walk pose or jump.");
                        sliding.Add(eventIndex);
                    }
                }
                await Tick(board, 0.03);
            }
            Require(task.IsCompleted && await task && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                "Ordered combat playback finishes under a bounded clock and releases all effects.");
            Require(completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count)) && observed.SequenceEqual(expected.Keys)
                && sliding.SetEquals(moving), "Each impact and displacement is presented once, in receipt order; no slide may become a teleport.");
            GD.Print($"COMBAT_TIMELINE {witness}: impacts=[{string.Join(",", observed)}]; slides=[{string.Join(",", sliding)}]; exact ordered receipt preserved.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckDisplacementBoundaries(BoardView board, BattlePresentationTransition transition)
    {
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            var forced = transition.Events.Select((value, index) => (value, index)).Single(pair => pair.value.GetProperty("type").ValueEquals("forcedMove"));
            var id = forced.value.GetProperty("unit").GetInt32();
            var to = forced.value.GetProperty("to");
            var final = board.Projection.CellContact(new(to.GetProperty("x").GetInt32(), to.GetProperty("y").GetInt32()));
            var task = board.PresentTransitionAsync(transition);
            for (var step = 0; completed.Count < forced.index && step < 1000; step++)
                await Tick(board, 0.01);
            await Tick(board, 0.05);
            Require(completed.Count == forced.index && !Actor(board, id).Position.IsEqualApprox(final),
                "Cancellation witness must interrupt an actual unfinished displacement, not its settled result.");
            board.CancelPresentation();
            Require(!await task && Actor(board, id).Position.IsEqualApprox(final) && board.GetNodeOrNull("BattleEffects") is null
                && !completed.Contains(forced.index), "Cancellation settles committed After without announcing an unfinished displacement.");
            completed.Clear();
            board.AnimationsEnabled = false;
            task = board.PresentTransitionAsync(transition);
            Require(task.IsCompleted && await task && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count))
                && Actor(board, id).Position.IsEqualApprox(final) && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion retains the exact event sequence and final positions with no outstanding effect.");
            board.AnimationsEnabled = true;
        }
        finally { board.EventPresented -= Presented; }
        await CheckAnnouncementAndRecruitBoundaries(board, transition);
        await CheckSequentialDisplacementDepth(board, transition);
        await CheckDisplacementVisibility(board, transition);
    }

    private void CheckActorDepthCuts(BoardView board)
    {
        foreach (var actor in board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>().Where(node => node.HasMeta("unit_id")))
        {
            var front = new SpriteStandingVisual(actor.GetNode<Sprite2D>("AuthoredSprite")).FrontOffset(actor).Y;
            var cut = (int)Math.Floor(board.Projection.Depth(actor.GetMeta("logical_contact").AsVector2()) + front) + 1;
            Require(board.Projection.DepthPartitions.Any(partition => partition.StartDepth == cut || partition.EndDepthExclusive == cut),
                "Every current actor contact retains its terrain depth cut after another actor's movement or teleport.");
        }
    }

    private async Task CheckAnnouncementAndRecruitBoundaries(BoardView board, BattlePresentationTransition original)
    {
        var before = EcmaJson.Parse(original.Before.GetRawText())!.AsObject();
        var after = EcmaJson.Parse(original.After.GetRawText())!.AsObject();
        var action = EcmaJson.Parse(original.Action.GetRawText())!.AsObject();
        var events = original.Events.Select(value => EcmaJson.Parse(value.GetRawText())!.AsObject()).ToArray();
        var turn = new JsonObject { ["type"] = "turnStart", ["player"] = 1, ["turn"] = 2 };
        await CheckOrderedCombat(board, Transition(before, after, action, new[] { turn }.Concat(events).ToArray()), "projection:turn-before-displacement");
        var recruit = (JsonObject)after["units"]![0]!.DeepClone();
        recruit["id"] = 1000;
        recruit.Remove("key");
        recruit["x"] = 3;
        recruit["y"] = 3;
        after["units"]!.AsArray().Add(recruit);
        var arrival = new JsonObject { ["type"] = "recruit", ["unit"] = 1000, ["at"] = At(3, 3) };
        await CheckOrderedCombat(board, Transition(before, after, action, new[] { arrival }.Concat(events).ToArray()), "projection:recruit-before-displacement");
        after["units"]!.AsArray().Remove(recruit);
        var forced = events.Single(item => item["type"]!.GetValue<string>() == "forcedMove");
        var id = Int(forced, "unit");
        await CheckOrderedCombat(board, Transition(before, after, IdleAction(), new[]
        {
            turn, new JsonObject { ["type"] = "regen", ["unit"] = id, ["amount"] = 1 }, forced,
            new JsonObject { ["type"] = "heal", ["source"] = id, ["target"] = id, ["amount"] = 2 }
        }), "projection:recovery-around-displacement");
    }

    private async Task CheckSequentialDisplacementDepth(BoardView board, BattlePresentationTransition original)
    {
        var before = EcmaJson.Parse(original.Before.GetRawText())!.AsObject();
        board.RenderState(before);
        var first = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == 12);
        var second = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == 14);
        var actor = Actor(board, 12);
        var front = new SpriteStandingVisual(actor.GetNode<Sprite2D>("AuthoredSprite")).FrontOffset(actor).Y;
        var cells = board.Projection.Triangles.Select(triangle => triangle.Cell).Distinct().ToHashSet();
        var secondDepth = board.Projection.Depth(board.Projection.LogicalCellContact(new(Int(second, "x"), Int(second, "y"))));
        var candidates = cells.Where(cell => cells.Contains(new(cell.X, cell.Y - 1)))
            .Where(cell => Math.Abs(board.Projection.Depth(board.Projection.LogicalCellContact(cell)) - secondDepth) > 50)
            .Where(cell => !board.Projection.DepthPartitions.Any(partition => partition.StartDepth ==
                (int)Math.Floor(board.Projection.Depth(board.Projection.LogicalCellContact(cell)) + front) + 1))
            .OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        Require(candidates.Length >= 2, "Depth regression must use contacts whose cuts are genuinely absent, not coincident with existing scenery.");
        var destination = candidates[0];
        var teleport = candidates.First(cell => board.Projection.Depth(board.Projection.LogicalCellContact(cell))
            > board.Projection.Depth(board.Projection.LogicalCellContact(destination)) + 30);
        first["x"] = destination.X;
        first["y"] = destination.Y - 1;
        var after = (JsonObject)before.DeepClone();
        var afterFirst = after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == 12);
        var afterSecond = after["units"]!.AsArray().OfType<JsonObject>().Single(unit => Int(unit, "id") == 14);
        afterFirst["x"] = teleport.X;
        afterFirst["y"] = teleport.Y;
        afterSecond["x"] = Int(second, "x") + 1;
        JsonObject Displace(int id, string mode, JsonObject from, JsonObject to) => new()
        {
            ["type"] = "forcedMove",
            ["unit"] = id,
            ["mode"] = mode,
            ["from"] = from.DeepClone(),
            ["to"] = to.DeepClone(),
            ["path"] = new JsonArray(from.DeepClone(), to.DeepClone()),
            ["collided"] = false
        };
        var sequence = new[]
        {
            Displace(12, "push", At(Int(first, "x"), Int(first, "y")), At(destination.X, destination.Y)),
            Displace(14, "pull", At(Int(second, "x"), Int(second, "y")), At(Int(afterSecond, "x"), Int(afterSecond, "y"))),
            Displace(12, "teleport", At(destination.X, destination.Y), At(teleport.X, teleport.Y)), Strike("attack", 14, 12)
        };
        await CheckOrderedCombat(board, Transition(before, after, IdleAction(), sequence), "projection:two-slides-teleport-and-hit");
    }

    private async Task CheckDisplacementVisibility(BoardView board, BattlePresentationTransition original)
    {
        var before = EcmaJson.Parse(original.Before.GetRawText())!.AsObject();
        var after = EcmaJson.Parse(original.After.GetRawText())!.AsObject();
        var forced = original.Events.Select(value => EcmaJson.Parse(value.GetRawText())!.AsObject())
            .Single(item => item["type"]!.GetValue<string>() == "forcedMove");
        var id = Int(forced, "unit");
        var collision = new JsonObject { ["type"] = "collisionDamage", ["unit"] = id, ["amount"] = 1, ["hpAfter"] = 1, ["killed"] = false };
        var visible = new HashSet<int> { id };
        var hidden = new HashSet<int>();
        foreach (var (beforeVisible, afterVisible, expected) in new (IReadOnlySet<int>?, IReadOnlySet<int>?, bool)[]
        {
            (visible, visible, true), (visible, hidden, false), (hidden, visible, false),
            (hidden, hidden, false), (null, hidden, false), (hidden, null, false)
        })
        {
            var transition = Transition(before, after, IdleAction(), new[] { forced, collision }, beforeVisible);
            var task = board.PresentTransitionAsync(transition, afterVisible);
            var effects = new HashSet<ulong>();
            for (var step = 0; !task.IsCompleted && step < 100; step++)
            {
                if (board.GetNodeOrNull<Node2D>("BattleEffects/Hit") is { } hit)
                    effects.Add(hit.GetInstanceId());
                await Tick(board, 0.03);
            }
            Require(task.IsCompleted && await task && effects.Count == (expected ? 1 : 0),
                "New displacement/collision feedback cannot disclose a unit hidden at either boundary; null and empty remain different.");
        }
        var recruit = (JsonObject)after["units"]![0]!.DeepClone();
        recruit["id"] = 1000;
        recruit.Remove("key");
        recruit["x"] = 3;
        recruit["y"] = 4;
        after["units"]!.AsArray().Add(recruit);
        var events = new[]
        {
            new JsonObject { ["type"] = "recruit", ["unit"] = 1000, ["at"] = At(3, 3) },
            new JsonObject { ["type"] = "forcedMove", ["unit"] = 1000, ["mode"] = "push", ["from"] = At(3, 3),
                ["to"] = At(3, 4), ["path"] = new JsonArray(At(3, 3), At(3, 4)), ["collided"] = true },
            new JsonObject { ["type"] = "collisionDamage", ["unit"] = 1000, ["amount"] = 1, ["hpAfter"] = 1, ["killed"] = false }
        };
        var beforeVisibleIds = before["units"]!.AsArray().OfType<JsonObject>().Select(unit => Int(unit, "id")).ToHashSet();
        var afterVisibleIds = new HashSet<int>(beforeVisibleIds) { 1000 };
        var observedBoundary = false;
        void Presented(BattlePresentationTransition _, int index)
        {
            if (index != 1)
                return;
            observedBoundary = true;
            Require(Actor(board, 1000).Position.IsEqualApprox(board.Projection.CellContact(new(3, 3))),
                "A newly visible recruit is born at its event location; conservative two-boundary policy does not replay its later hidden-origin displacement.");
        }
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(Transition(before, after, IdleAction(), events, beforeVisibleIds), afterVisibleIds);
            await Drain(board, task);
            Require(observedBoundary && Actor(board, 1000).Position.IsEqualApprox(board.Projection.CellContact(new(3, 4))),
                "Final settlement still projects the recruit's committed destination exactly once.");
        }
        finally { board.EventPresented -= Presented; }
    }
}
