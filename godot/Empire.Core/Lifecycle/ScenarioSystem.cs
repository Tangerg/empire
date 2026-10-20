using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

/// <summary>Source trigger timing, ledger and conditions. Unsupported effects require an explicit handler.</summary>
public sealed class ScenarioSystem
{
    public static IReadOnlyList<string> SupportedConditions { get; } = System.Array.AsReadOnly(new[]
    {
        "turnAtLeast", "turnCycle", "chance", "markerCount", "currentPlayer", "variable", "unitCount", "eventCount",
        "unitInZone", "unitHealth", "structure", "objective", "all", "any", "not",
    });
    public static IReadOnlyList<string> SupportedEffects { get; } = System.Array.AsReadOnly(new[] { "emitSignal" });
    private readonly StateContent content;
    private readonly Func<JsonObject, JsonObject, bool>? extraCondition;
    private readonly Action<JsonObject, JsonObject, Action<JsonObject>>? extraEffect;
    public ScenarioSystem(JsonArray packs, Func<JsonObject, JsonObject, bool>? extraCondition = null,
        Action<JsonObject, JsonObject, Action<JsonObject>>? extraEffect = null)
        : this(new StateContent(packs), extraCondition, extraEffect) { }
    internal ScenarioSystem(StateContent content, Func<JsonObject, JsonObject, bool>? extraCondition = null,
        Action<JsonObject, JsonObject, Action<JsonObject>>? extraEffect = null)
    { this.content = content; this.extraCondition = extraCondition; this.extraEffect = extraEffect; }

    public IReadOnlyList<JsonObject> SelectUnits(JsonObject state, JsonObject selector) => Units(state).Where(unit =>
        (selector["ids"] is not JsonArray ids || ids.Any(id => Integer(id, "unit id") == Integer(unit, "id"))) &&
        (selector["keys"] is not JsonArray keys || unit["key"] is { } key && keys.Any(candidate => Text(candidate, "unit key") == Text(key, "unit key"))) &&
        (selector["owner"] is null || Integer(selector, "owner") == Integer(unit, "owner")) &&
        (selector["zone"] is null || Zone(state, Text(selector, "zone")).Any(cell => SameCell(unit, cell))) &&
        (selector["anyTags"] is not JsonArray tags || tags.Count == 0 ||
            Array(content.Get("units", Text(unit, "type"))["tags"], "unit tags").Any(tag => tags.Any(candidate => Empire.Core.Determinism.EcmaJson.DeepEquals(candidate, tag)))))
        .ToArray();

    public IReadOnlyList<JsonObject> SelectMarkers(JsonObject state, JsonObject selector) =>
        Objects(state["markers"], "markers").Where(marker =>
            (selector["ids"] is not JsonArray ids || ids.Any(id => Empire.Core.Determinism.EcmaJson.DeepEquals(id, marker["id"]))) &&
            (selector["kind"] is null || Text(selector, "kind") == Text(marker, "kind")) &&
            (selector["owner"] is null || Empire.Core.Determinism.EcmaJson.DeepEquals(selector["owner"], marker["owner"])) &&
            (selector["zone"] is null || Zone(state, Text(selector, "zone")).Any(cell => SameCell(Object(marker["at"], "marker position"), cell))))
        .ToArray();

    private static bool Chance(JsonObject state, JsonObject condition)
    {
        var stored = Object(state["random"], "random");
        var counters = Object(stored["counters"], "random counters");
        var random = new RandomState(EcmaNumber.ToUint32(Number(stored["seed"], "seed")),
            counters.ToDictionary(pair => pair.Key, pair => Number(pair.Value, "random counter"), StringComparer.Ordinal));
        var stream = condition["stream"] is { } streamValue ? EcmaJson.ReadString(streamValue) : "scenario.chance";
        var result = new SplitMixRandom().Chance(random, stream, Number(condition["percent"], "percent"));
        // Preserve insertion order and untouched counters; the canonical RNG owns all arithmetic.
        if (random.Counters.TryGetValue(stream, out var counter)) counters[stream] = counter;
        return result;
    }

    public bool ConditionMet(JsonObject state, JsonObject condition)
    {
        var scenario = Object(state["scenario"], "scenario");
        switch (Text(condition, "type"))
        {
            case "turnAtLeast": return Number(state["turn"], "turn") >= Number(condition["turn"], "turn");
            case "turnCycle":
                var every = Number(condition["every"], "every");
                var offset = Round(Number(condition, "offset", 1));
                var turn = Number(state["turn"], "turn");
                return every >= 1 && every == Math.Truncate(every) && turn >= offset && (turn - offset) % every == 0;
            case "chance": return Chance(state, condition);
            case "markerCount": return Compare(JsonValue.Create(SelectMarkers(state, Object(condition["selector"], "selector")).Count), Text(condition, "op"), condition["value"]);
            case "currentPlayer": return Integer(state, "currentPlayer") == Integer(condition, "player");
            case "variable": return Compare(Object(scenario["variables"], "variables")[Text(condition, "key")], Text(condition, "op"), condition["value"]);
            case "unitCount": return Compare(JsonValue.Create(SelectUnits(state, Object(condition["selector"], "selector")).Count), Text(condition, "op"), condition["value"]);
            case "eventCount": return Compare(Object(scenario["eventCounts"], "eventCounts")[Text(condition, "event")] ?? JsonValue.Create(0), Text(condition, "op"), condition["value"]);
            case "unitInZone":
                var selector = new JsonObject();
                foreach (var field in new[] { "owner", "zone", "anyTags" }) CopyIfPresent(condition, selector, field);
                return SelectUnits(state, selector).Count > 0;
            case "unitHealth":
                var ratios = SelectUnits(state, Object(condition["selector"], "selector")).Select(unit =>
                    Number(unit["hp"], "hp") / Number(content.Get("units", Text(unit, "type"))["maxHp"], "maxHp")).ToArray();
                if (ratios.Length == 0) return false;
                bool Match(double value) => Compare(JsonValue.Create(value), Text(condition, "op"), condition["value"]);
                return Text(condition, "aggregate") switch { "any" => ratios.Any(Match), "all" => ratios.All(Match), _ => Match(ratios.Average()) };
            case "structure":
                var structure = Objects(state["structures"], "structures").FirstOrDefault(s => Text(s, "id") == Text(condition, "id"));
                var requested = Text(condition, "state");
                if (structure is null) return requested == "destroyed";
                var hp = Number(structure["hp"], "structure hp");
                return requested == "destroyed" ? hp <= 0 : requested == "disabled" ? hp > 0 && IsTrue(structure["disabled"]) : hp > 0 && !IsTrue(structure["disabled"]);
            case "objective": return Object(Player(state, Integer(condition, "player"))["objectiveStates"], "objective states")[Text(condition, "id")]?["status"]?.GetValue<string>() == Text(condition, "status");
            case "all": return Objects(condition["conditions"], "conditions").All(child => ConditionMet(state, child));
            case "any": return Objects(condition["conditions"], "conditions").Any(child => ConditionMet(state, child));
            case "not": return !ConditionMet(state, Object(condition["condition"], "condition"));
            default: return extraCondition?.Invoke(state, condition) ?? throw new DomainInvariantException($"Unsupported scenario condition \"{Text(condition, "type")}\"; compose an explicit handler");
        }
    }

    public void Run(JsonObject state, string timing, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        var triggers = Objects(scenario["triggers"], "triggers").ToArray();
        var fired = new HashSet<string>(StringComparer.Ordinal);
        var occurrence = $"{Integer(state, "turn")}:{Integer(state, "currentPlayer")}:{timing}";
        for (var pass = 0; pass < triggers.Length + 1; pass++)
        {
            var changed = false;
            foreach (var trigger in triggers)
            {
                var id = Text(trigger, "id");
                if (fired.Contains(id) || !Due(state, trigger, timing, occurrence) || !ConditionMet(state, Object(trigger["condition"], "condition"))) continue;
                fired.Add(id);
                if (trigger["repeat"] is null) Array(scenario["firedTriggerIds"], "firedTriggerIds").Add(id);
                else
                {
                    var runtime = Object(scenario["triggerRuntime"], "triggerRuntime");
                    var previous = runtime[id] as JsonObject;
                    runtime[id] = new JsonObject { ["count"] = previous is null ? 1 : Number(previous, "count", 0) + 1, ["lastOccurrence"] = occurrence };
                }
                foreach (var effect in Objects(trigger["effects"], "effects"))
                {
                    if (Text(effect, "type") == "emitSignal") emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = Text(effect, "signal") });
                    else if (extraEffect is not null) extraEffect(state, effect, emit);
                    else throw new DomainInvariantException($"Unsupported scenario effect \"{Text(effect, "type")}\"; compose an explicit handler");
                }
                changed = true;
            }
            if (!changed) return;
        }
        throw new DomainInvariantException($"scenario trigger loop did not stabilise at {timing}");
    }

    private static bool Due(JsonObject state, JsonObject trigger, string timing, string occurrence)
    {
        if (Text(trigger, "timing") != timing) return false;
        var scenario = Object(state["scenario"], "scenario");
        var id = Text(trigger, "id");
        if (trigger["repeat"] is not JsonObject repeat)
            return !Array(scenario["firedTriggerIds"], "firedTriggerIds").Any(entry => Text(entry, "fired trigger id") == id);
        var cadence = Number(repeat["everyRounds"], "everyRounds");
        var turn = Number(state["turn"], "turn");
        var start = Number(repeat, "startTurn", 1);
        if (cadence < 1 || cadence != Math.Truncate(cadence) || turn < start ||
            repeat["endTurn"] is { } end && turn > Number(end, "endTurn") || (turn - start) % cadence != 0) return false;
        var runtime = Object(scenario["triggerRuntime"], "triggerRuntime")[id] as JsonObject;
        var count = runtime is null ? 0 : Number(runtime, "count", 0);
        return (repeat["maxFirings"] is null || count < Number(repeat["maxFirings"], "maxFirings")) &&
            runtime?["lastOccurrence"]?.GetValue<string>() != occurrence;
    }

    internal static bool Compare(JsonNode? left, string operation, JsonNode? right) => operation switch
    {
        "eq" => Empire.Core.Determinism.EcmaJson.DeepEquals(left, right),
        "neq" => !Empire.Core.Determinism.EcmaJson.DeepEquals(left, right),
        "gte" => left?.GetValueKind() == JsonValueKind.Number && right?.GetValueKind() == JsonValueKind.Number && Number(left, "left") >= Number(right, "right"),
        "lte" => left?.GetValueKind() == JsonValueKind.Number && right?.GetValueKind() == JsonValueKind.Number && Number(left, "left") <= Number(right, "right"),
        _ => throw new DomainInvariantException($"unknown scenario comparison \"{operation}\""),
    };
    internal static IEnumerable<JsonObject> Zone(JsonObject state, string id) => Objects(
        Object(Object(state["scenario"], "scenario")["zones"], "zones")[id] ?? throw new DomainInvariantException($"unknown scenario zone \"{id}\""), "zone");
    internal static bool SameCell(JsonObject left, JsonObject right) => Integer(left, "x") == Integer(right, "x") && Integer(left, "y") == Integer(right, "y");
}
