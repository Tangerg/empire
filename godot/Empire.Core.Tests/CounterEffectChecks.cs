using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

internal static class CounterEffectChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var catalog = new ReadOnlyContent(Read(Path.Combine(data, "content-packs.json")).AsArray());
        var engine = BattleEngineFactory.Create(catalog);
        PlannedPayloadOwnsExecution(engine);
        AbsentCountersHaveNoEffects(engine);
        DepartedTargetsReceiveNoEffects(engine);
        EarlierEffectsCanSaveTheAttacker(catalog);
        CollisionNormalization(engine);
        ShippedCollision(engine, data, Path.Combine(oracleRoot, "../native/c01-14-collision-forecast.json"));
    }

    private static void PlannedPayloadOwnsExecution(BattleEngine engine)
    {
        var state = engine.CreateState(Fixture("selected-counter",
            Unit("actor", "c01.engineer", 1, new(1, 1)),
            Unit("defender", "c01.rune-artificer", 2, new(3, 1))));
        var plan = PlanAttack(engine, state, "c01.satchel-charge");
        Require(plan.PrimaryUnit?.Counter?.Weapon == "c01.forge-burst",
            "The selected counter is the ranged secondary weapon, not the defender's primary hammer.");
        var canonical = engine.Catalog.Definition("weapons", "c01.forge-burst")!.Value.GetRawText();
        Require(EcmaJson.DeepEquals(new JsonArray(plan.CounterEffects.Select(effect => effect.DeepClone()).ToArray()),
            ReadJson(canonical)["hitEffects"]), "Counter payload must match the selected counter weapon.");
        var plannedEffect = plan.CounterEffects.Single(effect => Text(effect, "type") == "addStatus");
        plannedEffect["status"] = "poisoned";
        plannedEffect["duration"] = 4;

        var nextPlan = PlanAttack(engine, state, "c01.satchel-charge");
        Require(nextPlan.CounterEffects.Any(effect => Text(effect, "type") == "addStatus"
            && Text(effect, "status") == "armor_down" && Number(effect, "duration") == 2)
            && engine.Catalog.Definition("weapons", "c01.forge-burst")!.Value.GetRawText() == canonical,
            "Mutating a planned counter effect must not change the catalog or a later forecast.");

        var events = new List<JsonObject>();
        engine.Combat.ExecutePlan(state, plan, events.Add);
        var attacker = TacticalQueries.RequireUnit(state, plan.Attacker);
        Require(attacker["statuses"]!.AsArray().OfType<JsonObject>().Any(status => Text(status, "id") == "poisoned"
            && Number(status, "remaining") == 4), "Execution must consume the exact planned counter payload.");
        Require(events.Any(value => Text(value, "type") == "statusApplied" && Integer(value, "unit") == plan.Attacker
            && Text(value, "status") == "poisoned" && Number(value, "remaining") == 4)
            && !attacker["statuses"]!.AsArray().OfType<JsonObject>().Any(status => Text(status, "id") == "armor_down"),
            "Counter execution must not silently replace its plan with a fresh catalog lookup.");
    }

    private static void AbsentCountersHaveNoEffects(BattleEngine engine)
    {
        foreach (var reason in new[] { "disabled", "guard", "lethal-primary" })
        {
            var defender = Unit("defender", "rogue", 2, new(2, 1));
            if (reason == "guard")
                defender["reaction"] = "guard";
            if (reason == "lethal-primary")
                defender["hp"] = 1;
            var fixture = Fixture(reason, Unit("actor", "c01.swordsman", 1, new(1, 1)), defender);
            if (reason == "disabled")
                fixture["rules"]!["counterAttack"] = false;
            var state = engine.CreateState(fixture);
            var plan = PlanAttack(engine, state, "c01.border-blade");
            Require(plan.PrimaryUnit?.Counter is null && plan.CounterEffects.Count == 0,
                reason + ": a weapon with effects must not disclose a counter payload when no counter exists.");
            var events = new List<JsonObject>();
            engine.Combat.ExecutePlan(state, plan, events.Add);
            Require(events.All(value => Text(value, "type") != "counter"), reason + ": execution must not invent a counter.");
        }
    }

    private static void DepartedTargetsReceiveNoEffects(BattleEngine engine)
    {
        foreach (var departure in new[] { "death", "unitRouted" })
        {
            var actor = Unit("actor", "c01.swordsman", 1, new(1, 1));
            if (departure == "death")
                actor["hp"] = 1;
            else
                actor["morale"] = 1;
            var fixture = Fixture(departure, actor, Unit("defender", "rogue", 2, new(2, 1)));
            fixture["rules"]!["moraleEnabled"] = departure == "unitRouted";
            var state = engine.CreateState(fixture);
            var plan = PlanAttack(engine, state, "c01.border-blade");
            Require(plan.PrimaryUnit?.Counter is not null && plan.CounterEffects.Any(effect => Text(effect, "type") == "addStatus"),
                departure + ": the counter payload must exist even when a later departure prevents applying it.");
            Require(plan.PrimaryUnit!.AttackerDies == (departure == "death"),
                "The death fixture is directly lethal; the rout fixture must retain HP after direct damage.");
            var events = new List<JsonObject>();
            engine.Combat.ExecutePlan(state, plan, events.Add);
            Require(events.Any(value => Text(value, "type") == departure && Integer(value, "unit") == plan.Attacker)
                && TacticalQueries.UnitById(state, plan.Attacker) is null, departure + ": the canonical damage owner must remove the target.");
            Require(events.All(value => Text(value, "type") != "statusApplied" || Integer(value, "unit") != plan.Attacker),
                departure + ": no counter effect may run after damage or morale removes its recipient.");
        }
    }

    private static void EarlierEffectsCanSaveTheAttacker(ReadOnlyContent catalog)
    {
        var healed = 0.0;
        var plugin = new EnginePlugin("test.counter-effect-healing", 1, context =>
        {
            var effects = context.Require(Keys.HitEffects).Clone();
            var original = effects.Get("addStatus");
            effects.Replace(original with
            {
                Apply = (query, effect) =>
                {
                    original.Apply(query, effect);
                    if (Integer(query.Attacker, "owner") == 1)
                    {
                        var health = new UnitHealth(query.Content, query.Attacker);
                        healed += health.Heal(health.Missing);
                    }
                }
            });
            context.Replace(Keys.HitEffects, effects);
        }, overrides: [Keys.HitEffects], requiresCapabilities: [Keys.HitEffects]);
        var engine = BattleEngineFactory.Create(catalog, [plugin]);
        var actor = Unit("actor", "rogue", 1, new(1, 1));
        actor["hp"] = 1;
        var state = engine.CreateState(Fixture("healing-before-counter", actor, Unit("defender", "rogue", 2, new(2, 1))));
        var plan = PlanAttack(engine, state, "rogue_blades");
        Require(plan.PrimaryUnit is { AttackerDies: true, Counter: not null }
            && plan.UnitHits.Any(hit => hit.Primary && hit.Effects.Count > 0)
            && plan.CounterEffects.Any(effect => Text(effect, "type") == "addStatus") && healed == 0,
            "A directly lethal counter still carries effects because earlier extensible effects have not executed yet.");
        var events = new List<JsonObject>();
        engine.Combat.ExecutePlan(state, plan, events.Add);
        var survivor = TacticalQueries.UnitById(state, plan.Attacker);
        Require(healed > 0 && survivor is not null && Number(survivor, "hp") > 0,
            "The primary hit's composed effect must save the attacker before the planned counter lands.");
        Require(events.Any(value => Text(value, "type") == "counter" && Integer(value, "defender") == plan.Attacker)
            && events.Any(value => Text(value, "type") == "statusApplied" && Integer(value, "unit") == plan.Attacker
                && Text(value, "status") == "poisoned"),
            "The saved attacker must receive the planned counter effect despite the initial lethal forecast.");
    }

    private static void CollisionNormalization(BattleEngine engine)
    {
        foreach (var (configured, expected) in new[] { (-1.5, 0.0), (0.0, 0.0), (0.49, 0.0), (0.5, 1.0), (1.49, 1.0), (1.5, 2.0), (8.5, 9.0) })
            Require(BattleCombat.CollisionDamageAmount(configured) == expected,
                $"Collision damage {configured} must normalize to {expected}, matching gameplay rounding and clamping.");

        foreach (var (configured, hp, expected) in new[] { (0.49, 98.0, 0.0), (0.5, 98.0, 1.0), (1.5, 98.0, 2.0), (8.5, 2.0, 2.0) })
        {
            var target = Unit("target", "c01.swordsman", 2, new(0, 1));
            target["hp"] = hp;
            var state = engine.CreateState(Fixture("fractional-collision", Unit("actor", "c01.swordsman", 1, new(1, 1)), target));
            var events = new List<JsonObject>();
            var targetId = Integer(ByKey(state, "target"), "id");
            var result = engine.Combat.ForceMove(state, targetId, new(1, 1), "push", 1, configured, events.Add);
            Require(result.From == result.To && events.Any(value => Text(value, "type") == "forcedMove" && value["collided"]!.GetValue<bool>()),
                "The edge fixture must produce a real blocked forced move.");
            var collision = events.SingleOrDefault(value => Text(value, "type") == "collisionDamage");
            Require(expected == 0 ? collision is null : collision is not null && Number(collision, "amount") == expected,
                "Forced movement must use the disclosed normalization and cap actual damage to remaining HP.");
            Require(result.Killed == (expected == hp) && (TacticalQueries.UnitById(state, targetId) is { } survivor
                ? Number(survivor, "hp") == hp - expected : expected == hp), "Collision HP and departure must match the emitted damage.");
        }
    }

    private static void ShippedCollision(BattleEngine engine, string data, string fixturePath)
    {
        var fixture = Read(fixturePath).AsObject();
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(value => Text(value, "id") == Text(fixture, "levelId"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == Text(fixture, "initialStateHash"), "The collision witness starts from the unchanged shipped C01-14 level.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == Text(fixture, "beforeStateHash"), "The real command prefix must reach its recorded collision state.");
        var state = battle.Snapshot();
        var command = fixture["action"]!.AsObject();
        var actorId = Integer(command, "unit");
        var at = TacticalQueries.Position(command["path"]!.AsArray()[^1]!.AsObject());
        var target = TacticalQueries.Position(command["command"]!["target"]!.AsObject());
        var weapon = Text(command["command"]!, "weapon");
        var plan = engine.Combat.PreviewAttack(state, actorId, at, target, weapon);
        Require(plan.PrimaryUnit?.Counter?.Weapon == "c01.golem-slam" && plan.CounterEffects.Any(effect =>
            Text(effect, "type") == "forcedMove" && Text(effect, "mode") == "push" && Number(effect, "collisionDamage") == 10),
            "The real golem counter must disclose its separate collision-capable push.");
        var receipt = battle.Dispatch(command);
        var collision = receipt.Events.Single(value => Text(value, "type") == "collisionDamage" && Integer(value, "unit") == actorId);
        Require(Number(collision, "amount") == 10 && Number(collision, "hpAfter") == plan.PrimaryUnit!.AttackerHpAfter - 10,
            "The shipped receipt must retain the direct counter forecast and apply collision as a separate later effect.");
        Require(battle.StateHash == Text(fixture, "afterStateHash"), "Counter disclosure must not change the recorded C01-14 outcome.");
    }

    private static CombatPlan PlanAttack(BattleEngine engine, JsonObject state, string weapon)
    {
        var actor = ByKey(state, "actor");
        var at = TacticalQueries.Position(actor);
        var target = TacticalQueries.Position(ByKey(state, "defender"));
        Require(engine.CommandsAt(state, actor, at).Any(option => option.Ability == "attack" && option.Weapon == weapon && option.Targets.Contains(target)),
            "Synthetic attacks must be available through the canonical command query.");
        var before = EcmaJson.Stringify(state);
        var plan = engine.Combat.PreviewAttack(state, Integer(actor, "id"), at, target, weapon);
        Require(EcmaJson.Stringify(state) == before, "Effect disclosure must not change the input battlefield.");
        return plan;
    }

    private static JsonObject Fixture(string id, params JsonObject[] units) => new()
    {
        ["schema"] = 3,
        ["id"] = "test.counter-effects." + id,
        ["width"] = 6,
        ["height"] = 4,
        ["terrain"] = new JsonArray("......", "......", "......", "......"),
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
        ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" })
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
    private static JsonObject ByKey(JsonObject state, string key) => state["units"]!.AsArray().OfType<JsonObject>().Single(unit => Text(unit, "key") == key);
    private static int Integer(JsonNode node, string field) => node[field]!.GetValue<int>();
    private static double Number(JsonNode node, string field) => node[field]!.GetValue<double>();
    private static string Text(JsonNode node, string field) => node[field]!.GetValue<string>();
    private static JsonNode Read(string path) => ReadJson(File.ReadAllText(path));
    private static JsonNode ReadJson(string source) => EcmaJson.Parse(source)!;
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
