using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

internal static class AttackActivationChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var packs = Read(Path.Combine(data, "content-packs.json")).AsArray();
        var authored = EcmaJson.Stringify(packs);
        var engine = new BattleEngine(packs);
        ImmediateAttacks(engine);
        DelayedAttacks(engine);
        RefusedCommand(engine);
        CampaignCollateral(engine, oracleRoot, data);
        Require(EcmaJson.Stringify(packs) == authored, "Activation checks must preserve source content.");
    }

    private static void ImmediateAttacks(BattleEngine engine)
    {
        var supporter = Unit("supporter", "c01.templar", 1, new(3, 3));
        supporter["reaction"] = "support";
        var enemy = Unit("enemy", "c01.banner-guard", 2, new(4, 2));
        enemy["reaction"] = "counter";
        var battle = engine.CreateBattle(Fixture("unit-counter-support",
            Unit("actor", "c01.laiya", 1, new(1, 2)), supporter, enemy));
        var receipt = Attack(engine, battle, "actor", new(3, 2), new(4, 2), "c01.border-blade");
        Bracket(receipt.Events, Id(receipt.Before, "actor"), "c01.border-blade", new(3, 2), new(4, 2));
        Require(Has(receipt.Events, "attack") && Has(receipt.Events, "counter") && Has(receipt.Events, "supportAttack"),
            "The unit witness must actually exercise primary, counter and support strikes.");
        battle = engine.CreateBattle(Fixture("guard-reaction",
            Unit("actor", "c01.laiya", 1, new(1, 2)), Unit("enemy", "c01.banner-guard", 2, new(4, 2))));
        receipt = Attack(engine, battle, "actor", new(3, 2), new(4, 2), "c01.border-blade");
        Bracket(receipt.Events, Id(receipt.Before, "actor"), "c01.border-blade", new(3, 2), new(4, 2));
        Require(Has(receipt.Events, "reactionTriggered"), "The guard witness must exercise primary reaction ordering.");

        battle = engine.CreateBattle(Fixture("consumed-before-activation",
            Unit("actor", "soldier", 1, new(1, 2)), Unit("enemy", "soldier", 2, new(4, 2))));
        receipt = Attack(engine, battle, "actor", new(2, 2), new(4, 2), "soldier_javelin");
        Bracket(receipt.Events, Id(receipt.Before, "actor"), "soldier_javelin", new(2, 2), new(4, 2));
        var spent = receipt.Events.Single(value => Text(value, "type") == "resourceChanged"
            && Text(value, "resource") == "weapon_uses");
        Require(spent["amount"]!.GetValue<double>() == -1
            && Index(receipt.Events, "resourceChanged") < Index(receipt.Events, "attackActivated"),
            "Weapon consumption must succeed and be announced before activation.");

        var area = Fixture("unit-area-and-deaths", Unit("actor", "c01.engineer", 1, new(2, 2)),
            Unit("enemy", "c01.swordsman", 2, new(4, 2), 1),
            Unit("collateral", "c01.swordsman", 2, new(4, 3), 1),
            Unit("reserve", "c01.swordsman", 2, new(7, 4)));
        area["structures"] = new JsonArray(Structure("node", new(5, 2), 1));
        battle = engine.CreateBattle(area);
        receipt = Attack(engine, battle, "actor", new(3, 2), new(4, 2), "c01.satchel-charge");
        Bracket(receipt.Events, Id(receipt.Before, "actor"), "c01.satchel-charge", new(3, 2), new(4, 2));
        Require(Has(receipt.Events, "attack") && Has(receipt.Events, "areaAttack") && Has(receipt.Events, "areaAttackStructure"),
            "A unit-centered area attack retains primary and both collateral event kinds.");
        Require(receipt.Events.Count(value => Text(value, "type") == "death") == 2 && Has(receipt.Events, "structureDestroyed"),
            "Both affected unit deaths and the affected structure destruction must be bracketed.");

        var structure = Fixture("structure-primary", Unit("actor", "c01.engineer", 1, new(2, 2)),
            Unit("reserve", "c01.swordsman", 2, new(7, 4)));
        structure["structures"] = new JsonArray(Structure("node", new(4, 2), 1));
        battle = engine.CreateBattle(structure);
        receipt = Attack(engine, battle, "actor", new(3, 2), new(4, 2), "c01.satchel-charge");
        Bracket(receipt.Events, Id(receipt.Before, "actor"), "c01.satchel-charge", new(3, 2), new(4, 2));
        Require(Has(receipt.Events, "attackStructure") && Has(receipt.Events, "structureDestroyed"),
            "A structure-only primary still activates exactly once and retains its strike and destruction.");
    }

    private static void DelayedAttacks(BattleEngine engine)
    {
        foreach (var empty in new[] { false, true })
        {
            var battle = engine.CreateBattle(Fixture("delayed-area-" + empty,
                Unit("caster", "mage", 1, new(1, 2)),
                Unit("enemy", "c01.swordsman", 2, new(4, 2)),
                Unit("reserve", "c01.swordsman", 2, new(7, 4))));
            var declaration = Attack(engine, battle, "caster", new(1, 2), new(4, 2), "mage_meteor");
            Require(Has(declaration.Events, "castBegan"), "The delayed witness must declare a real charged weapon.");
            NoActivation(declaration.Events, "Declaration is not an attack activation.");
            NoActivation(EndTurn(battle).Events, "The first handoff is before this cast is due.");
            if (empty)
            {
                // The initial target is legal and occupied. Its owner then moves it out of the blast normally.
                var move = Move(engine, battle, "enemy", new(6, 2));
                NoActivation(move.Events, "Vacating the telegraph does not activate the cast early.");
            }
            var resolution = EndTurn(battle);
            Bracket(resolution.Events, Id(declaration.Before, "caster"), "mage_meteor", new(1, 2), new(4, 2));
            var resolved = Index(resolution.Events, "castResolved");
            Require(resolved >= 0 && resolved < Index(resolution.Events, "attackActivated"),
                "castResolved stays a separate lifecycle marker before activation.");
            Require(!BattleEventFacts.IsStrike(resolution.Events[resolved]), "castResolved is not a damage fact.");
            Require(empty ? !resolution.Events.Any(BattleEventFacts.IsStrike) : Has(resolution.Events, "attack"),
                "A valid due area cast activates once whether it has a victim or its target has become empty.");
            Require(battle.Snapshot()["pendingCasts"]!.AsArray().Count == 0, "A due cast must leave the pending queue.");
        }

        var cancelled = engine.CreateBattle(Fixture("cancelled-caster",
            Unit("caster", "mage", 1, new(1, 2), 1),
            Unit("ally", "c01.swordsman", 1, new(0, 4)),
            Unit("enemy", "c01.swordsman", 2, new(4, 2))));
        var declared = Attack(engine, cancelled, "caster", new(1, 2), new(4, 2), "mage_meteor");
        NoActivation(declared.Events, "A later-cancelled declaration never activates.");
        EndTurn(cancelled);
        var killing = Attack(engine, cancelled, "enemy", new(2, 2), new(1, 2), "c01.border-blade");
        Require(Has(killing.Events, "death"), "The cancellation witness must actually lose its caster.");
        Require(Has(killing.Events, "castCancelled") && !Has(killing.Events, "castResolved"),
            "The caster's departure cancels the pending cast immediately.");
        Bracket(killing.Events, Id(killing.Before, "enemy"), "c01.border-blade", new(2, 2), new(1, 2));
        Require(killing.Events.Where(value => Text(value, "type") is "attackActivated" or "attackCompleted")
            .All(value => Integer(value, "attacker") != Id(declared.Before, "caster")),
            "The killing attack cannot activate the cancelled caster's attack.");
        var cancellation = EndTurn(cancelled);
        NoActivation(cancellation.Events, "The cancelled cast cannot activate on its former due turn.");
        Require(cancelled.Snapshot()["pendingCasts"]!.AsArray().Count == 0, "Cancellation removes the pending cast.");
    }

    private static void RefusedCommand(BattleEngine engine)
    {
        var battle = engine.CreateBattle(Fixture("refused-command", Unit("actor", "c01.swordsman", 1, new(1, 2)),
            Unit("ally", "c01.swordsman", 1, new(4, 2)), Unit("enemy", "c01.swordsman", 2, new(7, 4))));
        var state = battle.Snapshot();
        var before = EcmaJson.Stringify(state);
        var hash = battle.StateHash;
        var actor = TacticalQueries.RequireUnit(state, Id(state, "actor"));
        var path = engine.Space.PathTo(engine.Space.ComputeMoveField(state, actor), state, new(3, 2));
        Require(path is not null, "The refusal witness must have a legal movement path before the refused attack.");
        BattleReceipt? receipt = null;
        var refused = false;
        try
        {
            receipt = battle.Dispatch(Command(Id(state, "actor"), path!, "c01.border-blade", new(4, 2)));
        }
        catch (IllegalActionException) { refused = true; }
        Require(refused && receipt is null, "A refused attack cannot publish a receipt or its partial activation facts.");
        Require(battle.StateHash == hash && EcmaJson.Stringify(battle.Snapshot()) == before,
            "Refusing an attack after legal movement must roll back all state, including scenario event counts.");
    }

    private static void CampaignCollateral(BattleEngine engine, string oracleRoot, string data)
    {
        var fixture = Read(Path.Combine(oracleRoot, "../native/c01-09-structure-collateral.json")).AsObject();
        var original = EcmaJson.Stringify(fixture);
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(value => Text(value, "id") == Text(fixture, "levelId"));
        var authored = EcmaJson.Stringify(level);
        var battle = engine.CreateBattle(level);
        var initial = battle.Snapshot();
        var recorder = engine.RecordBattle(initial);
        Require(fixture["prefix"]!.AsArray().Count == 58, "Use the complete formal campaign witness.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
        {
            battle.Dispatch(action);
            recorder.Record(action);
        }
        var saved = engine.SaveBattle(battle.Snapshot());
        Require(Integer(saved, "schema") == 3, "Snapshot schema stays independent from the new action replay contract.");
        var restored = engine.LoadSavedBattle(saved);
        var command = fixture["action"]!.AsObject();
        var receipt = battle.Dispatch(command);
        recorder.Record(command);
        var continued = restored.Dispatch(command);
        Require(EcmaJson.DeepEquals(battle.Snapshot(), restored.Snapshot())
            && EcmaJson.DeepEquals(new JsonArray(receipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()),
                new JsonArray(continued.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())),
            "Saving and reloading before activation preserves the exact resulting state and receipt.");
        var replay = recorder.Replay(battle.Snapshot());
        Require(Integer(replay, "schema") == 4, "Replay schema identifies the new lifecycle facts and their counted hashes.");
        var replayed = engine.ReplayBattle(level, replay);
        Require(replayed.DivergedAt is null && EcmaJson.DeepEquals(replayed.State, battle.Snapshot()),
            "Current action replay reproduces the full activation-bearing state without hash exclusions.");
        var obsolete = (JsonObject)replay.DeepClone();
        obsolete["schema"] = 3;
        obsolete["actions"] = new JsonArray(new JsonObject { ["kind"] = "unregistered-must-not-execute" });
        var refused = engine.ReplayBattle(level, obsolete);
        Require(refused.DivergedAt == 0 && refused.Reason == "不支持回放 schema 3"
            && EcmaJson.DeepEquals(refused.State, initial),
            "Pre-activation action replay is refused explicitly before any recorded action executes.");
        var at = TacticalQueries.Position(command["command"]!["target"]!.AsObject());
        var from = TacticalQueries.Position(command["path"]!.AsArray().Last()!.AsObject());
        Bracket(receipt.Events, Integer(command, "unit"), Text(command["command"]!, "weapon"), from, at);
        Require(Has(receipt.Events, "attackStructure") && Has(receipt.Events, "areaAttack"),
            "The real C01-09 witness must retain its structure primary and unit collateral.");
        Require(EcmaJson.Stringify(level) == authored && EcmaJson.Stringify(fixture) == original,
            "Replay must preserve the authored level, lawful actions and original oracle fixture.");
        // Old hashes include scenario.eventCounts, so adding lifecycle facts intentionally changes them.
        Console.WriteLine("  Activation contract: immediate unit/structure/area, deaths, counter/support, delayed occupied/empty, cancelled/refused, and 58-action C01-09 witness.");
    }

    private static void Bracket(IReadOnlyList<JsonObject> events, int actor, string weapon, Coord from, Coord at)
    {
        Require(events.Count(value => Text(value, "type") == "attackActivated") == 1
            && events.Count(value => Text(value, "type") == "attackCompleted") == 1,
            "One primary execution must have exactly one activation and one completion, independent of victim count.");
        var start = Index(events, "attackActivated");
        var end = Index(events, "attackCompleted");
        Require(start < end, "Activation must precede completion.");
        var activation = events[start];
        var completion = events[end];
        Require(Integer(activation, "attacker") == actor && Text(activation, "weapon") == weapon
            && TacticalQueries.Position(activation["from"]!.AsObject()) == from
            && TacticalQueries.Position(activation["at"]!.AsObject()) == at,
            "Activation identifies the attacker, weapon, executed origin and aimed cell.");
        Require(Integer(completion, "attacker") == actor && Text(completion, "weapon") == weapon,
            "Completion identifies the same attacker and weapon.");
        Require(!activation.ContainsKey("damage") && !completion.ContainsKey("damage")
            && !BattleEventFacts.IsStrike(activation) && !BattleEventFacts.IsStrike(completion),
            "Lifecycle facts are not damage-bearing strikes.");
        for (var index = 0; index < events.Count; index++)
        {
            var value = events[index];
            var kind = Text(value, "type");
            if (kind is "attack" or "areaAttack" or "attackStructure" or "areaAttackStructure" or "reactionTriggered"
                or "structureDamaged" or "structureDestroyed")
                Require(index > start && index < end, kind + " must stay inside the primary attack bracket.");
            if (kind == "resourceChanged" && Text(value, "resource") == "momentum"
                && value["subject"] is JsonObject subject && Text(subject, "kind") == "unit"
                && Integer(subject, "id") == actor && value["amount"]!.GetValue<double>() > 0)
                Require(index > end, "The attacker's momentum reward follows primary completion.");
            if (kind is "counter" or "supportAttack")
                Require(index > end, kind + " is a separate strike outside the primary attack bracket.");
            if (kind == "death" && !events.Take(index).Any(item => Text(item, "type") is "counter" or "supportAttack"))
                Require(index > start && index < end, "A primary affected death must occur before attack completion.");
        }
        Require(BattleEventFacts.StrikeCount(events) == BattleEventFacts.StrikeCount(events.Where(value =>
            Text(value, "type") is not ("attackActivated" or "attackCompleted"))),
            "Lifecycle facts cannot inflate strike counts.");
    }

    private static BattleReceipt Attack(BattleEngine engine, Battle battle, string key, Coord from, Coord target, string weapon)
    {
        var state = battle.Snapshot();
        var actor = TacticalQueries.RequireUnit(state, Id(state, key));
        var field = engine.Space.ComputeMoveField(state, actor);
        var path = engine.Space.PathTo(field, state, from);
        Require(path is not null && field.Stops.Contains(engine.Space.Board(state).IndexOf(from))
            && engine.CommandsAt(state, actor, from).Any(option => option.Ability == "attack"
                && option.Weapon == weapon && option.Targets.Contains(target)), "The attack witness must be available through normal command queries.");
        return battle.Dispatch(Command(Integer(actor, "id"), path!, weapon, target));
    }

    private static BattleReceipt Move(BattleEngine engine, Battle battle, string key, Coord to)
    {
        var state = battle.Snapshot();
        var actor = TacticalQueries.RequireUnit(state, Id(state, key));
        var field = engine.Space.ComputeMoveField(state, actor);
        var path = engine.Space.PathTo(field, state, to);
        Require(path is not null && field.Stops.Contains(engine.Space.Board(state).IndexOf(to)), "The target must vacate using lawful movement.");
        return battle.Dispatch(new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = Integer(actor, "id"),
            ["path"] = TacticalQueries.PathJson(path!),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        });
    }
    private static JsonObject Command(int actor, IReadOnlyList<Coord> path, string weapon, Coord target) => new()
    {
        ["kind"] = "command",
        ["unit"] = actor,
        ["path"] = TacticalQueries.PathJson(path),
        ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = weapon, ["target"] = TacticalQueries.CoordinateJson(target) }
    };
    private static BattleReceipt EndTurn(Battle battle) => battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
    private static void NoActivation(IEnumerable<JsonObject> events, string message) =>
        Require(events.All(value => Text(value, "type") is not ("attackActivated" or "attackCompleted")), message);
    private static bool Has(IEnumerable<JsonObject> events, string kind) => events.Any(value => Text(value, "type") == kind);
    private static int Index(IReadOnlyList<JsonObject> events, string kind) => Enumerable.Range(0, events.Count).FirstOrDefault(index => Text(events[index], "type") == kind, -1);
    private static JsonObject Fixture(string id, params JsonObject[] units) => new()
    {
        ["schema"] = 3,
        ["id"] = "test.activation." + id,
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
        ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" })
    };
    private static JsonObject Unit(string key, string type, int owner, Coord at, int? hp = null)
    {
        var unit = new JsonObject
        {
            ["key"] = key,
            ["unit"] = type,
            ["owner"] = owner,
            ["x"] = at.X,
            ["y"] = at.Y,
            ["facing"] = owner == 1 ? "east" : "west"
        };
        if (hp is not null)
            unit["hp"] = hp.Value;
        return unit;
    }
    private static JsonObject Structure(string id, Coord at, int hp) => new()
    {
        ["id"] = id,
        ["type"] = "command_node",
        ["owner"] = 2,
        ["x"] = at.X,
        ["y"] = at.Y,
        ["hp"] = hp
    };
    private static int Id(JsonObject state, string key) => Integer(state["units"]!.AsArray().OfType<JsonObject>().Single(value => Text(value, "key") == key), "id");
    private static int Integer(JsonNode value, string field) => value[field]!.GetValue<int>();
    private static string Text(JsonNode value, string field) => value[field]!.GetValue<string>();
    private static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
