using System.Diagnostics;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

internal static class CombatPreviewChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var engine = new BattleEngine(Read(Path.Combine(data, "content-packs.json")).AsArray());
        var levels = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>().ToArray();
        ShippedOpenings(engine, levels);
        SpatialRelationships(engine);
        FailedPreviewIsolation(engine);
    }

    private static void ShippedOpenings(BattleEngine engine, IReadOnlyList<JsonObject> levels)
    {
        var timer = Stopwatch.StartNew();
        var unitAttacks = 0;
        var structureAttacks = 0;
        var collisions = 0;
        Require(levels.Count == 16, "The preview sweep covers all sixteen shipped campaign openings.");
        foreach (var level in levels)
        {
            var authored = EcmaJson.Stringify(level);
            var battle = engine.CreateBattle(level);
            var state = battle.Snapshot();
            Require(Text(state, "phase") == "playing" && Integer(state, "currentPlayer") == 1,
                $"{Text(level, "id")}: use its real human opening without changing controllers, deployment or rules.");
            foreach (var actor in engine.Turns.Actors(state))
            {
                var field = engine.Space.ComputeMoveField(state, actor);
                foreach (var index in field.Stops)
                {
                    var at = engine.Space.Board(state).CoordOf(index);
                    foreach (var option in engine.CommandsAt(state, actor, at).Where(option => option.Ability == "attack" && option.Weapon is not null))
                        foreach (var target in option.Targets)
                        {
                            var context = $"{Text(level, "id")}/{Text(actor, "key")} {TacticalQueries.Position(actor)} -> {at} -> {target}/{option.Weapon}";
                            var weapon = engine.Catalog.Definition("weapons", option.Weapon!)!.Value;
                            Require(weapon.GetProperty("castTurns").GetDouble() == 0,
                                context + ": delayed casts need a later-resolution oracle, not an immediate damage comparison.");
                            var result = VerifyCommand(engine, battle, state, Integer(actor, "id"), at, target, option.Weapon!, context);
                            if (result.Plan.PrimaryUnit is not null)
                                unitAttacks++;
                            else
                                structureAttacks++;
                            collisions += result.Receipt.Events.Count(value => Text(value, "type") == "collisionDamage");
                        }
                }
            }
            Require(EcmaJson.Stringify(level) == authored, "Opening checks must not rewrite the authored level.");
        }
        Require(unitAttacks == 197, $"The unchanged campaign exposes 197 opening unit attacks; checked {unitAttacks}.");
        Console.WriteLine($"  Unchanged campaign: 16 levels, {unitAttacks} unit attacks, {structureAttacks} structure attacks; receipts and final HP verified in {timer.Elapsed.TotalSeconds:F3}s.");
        Console.WriteLine($"  Scope: no delayed casts or parting shots; {collisions} separately emitted collision effects accounted for after the forecast exchange.");
    }

    private static void SpatialRelationships(BattleEngine engine)
    {
        // Synthetic, flat, no-ZOC fixtures isolate placement. All units, weapons, formations and actions are existing content.
        foreach (var entering in new[] { true, false })
        {
            var origin = entering ? new Coord(1, 2) : new Coord(3, 2);
            var destination = entering ? new Coord(3, 2) : new Coord(1, 2);
            var target = entering ? new Coord(4, 2) : new Coord(0, 2);
            var actor = Unit("actor", "c01.laiya", 1, origin);
            actor["formation"] = "formation-defensive";
            actor["commander"] = "allied-command";
            var level = Fixture("self-formation-aura-" + entering, actor,
                Unit("leader", "c01.swordsman", 1, new(3, 3)), Unit("enemy", "c01.swordsman", 2, target));
            level["commanders"] = new JsonArray(Commander("leader"));
            var battle = engine.CreateBattle(level);
            var state = battle.Snapshot();
            var plan = VerifyCommand(engine, battle, state, Id(state, "actor"), destination, target, "c01.border-blade", Text(level, "id")).Plan;
            var exchange = plan.PrimaryUnit!;
            Require(exchange.Strike.Modifiers.Any(value => value.Kind == "core.formation-attack") == entering,
                "Entering/leaving the only adjacent ally changes the actor's formation at the attack cell.");
            Require(exchange.Strike.FactorOf("commander.attack") == (entering ? 1.25 : 1),
                "Entering/leaving the command radius changes attack aura at the same projected cell.");
        }

        var leaderActor = Unit("actor", "c01.laiya", 1, new(1, 2));
        var supporter = Unit("supporter", "c01.templar", 1, new(3, 3));
        supporter["reaction"] = "support";
        supporter["formation"] = "formation-defensive";
        supporter["commander"] = "allied-command";
        var supported = Fixture("moving-leader-and-neighbour", leaderActor, supporter,
            Unit("enemy", "c01.banner-guard", 2, new(4, 2)));
        supported["commanders"] = new JsonArray(Commander("actor"));
        var supportedBattle = engine.CreateBattle(supported);
        var supportedState = supportedBattle.Snapshot();
        var supportPlan = VerifyCommand(engine, supportedBattle, supportedState, Id(supportedState, "actor"),
            new(3, 2), new(4, 2), "c01.border-blade", Text(supported, "id")).Plan;
        Require(supportPlan.SupportAttack is { } support && support.Attacker == Id(supportedState, "supporter")
            && support.Damage.FactorOf("commander.attack") == 1.25
            && support.Damage.Modifiers.Any(value => value.Kind == "core.formation-attack"),
            "Moving the leader updates another unit's aura and formation before its actual support attack is forecast.");

        var flank = Fixture("counter-at-moved-position", Unit("actor", "c01.swordsman", 1, new(3, 1)),
            Unit("enemy", "c01.swordsman", 2, new(4, 2)), Unit("flanker", "c01.swordsman", 2, new(2, 2)));
        var flankBattle = engine.CreateBattle(flank);
        var flankState = flankBattle.Snapshot();
        var counterPlan = VerifyCommand(engine, flankBattle, flankState, Id(flankState, "actor"),
            new(3, 2), new(4, 2), "c01.border-blade", Text(flank, "id")).Plan;
        Require(counterPlan.PrimaryUnit?.Counter is { } counter
            && counter.Modifiers.Any(value => value.Id == "position.flank")
            && counter.Modifiers.All(value => value.Id is not ("position.back-attack" or "position.side-attack")),
            "The counter sees the attacker's new position, enemy flanker and final target-facing, not the origin or last movement-facing.");

        foreach (var entering in new[] { true, false })
        {
            var origin = entering ? new Coord(1, 2) : new Coord(3, 2);
            var destination = entering ? new Coord(3, 2) : new Coord(1, 2);
            var target = entering ? new Coord(5, 2) : new Coord(0, 2);
            var actor = Unit("actor", "c01.engineer", 1, origin);
            actor["commander"] = "allied-command";
            var level = Fixture("structure-aura-" + entering, actor,
                Unit("leader", "c01.swordsman", 1, new(3, 3)), Unit("enemy", "c01.swordsman", 2, new(7, 4)));
            level["commanders"] = new JsonArray(Commander("leader"));
            level["structures"] = new JsonArray(new JsonObject
            {
                ["id"] = "node",
                ["type"] = "command_node",
                ["owner"] = 2,
                ["x"] = target.X,
                ["y"] = target.Y
            });
            var battle = engine.CreateBattle(level);
            var state = battle.Snapshot();
            var plan = VerifyCommand(engine, battle, state, Id(state, "actor"), destination, target, "c01.satchel-charge", Text(level, "id")).Plan;
            Require(plan.PrimaryStructure?.CommanderAttackMultiplier == (entering ? 1.25 : 1),
                "Structure previews use the same projected aura relationship as unit attacks.");
        }
        Console.WriteLine("  Synthetic placement contracts: entering/leaving actor formation and aura, moving commander/support neighbour, counter flanking/facing, structure aura.");
    }

    private static void FailedPreviewIsolation(BattleEngine engine)
    {
        var state = engine.CreateState(Fixture("failed-preview-isolation", Unit("actor", "c01.swordsman", 1, new(1, 2)),
            Unit("ally", "c01.swordsman", 1, new(3, 3)), Unit("enemy", "c01.swordsman", 2, new(4, 2))));
        var actor = TacticalQueries.RequireUnit(state, Id(state, "actor"));
        var original = EcmaJson.Stringify(state);
        foreach (var invalid in new[] { "allied target", "unknown weapon", "occupied destination" })
        {
            var refused = false;
            try
            {
                engine.Combat.PreviewAttack(state, Integer(actor, "id"), invalid == "occupied destination" ? new(3, 3) : new(3, 2),
                    invalid == "allied target" ? new(3, 3) : new(4, 2), invalid == "unknown weapon" ? "test.absent-weapon" : "c01.border-blade");
            }
            catch (DomainInvariantException) { refused = true; }
            Require(refused, $"{invalid}: an invalid preview must fail explicitly.");
            Require(EcmaJson.Stringify(state) == original && ReferenceEquals(actor, TacticalQueries.RequireUnit(state, Integer(actor, "id"))),
                $"{invalid}: failure after attempted placement must leave all input fields and original entity references unchanged.");
        }
    }

    private static (CombatPlan Plan, BattleReceipt Receipt) VerifyCommand(BattleEngine engine, Battle battle, JsonObject state,
        int actorId, Coord destination, Coord target, string weapon, string context)
    {
        var actor = TacticalQueries.RequireUnit(state, actorId);
        var field = engine.Space.ComputeMoveField(state, actor);
        var path = engine.Space.PathTo(field, state, destination);
        Require(path is not null && field.Stops.Contains(engine.Space.Board(state).IndexOf(destination))
            && engine.CommandsAt(state, actor, destination).Any(option => option.Ability == "attack" && option.Weapon == weapon && option.Targets.Contains(target)),
            context + ": reference action must be available through the canonical movement and command queries.");
        var original = EcmaJson.Stringify(state);
        var liveHash = battle.StateHash;
        var plan = engine.Combat.PreviewAttack(state, actorId, destination, target, weapon);
        Require(EcmaJson.Stringify(state) == original && battle.StateHash == liveHash
            && ReferenceEquals(actor, TacticalQueries.RequireUnit(state, actorId)), context + ": preview changed its input or live battle.");
        var receipt = battle.Dispatch(new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = actorId,
            ["path"] = TacticalQueries.PathJson(path!),
            ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = weapon, ["target"] = TacticalQueries.CoordinateJson(target) }
        });
        try
        {
            Require(receipt.Events.All(value => Text(value, "type") != "partingShot"),
                context + ": placement-only reference cannot include movement damage.");
            CompareReceipt(plan, state, battle.Snapshot(), receipt.Events, context);
        }
        finally
        {
            battle.Restore(receipt);
        }
        Require(battle.StateHash == liveHash && EcmaJson.Stringify(state) == original, context + ": reversible probe did not restore its exact input.");
        return (plan, receipt);
    }

    private static void CompareReceipt(CombatPlan plan, JsonObject before, JsonObject after, IReadOnlyList<JsonObject> events, string context)
    {
        var hp = new Dictionary<int, double> { [plan.Attacker] = Health(before, plan.Attacker) };
        foreach (var hit in plan.UnitHits)
        {
            var kind = hit.Primary ? "attack" : "areaAttack";
            var landed = events.Single(value => Text(value, "type") == kind && Integer(value, "defender") == hit.Target);
            Require(Number(landed, "damage") == hit.HpBefore - hit.HpAfter, context + $": {kind} damage disagrees for {hit.Target}.");
            hp[hit.Target] = hit.HpAfter;
        }
        var counters = events.Where(value => Text(value, "type") == "counter").ToArray();
        if (plan.PrimaryUnit is { Counter: not null } exchange)
        {
            Require(counters.Length == 1 && Number(counters[0], "damage") == Health(before, plan.Attacker) - exchange.AttackerHpAfter,
                context + ": counter damage disagrees with the real receipt.");
            hp[plan.Attacker] = exchange.AttackerHpAfter;
        }
        else
            Require(counters.Length == 0, context + ": a counter occurred without a forecast.");
        var supports = events.Where(value => Text(value, "type") == "supportAttack").ToArray();
        if (plan.SupportAttack is { } support)
        {
            Require(supports.Length == 1 && Integer(supports[0], "attacker") == support.Attacker
                && Integer(supports[0], "defender") == support.Target && Number(supports[0], "damage") == support.HpBefore - support.HpAfter,
                context + ": support choice or damage disagrees with the real receipt.");
            hp[support.Target] = support.HpAfter;
        }
        else
            Require(supports.Length == 0, context + ": support occurred without a forecast.");
        foreach (var hit in plan.StructureHits)
        {
            var kind = hit.Primary ? "attackStructure" : "areaAttackStructure";
            var landed = events.Single(value => Text(value, "type") == kind && Text(value, "structure") == hit.Target);
            var structure = after["structures"]!.AsArray().OfType<JsonObject>().Single(value => Text(value, "id") == hit.Target);
            Require(Number(landed, "damage") == hit.Forecast.Damage && Number(structure, "hp") == hit.Forecast.HpAfter,
                context + $": structure {hit.Target} damage or final HP disagrees.");
        }
        // Forced-move collision is an explicit later hit effect, not part of the placement forecast's exchange.
        foreach (var collision in events.Where(value => Text(value, "type") == "collisionDamage"))
            if (hp.TryGetValue(Integer(collision, "unit"), out var remaining))
                hp[Integer(collision, "unit")] = Math.Max(0, remaining - Number(collision, "amount"));
        foreach (var (id, expected) in hp)
            Require(Health(after, id) == expected, context + $": final HP for {id} is {Health(after, id)}, preview/receipt requires {expected}.");
    }

    private static double Health(JsonObject state, int id) => TacticalQueries.UnitById(state, id) is { } unit ? Number(unit, "hp")
        : state["markers"]!.AsArray().OfType<JsonObject>().Select(marker => marker["fallenUnit"] as JsonObject)
            .FirstOrDefault(fallen => fallen is not null && Integer(fallen, "id") == id) is { } snapshot ? Number(snapshot, "hp") : 0;
    private static JsonObject Fixture(string id, params JsonObject[] units) => new()
    {
        ["schema"] = 3,
        ["id"] = "test.preview." + id,
        ["width"] = 8,
        ["height"] = 5,
        ["terrain"] = new JsonArray(Enumerable.Repeat("........", 5).Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
        ["units"] = new JsonArray(units.Cast<JsonNode>().ToArray()),
        ["players"] = new JsonArray(Player(1), Player(2)),
        ["rules"] = new JsonObject { ["fog"] = false, ["zoneOfControl"] = false, ["moraleEnabled"] = false, ["turnLimit"] = null }
    };
    private static JsonObject Player(int id) => new()
    {
        ["id"] = id,
        ["team"] = id,
        ["color"] = id == 1 ? "#336699" : "#993333",
        ["controller"] = id == 1 ? "human" : "ai",
        ["resources"] = new JsonObject(),
        ["objectives"] = new JsonArray(new JsonObject { ["id"] = "route", ["type"] = "routEnemies" })
    };
    private static JsonObject Unit(string key, string type, int owner, Coord at) => new()
    {
        ["key"] = key,
        ["unit"] = type,
        ["owner"] = owner,
        ["x"] = at.X,
        ["y"] = at.Y,
        ["facing"] = owner == 1 ? "east" : "west"
    };
    private static JsonObject Commander(string leader) => new()
    {
        ["id"] = "allied-command",
        ["unitKey"] = leader,
        ["radius"] = 1,
        ["aura"] = new JsonObject { ["attackMultiplier"] = 1.25, ["defenseDelta"] = 0.1, ["movementDelta"] = 0 }
    };
    private static int Id(JsonObject state, string key) => Integer(state["units"]!.AsArray().OfType<JsonObject>().Single(unit => Text(unit, "key") == key), "id");
    private static int Integer(JsonNode node, string field) => node[field]!.GetValue<int>();
    private static double Number(JsonNode node, string field) => node[field]!.GetValue<double>();
    private static string Text(JsonNode node, string field) => node[field]!.GetValue<string>();
    private static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
