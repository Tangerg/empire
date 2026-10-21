#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Presentation;

/// <summary>Focused domain checks for the painted sample, not a campaign or GUI playthrough.</summary>
public static class PaintedInteractionChecks
{
    public static void Run(Action<bool, string> require)
    {
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        packs.Add(PresentationJson.Load<JsonObject>("res://data/samples/three-bridges-content.json"));
        var engine = new BattleEngine(packs);
        var level = PresentationJson.Load<JsonObject>("res://data/samples/three-bridges-level.json");
        CheckFootRoutes(engine, engine.CreateState(level), require);
        CheckBarricade(engine, engine.CreateBattle(level), require);
        CheckAidPost(engine, level, require);
    }

    private static void CheckFootRoutes(BattleEngine engine, JsonObject state, Action<bool, string> require)
    {
        var before = state.ToJsonString();
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var battlefield = movement.Battlefield(state);
        var board = engine.Space.Board(state);
        var tiles = state["map"]!["tiles"]!.AsArray();
        var foot = state["units"]!.AsArray().OfType<JsonObject>()
            .First(unit => unit["key"]!.GetValue<string>() == "bridges-sword");
        require(engine.Catalog.Definition("units", foot["type"]!.GetValue<string>())!.Value
            .GetProperty("movementClass").GetString() == "foot", "Bridge route probes must use the sample's actual foot unit.");
        var remaining = tiles.Select((tile, index) => (tile, index))
            .Where(item => item.tile!.GetValue<string>() == "bridge")
            .Select(item => board.CoordOf(item.index)).ToHashSet();
        var bridges = 0;
        while (remaining.Count > 0)
        {
            bridges++;
            var start = remaining.OrderBy(at => at.X).ThenBy(at => at.Y).First();
            remaining.Remove(start);
            var pending = new Queue<Coord>();
            pending.Enqueue(start);
            var banks = new HashSet<Coord>();
            while (pending.TryDequeue(out var at))
            {
                // Only the detached query actor moves; the canonical snapshot is never changed.
                var probe = (JsonObject)foot.DeepClone();
                probe["x"] = at.X;
                probe["y"] = at.Y;
                var field = engine.Space.ComputeMoveField(state, probe);
                require(battlefield.Cell(at).Admits("foot"), $"Bridge {bridges} must admit foot at {at}.");
                foreach (var next in board.Neighbours(at))
                {
                    var terrain = tiles[board.IndexOf(next)]!.GetValue<string>();
                    if (terrain is not ("bridge" or "road"))
                        continue;
                    require(engine.Space.PathTo(field, state, next) is not null,
                        $"Bridge {bridges} must connect {at} to its {terrain} neighbour {next} through real movement rules.");
                    if (terrain == "road")
                        banks.Add(next);
                    else if (remaining.Remove(next))
                        pending.Enqueue(next);
                }
            }
            require(banks.Count >= 2 && banks.Min(at => at.X) < banks.Max(at => at.X),
                $"Bridge {bridges} must join road approaches on both banks, not end in water or outside.");
        }
        require(bridges == 3, "The painted sample must have exactly three connected bridge footprints.");
        foreach (var tag in new[] { "water", "outside" })
        {
            var denied = tiles.Select((_, index) => board.CoordOf(index)).First(at =>
                battlefield.Cell(at).Terrain.GetProperty("tags").EnumerateArray().Any(value => value.GetString() == tag)
                && board.Neighbours(at).Any(next => battlefield.Cell(next).Admits("foot")));
            var from = board.Neighbours(denied).First(at => battlefield.Cell(at).Admits("foot"));
            var probe = (JsonObject)foot.DeepClone();
            probe["x"] = from.X;
            probe["y"] = from.Y;
            require(!battlefield.Cell(denied).Admits("foot")
                && engine.Space.PathTo(engine.Space.ComputeMoveField(state, probe), state, denied) is null,
                $"Canonical {tag} at {denied} must deny foot movement even from an adjacent traversable cell.");
        }
        require(state.ToJsonString() == before, "Foot route queries must leave the canonical sample state unchanged.");
    }

    private static void CheckBarricade(BattleEngine engine, Battle battle, Action<bool, string> require)
    {
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var initial = battle.Snapshot();
        var structure = initial["structures"]!.AsArray().OfType<JsonObject>().Single();
        var target = TacticalQueries.Position(structure);
        var structureId = structure["id"]!.GetValue<string>();
        var attackerId = initial["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["key"]!.GetValue<string>() == "bridges-enemy-sword")["id"]!.GetValue<int>();
        battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        var state = battle.Snapshot();
        var attacker = TacticalQueries.RequireUnit(state, attackerId);
        require(state["currentPlayer"]!.GetValue<int>() == attacker["owner"]!.GetValue<int>(),
            "The real turn handoff must activate the sample swordsman's owner.");
        var blockedHash = battle.StateHash;
        var denied = false;
        try
        {
            battle.Dispatch(Command(attackerId, [TacticalQueries.Position(attacker), target], "wait"));
        }
        catch (IllegalActionException error) when (error.Refusal.Code == "tactics.target_unreachable") { denied = true; }
        require(denied && battle.StateHash == blockedHash, "A live barricade must reject actual movement atomically.");

        var attacks = 0;
        var sawDamagedBarrier = false;
        while (structure["hp"]!.GetValue<double>() > 0 && attacks < 6)
        {
            state = battle.Snapshot();
            attacker = TacticalQueries.RequireUnit(state, attackerId);
            var cell = movement.Battlefield(state).Cell(target);
            require(cell.BlocksMovement && cell.Cover == "half", "A barricade with positive HP must still block and provide half-cover.");
            var field = engine.Space.ComputeMoveField(state, attacker);
            require(engine.Space.PathTo(field, state, target) is null, "Positive barricade HP must keep its cell out of the movement field.");
            var attackAt = engine.Space.Board(state).Neighbours(target).First(at =>
                field.Stops.Contains(engine.Space.Board(state).IndexOf(at))
                && engine.CommandsAt(state, attacker, at).Any(option => option.Ability == "attack" && option.Targets.Contains(target)));
            var weapon = engine.CommandsAt(state, attacker, attackAt)
                .First(option => option.Ability == "attack" && option.Targets.Contains(target)).Weapon;
            var path = engine.Space.PathTo(field, state, attackAt)
                ?? throw new InvalidOperationException("Barricade attack approach must have a canonical path.");
            var hpBefore = structure["hp"]!.GetValue<double>();
            var receipt = battle.Dispatch(Command(attackerId, path, "attack", target, weapon));
            attacks++;
            structure = battle.Snapshot()["structures"]!.AsArray().OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == structureId);
            var hpAfter = structure["hp"]!.GetValue<double>();
            require(hpAfter < hpBefore && receipt.Events.Any(value => value["type"]!.GetValue<string>() == "structureDamaged"),
                "Dispatched weapon attacks must damage the canonical barricade and emit the structure transition.");
            if (hpAfter > 0)
            {
                sawDamagedBarrier = true;
                battle = CheckSave(engine, battle, require, "damaged barricade");
            }
            else
                require(receipt.Events.Any(value => value["type"]!.GetValue<string>() == "structureDestroyed"),
                    "The lethal barricade attack must emit structureDestroyed.");
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        }
        require(sawDamagedBarrier && structure["hp"]!.GetValue<double>() == 0 && structure["disabled"]!.GetValue<bool>(),
            "Real attacks must exercise damaged-positive HP, then destroy and disable the retained barricade within six attacks.");
        battle = CheckSave(engine, battle, require, "destroyed barricade");
        state = battle.Snapshot();
        var cleared = movement.Battlefield(state).Cell(target);
        require(!cleared.BlocksMovement && cleared.Cover == "none", "Zero-HP barricades must release movement and half-cover after load.");
        attacker = TacticalQueries.RequireUnit(state, attackerId);
        var clearedPath = engine.Space.PathTo(engine.Space.ComputeMoveField(state, attacker), state, target)
            ?? throw new InvalidOperationException("Destroyed barricade cell must become reachable through real movement rules.");
        var move = battle.Dispatch(Command(attackerId, clearedPath, "wait"));
        require(TacticalQueries.Position(TacticalQueries.RequireUnit(battle.Snapshot(), attackerId)) == target
            && move.Events.Any(value => value["type"]!.GetValue<string>() == "move"),
            "A real post-load movement command must enter the destroyed barricade cell.");
    }

    private static void CheckAidPost(BattleEngine engine, JsonObject canonicalLevel, Action<bool, string> require)
    {
        var canonical = engine.CreateState(canonicalLevel);
        var board = engine.Space.Board(canonical);
        var index = canonical["map"]!["tiles"]!.AsArray().Select((tile, i) => (tile, i))
            .First(item => item.tile!.GetValue<string>() == "three-bridges.aid-post").i;
        var at = board.CoordOf(index);
        // This isolated level seed tests injured capture/regen without changing the production sample or live state.
        var fixture = (JsonObject)canonicalLevel.DeepClone();
        var seed = fixture["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["key"]!.GetValue<string>() == "bridges-sword");
        var maximum = engine.Catalog.Definition("units", seed["unit"]!.GetValue<string>())!.Value.GetProperty("maxHp").GetDouble();
        seed["x"] = at.X;
        seed["y"] = at.Y;
        seed["hp"] = maximum - 30;
        var battle = engine.CreateBattle(fixture);
        var state = battle.Snapshot();
        var unit = TacticalQueries.UnitAt(state, at)!;
        var unitId = unit["id"]!.GetValue<int>();
        var injuredHp = unit["hp"]!.GetValue<double>();
        require(injuredHp == maximum - 30 && state["phase"]!.GetValue<string>() == "playing"
            && TacticalQueries.UnitsOf(state, 2).Count > 0, "The aid-post fixture must start genuinely injured, with its opponent alive.");
        require(engine.Support.CanCapture(state, unit, at), "The canonical neutral aid-post must offer the real capture rule.");
        var first = battle.Dispatch(Command(unitId, [at], "capture"));
        state = battle.Snapshot();
        require(first.Events.Any(value => value["type"]!.GetValue<string>() == "capture" && !value["captured"]!.GetValue<bool>())
            && state["map"]!["captureProgress"]![index]!.GetValue<double>() > 0
            && state["map"]!["owners"]![index]!.GetValue<int>() == 0,
            "Injured capture must create real partial progress without prematurely assigning aid-post ownership.");
        battle = CheckSave(engine, battle, require, "partial aid-post capture");
        battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        require(TacticalQueries.RequireUnit(battle.Snapshot(), unitId)["hp"]!.GetValue<double>() == injuredHp,
            "An unowned aid-post must not heal the injured unit at its next owner turn.");
        var capture = battle.Dispatch(Command(unitId, [at], "capture"));
        state = battle.Snapshot();
        require(capture.Events.Any(value => value["type"]!.GetValue<string>() == "capture" && value["captured"]!.GetValue<bool>())
            && state["map"]!["owners"]![index]!.GetValue<int>() == 1
            && state["map"]!["captureProgress"]![index]!.GetValue<double>() == 0,
            "The second real capture must assign ownership and clear progressive capture progress.");
        require(TacticalQueries.RequireUnit(state, unitId)["hp"]!.GetValue<double>() == injuredHp,
            "Capturing the aid-post must not heal immediately.");
        var opposingTurn = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        require(TacticalQueries.RequireUnit(battle.Snapshot(), unitId)["hp"]!.GetValue<double>() == injuredHp
            && !opposingTurn.Events.Any(value => value["type"]!.GetValue<string>() == "regen"),
            "The opposing turn must not trigger the owner's aid-post healing.");
        var ownerTurn = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        require(TacticalQueries.RequireUnit(battle.Snapshot(), unitId)["hp"]!.GetValue<double>() == injuredHp + 15
            && ownerTurn.Events.Any(value => value["type"]!.GetValue<string>() == "regen"
                && value["unit"]!.GetValue<int>() == unitId && value["amount"]!.GetValue<double>() == 15),
            "The next owner turn must heal exactly 15 HP and emit the canonical regen event.");
        CheckSave(engine, battle, require, "captured aid-post and healed unit");
    }

    private static JsonObject Command(int unitId, IEnumerable<Coord> path, string ability, Coord? target = null, string? weapon = null)
    {
        var command = new JsonObject { ["ability"] = ability };
        if (target is { } at)
            command["target"] = TacticalQueries.CoordinateJson(at);
        if (weapon is not null)
            command["weapon"] = weapon;
        return new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = unitId,
            ["path"] = TacticalQueries.PathJson(path),
            ["command"] = command
        };
    }

    private static Battle CheckSave(BattleEngine engine, Battle battle, Action<bool, string> require, string stage)
    {
        var state = battle.Snapshot();
        var saved = engine.SaveBattleJson(state, "2026-10-21T00:00:00.000Z");
        var restored = engine.LoadSavedBattle(JsonNode.Parse(saved));
        require(restored.StateHash == battle.StateHash && JsonNode.DeepEquals(restored.Snapshot(), state),
            $"Full JSON save/load must preserve every state field and digest for {stage}.");
        var continuation = engine.LoadSavedBattle(JsonNode.Parse(saved));
        var action = new JsonObject { ["kind"] = "endTurn" };
        var expected = battle.Dispatch(action);
        var actual = continuation.Dispatch(action);
        require(actual.StateHash == expected.StateHash && actual.Events.Count == expected.Events.Count
            && actual.Events.Zip(expected.Events).All(pair => JsonNode.DeepEquals(pair.First, pair.Second)),
            $"Save/load must preserve subsequent real turn events and state for {stage}.");
        return restored;
    }
}
