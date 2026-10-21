using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

using var inputStream = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new StreamReader(new GZipStream(inputStream, CompressionMode.Decompress)) : new StreamReader(inputStream);
var input = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var engine = new GrayFlagEngine(input["packs"]!.AsArray());
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var checks = 0;
void Check(string name, string subject, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (EcmaJson.DeepEquals(actual, expected)) return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, "failed-ability-" + subject + ".json");
    File.WriteAllText(destination, new JsonObject { ["case"] = name, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString(new() { WriteIndented = true }));
    throw new Exception($"Mismatch in {name}: {subject}; inspect {destination}");
}
JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, serialization);
AbilityRegistry Abilities(JsonObject config)
{
    var registry = DefaultAbilities.Create();
    registry.Register(new("test-charge", "蓄能")
    {
        Hint = "把一件武器充能到下一击。", Priority = 12.5,
        WeaponFor = (rules, query) => query.WeaponId is { } id
            ? rules.Combat.Forecasts.UnitWeapons(query.Unit).FirstOrDefault(weapon => StateJson.Text(weapon, "id") == id)
                ?? throw new DomainInvariantException($"unknown weapon \"{id}\"") : null,
        WeaponChoices = (rules, query) => StateJson.IsTrue(config["duplicateChoices"])
            ? Enumerable.Range(0, 40).Select(index =>
            {
                var weapon = rules.Combat.Forecasts.UnitWeapons(query.Unit)[0].DeepClone().AsObject();
                weapon["name"] = $"duplicate-{index}";
                return weapon;
            }).ToArray() : rules.Combat.Forecasts.UnitWeapons(query.Unit),
        Execute = (_, query, _, emit) =>
        {
            query.State["scenario"]!["variables"]!["charged"] = query.WeaponId ?? "";
            query.State["scenario"]!["variables"]!["origin"] = $"{query.At.X},{query.At.Y}:{query.Moved.ToString().ToLowerInvariant()}";
            emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "charge:" + (query.WeaponId ?? "") });
        },
    });
    registry.Register(new("test-pulse", "脉冲")
    {
        Hint = "自定义效果", Priority = 12.25, Tags = ["command"],
        Usable = (_, query) => !StateJson.IsTrue(query.State["scenario"]!["variables"]!["disabled"]),
        Execute = (_, query, _, emit) =>
        {
            var variables = query.State["scenario"]!["variables"]!.AsObject();
            variables["pulses"] = StateJson.Number(variables, "pulses", 0) + 1;
            emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "pulse" });
            if (StateJson.IsTrue(variables["fail"])) throw new DomainInvariantException("custom ability failed");
        },
    });
    registry.Register(new("test-mark", "标记")
    {
        SelfTargeted = StateJson.IsTrue(config["markSelf"]), Priority = 12.25,
        Engagement = config["engagement"]?.GetValue<string>() ?? "hostile-action", Targets = (_, _) => [new(3, 3)],
        Execute = (_, query, target, emit) =>
        {
            query.State["scenario"]!["variables"]!["marked"] = $"{target!.Value.X},{target.Value.Y}";
            emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "mark" });
        },
    });
    registry.Register(new("test-replace", "替身")
    {
        Execute = (_, query, _, emit) =>
        {
            var units = query.State["units"]!.AsArray();
            var index = units.IndexOf(query.Unit);
            var replacement = query.Unit.DeepClone().AsObject();
            replacement["done"] = false;
            units[index] = replacement;
            emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "actor-replaced" });
        },
    });
    registry.Register(new("test-sacrifice", "献身")
    {
        Execute = (rules, query, _, emit) =>
        {
            var id = StateJson.Integer(query.Unit, "id");
            rules.Combat.Damage.Resolve(query.State, id, StateJson.Number(query.Unit["hp"], "hp"), false,
                blow => new JsonObject { ["type"] = "statusTick", ["unit"] = id, ["status"] = "sacrifice", ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter }, emit);
        },
    });
    foreach (var (id, priority) in new[] { ("test-😀", 12.25), ("test-\ue000", 12.25), ("test-low", 12.1) })
        registry.Register(new(id, id) { Priority = priority });
    if (config["attackTags"] is JsonArray tags)
        registry.Replace(registry.Get("attack") with { Tags = tags.Select(tag => StateJson.Text(tag, "tag")).ToArray() });
    return registry.Seal();
}
JsonNode? ExpectedError(JsonObject item)
{
    if (item["error"]?["kind"]?.GetValue<string>() != "IllegalAction") return item["error"];
    var name = StateJson.Text(item, "name");
    var command = item["command"]!["command"]!.AsObject();
    var code = name switch
    {
        "custom soldier unoffered weapon" or "custom mage unoffered weapon" or "default wait rejects selected weapon" => "ability.weapon_unavailable",
        "second weapon ability blocked by selected weapon tags" or "replaced attack tags enter central status gate" or
        "custom ability tag blocks menu and execution" or "default attack tag is blocked" or "custom usable denial" => "ability.unusable",
        "no-attacks attack player 1" or "no-hostile-actions attack player 1" or "no-hostile-actions hostile-action player 1" or
        "custom targeted ability forbidden target" => "ability.target_invalid",
        "custom targeted ability missing target" => "ability.target_required",
        "ability not learned rejected" => "ability.not_owned",
        _ => throw new Exception("Unspecified semantic refusal expectation: " + name),
    };
    var details = new JsonObject { ["abilityId"] = command["ability"]!.DeepClone() };
    if (code == "ability.not_owned") details["unitType"] = item["initial"]!["units"]![0]!["type"]!.DeepClone();
    if (code == "ability.weapon_unavailable") details["weaponId"] = command["weapon"]!.DeepClone();
    return new JsonObject { ["kind"] = "IllegalAction", ["refusal"] = new JsonObject { ["code"] = code, ["details"] = details } };
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var initial = item["initial"]!.DeepClone().AsObject();
    var rules = engine.AbilityRules with { Abilities = Abilities(item["config"]!.AsObject()) };
    var commands = new AbilityCommands(rules, engine.Commands);
    var options = Node(commands.Options(initial, initial["units"]![0]!.AsObject(), TacticalQueries.Position(item["at"]!.AsObject())))!.AsArray();
    foreach (var option in options.OfType<JsonObject>())
    {
        option.Remove("engagement");
        if (option["weapon"] is null) option.Remove("weapon");
    }
    Check(name, "menus", options, item["menus"]);
    var battle = new Battle(initial, new BattleActionRegistry().Register("command", (state, action, emit) =>
    {
        void Emit(JsonObject value)
        {
            emit(value);
            var counts = state["scenario"]!["eventCounts"]!.AsObject();
            var type = StateJson.Text(value, "type");
            counts[type] = StateJson.Number(counts, type, 0) + 1;
        }
        commands.Execute(state, action, Emit);
    }));
    var events = new JsonArray(); JsonNode? error = null;
    BattleReceipt? receipt = null;
    if (item["command"] is JsonObject action)
    {
        try { receipt = battle.Dispatch(action); foreach (var value in receipt.Events) events.Add(value.DeepClone()); }
        catch (DomainInvariantException fault) { error = new JsonObject { ["kind"] = "DomainInvariant", ["message"] = fault.Message }; }
        catch (IllegalActionException fault) { error = new JsonObject { ["kind"] = "IllegalAction", ["refusal"] = Node(fault.Refusal) }; }
    }
    Check(name, "events", events, item["events"]);
    Check(name, "error", error, ExpectedError(item));
    Check(name, "state", battle.Snapshot(), item["after"]);
    Check(name, "digest", JsonValue.Create(battle.StateHash), item["hash"]);
    if (receipt is not null)
    {
        battle.Restore(receipt);
        Check(name, "undo", battle.Snapshot(), item["initial"]);
    }
}
var tagsSource = new[] { "original" };
var entry = new AbilityDefinition("probe", "Probe") { Tags = tagsSource };
tagsSource[0] = "outside";
Check("immutable tags", "tags", Node(entry.Tags), Node(new[] { "original" }));
var originalRegistry = new AbilityRegistry().Register(entry).Seal();
var cloned = originalRegistry.Clone().Replace(entry with { Name = "Changed" }).Register(new("second", "Second"));
Check("registry isolation", "keys", Node(originalRegistry.Keys()), Node(new[] { "probe" }));
Check("registry replacement", "keys", Node(cloned.Keys()), Node(new[] { "probe", "second" }));
Check("default isolation", "keys", Node(engine.Abilities.Keys()), Node(new[] { "attack", "heal", "capture", "wait" }));
Console.WriteLine($"PASS {input["cases"]!.AsArray().Count} source command cases / {checks} assertions (menus, errors, events, state, digest, undo, immutable definitions).");
