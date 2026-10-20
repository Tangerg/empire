using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Empire.Core.Campaign;

public static class CampaignValidation
{
    private static readonly char[] Whitespace =
    [
        '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u00A0', '\u1680',
        '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007',
        '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000', '\uFEFF',
    ];

    public static void ValidateDefinition(JsonObject definition)
    {
        if (!NumberEquals(definition["schema"], 1))
            throw new CampaignInvariantException($"unsupported campaign schema {Display(definition, "schema")}");
        if (!Named(definition["id"])) throw new CampaignInvariantException("campaign id cannot be empty");
        if (!Integer(definition["version"]) || Number(definition["version"]) < 1)
            throw new CampaignInvariantException("campaign version must be positive");
        foreach (var (id, version) in Object(definition["contentPacks"], "contentPacks"))
            if (!Named(id) || !Integer(version) || Number(version) < 1)
                throw new CampaignInvariantException($"campaign content pack \"{id}\" has invalid version");
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in Array(definition["nodes"], "nodes"))
        {
            var node = Object(value, "campaign node");
            if (!Named(node["id"])) throw new CampaignInvariantException("campaign node id cannot be empty");
            var id = Text(node["id"]);
            if (!nodes.Add(id)) throw new CampaignInvariantException($"duplicate campaign node \"{id}\"");
        }
        if (!nodes.Contains(Text(definition["start"])))
            throw new CampaignInvariantException($"unknown campaign start node \"{Display(definition, "start")}\"");
        var initial = definition["initial"] as JsonObject;
        var roster = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in OptionalArray(initial?["roster"], "initial roster"))
        {
            var unit = Object(value, "campaign roster unit");
            if (!Named(unit["id"]) || !roster.Add(Text(unit["id"])))
                throw new CampaignInvariantException($"duplicate or empty roster unit \"{Display(unit, "id")}\"");
            ValidateRosterUnit(unit);
        }
        foreach (var (key, value) in OptionalObject(initial?["variables"], "initial variables"))
            if (!Named(key) || !ScenarioValue(value))
                throw new CampaignInvariantException($"campaign initial variable \"{key}\" is invalid");
        ValidateNamedNumbers(initial?["resources"], "initial resources");
        ValidateNamedNumbers(initial?["relations"], "initial relations");
    }

    public static JsonObject CreateState(JsonObject definition)
    {
        ValidateDefinition(definition);
        var initial = definition["initial"] as JsonObject;
        var roster = new JsonObject();
        foreach (var value in OptionalArray(initial?["roster"], "initial roster"))
        {
            var seed = Object(value, "campaign roster unit");
            var unit = (JsonObject)seed.DeepClone();
            unit["disposition"] = "available";
            unit["hpRatio"] = 1;
            unit["moraleRatio"] = 1;
            roster[Text(seed["id"])] = unit;
        }
        return new JsonObject
        {
            ["definitionId"] = definition["id"]!.DeepClone(),
            ["definitionVersion"] = definition["version"]!.DeepClone(),
            ["currentNode"] = definition["start"]!.DeepClone(),
            ["status"] = "active",
            ["flags"] = Unique(initial?["flags"]),
            ["variables"] = OptionalObject(initial?["variables"], "initial variables").DeepClone(),
            ["resources"] = OptionalObject(initial?["resources"], "initial resources").DeepClone(),
            ["relations"] = OptionalObject(initial?["relations"], "initial relations").DeepClone(),
            ["features"] = Unique(initial?["features"]),
            ["roster"] = roster,
            ["completedNodes"] = new JsonArray(),
            ["battleHistory"] = new JsonArray(),
            ["pendingBattle"] = null,
            ["battleSequence"] = 0,
        };
    }

    public static void ValidateState(JsonObject definition, JsonObject state)
    {
        if (!Same(state["definitionId"], definition["id"]) || !Same(state["definitionVersion"], definition["version"]))
            throw new CampaignInvariantException("campaign state does not match definition identity/version");
        var nodes = Array(definition["nodes"], "nodes").Select(value => Object(value, "campaign node")).ToArray();
        if (!nodes.Any(node => Same(node["id"], state["currentNode"])))
            throw new CampaignInvariantException($"campaign state references unknown node \"{Display(state, "currentNode")}\"");
        if (!OneOf(state["status"], "active", "completed", "failed"))
            throw new CampaignInvariantException("campaign state has invalid status");
        if (!Integer(state["battleSequence"]) || Number(state["battleSequence"]) < 0)
            throw new CampaignInvariantException("campaign battle sequence is invalid");
        foreach (var (owner, key) in new[] { ("flag", "flags"), ("feature", "features"), ("completed node", "completedNodes") })
            if (!NamesAreUnique(Array(state[key], key)))
                throw new CampaignInvariantException($"campaign state has duplicate or empty {owner}");
        var knownNodes = nodes.Select(node => Text(node["id"])).ToHashSet(StringComparer.Ordinal);
        if (Array(state["completedNodes"], "completedNodes").Any(node => !knownNodes.Contains(Text(node))))
            throw new CampaignInvariantException("campaign state references an unknown completed node");
        foreach (var (owner, key) in new[] { ("resource", "resources"), ("relation", "relations") })
            if (Object(state[key], key).Any(entry => !Named(entry.Key) || !Finite(entry.Value)))
                throw new CampaignInvariantException($"campaign state has invalid {owner} value");
        foreach (var (key, value) in Object(state["variables"], "variables"))
            if (!Named(key) || !ScenarioValue(value))
                throw new CampaignInvariantException($"campaign state variable \"{key}\" is invalid");
        var roster = Object(state["roster"], "roster");
        var initial = definition["initial"] as JsonObject;
        if (OptionalArray(initial?["roster"], "initial roster").Any(unit =>
            !roster.TryGetPropertyValue(Text(Object(unit, "campaign roster unit")["id"]), out var member) || member is null))
            throw new CampaignInvariantException("campaign state is missing a roster unit declared by the definition");
        foreach (var (id, value) in roster)
        {
            var unit = Object(value, "campaign roster unit");
            if (!Same(unit["id"], JsonValue.Create(id)))
                throw new CampaignInvariantException($"campaign roster key mismatch for \"{id}\"");
            ValidateRosterUnit(unit);
            if (!OneOf(unit["disposition"], "available", "fallen", "routed", "surrendered", "missing"))
                throw new CampaignInvariantException($"campaign roster unit \"{id}\" has invalid disposition");
            if (!Ratio(unit["hpRatio"]) || !Ratio(unit["moraleRatio"]))
                throw new CampaignInvariantException($"campaign roster unit \"{id}\" has invalid battle ratios");
        }
        var requestIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in Array(state["battleHistory"], "battleHistory"))
        {
            var record = Object(value, "campaign battle record");
            var node = nodes.FirstOrDefault(candidate => Same(candidate["id"], record["node"]));
            if (!Named(record["requestId"]) || requestIds.Contains(Text(record["requestId"])) ||
                node is null || !OneOf(node["type"], "battle") || !Same(record["level"], node["level"]) ||
                !OneOf(record["outcome"], "victory", "defeat", "retreat") || !Integer(record["turns"]) ||
                Number(record["turns"]) < 0 || Array(record["signals"], "battle signals").Any(signal => !Named(signal)))
                throw new CampaignInvariantException($"campaign battle record \"{Display(record, "requestId")}\" is invalid");
            requestIds.Add(Text(record["requestId"]));
        }
        var current = nodes.FirstOrDefault(node => Same(node["id"], state["currentNode"]));
        if (state["pendingBattle"] is { } pendingValue)
        {
            var pending = Object(pendingValue, "campaign pending battle");
            if (!Same(pending["node"], state["currentNode"]) || current is null || !OneOf(current["type"], "battle") ||
                !Same(pending["level"], current["level"]) || !Named(pending["requestId"]) || !Named(pending["level"]) ||
                requestIds.Contains(Text(pending["requestId"])))
                throw new CampaignInvariantException("campaign pending battle does not match current node");
        }
    }

    private static void ValidateRosterUnit(JsonObject unit)
    {
        var id = Display(unit, "id");
        if (!Named(unit["id"]) || !Named(unit["unitType"]) || !Integer(unit["owner"]) || Number(unit["owner"]) < 1)
            throw new CampaignInvariantException($"campaign roster unit \"{id}\" has invalid identity");
        if (unit.ContainsKey("rank") && !(NumberEquals(unit["rank"], 0) || NumberEquals(unit["rank"], 1) || NumberEquals(unit["rank"], 2)) ||
            unit.ContainsKey("rankProgress") && (!Finite(unit["rankProgress"]) || Number(unit["rankProgress"]) < 0))
            throw new CampaignInvariantException($"campaign roster unit \"{id}\" has invalid rank progress");
        foreach (var (resourceId, value) in OptionalObject(unit["resources"], "roster resources"))
        {
            var account = value as JsonObject;
            if (!Named(resourceId) || account is null || !Finite(account["current"]) || Number(account["current"]) < 0 ||
                (!account.ContainsKey("capacity") || account["capacity"] is not null &&
                    (!Finite(account["capacity"]) || Number(account["capacity"]) < Number(account["current"]))))
                throw new CampaignInvariantException($"campaign roster unit \"{id}\" has invalid resource \"{resourceId}\"");
        }
        foreach (var key in new[] { "unlockedCareers", "learnedAbilities", "tags" })
            if (unit[key] is { } names && !NamesAreUnique(Array(names, key)))
                throw new CampaignInvariantException($"campaign roster unit \"{id}\" has duplicate or empty names");
        ValidateNamedNumbers(unit["careerMastery"], $"roster unit \"{id}\" career mastery");
    }

    private static void ValidateNamedNumbers(JsonNode? values, string owner)
    {
        if (OptionalObject(values, owner).Any(entry => !Named(entry.Key) || !Finite(entry.Value)))
            throw new CampaignInvariantException($"campaign {owner} contains an invalid entry");
    }

    private static JsonArray Unique(JsonNode? values)
    {
        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in OptionalArray(values, "campaign names"))
            if (seen.Add(Text(value))) result.Add(value!.DeepClone());
        return result;
    }

    private static bool NamesAreUnique(JsonArray values)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value => Named(value) && names.Add(Text(value)));
    }

    internal static bool Named(string value) => value.Trim(Whitespace).Length != 0;
    private static bool Named(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && Named(text);
    internal static bool ScenarioValue(JsonNode? value) => value is JsonValue scalar &&
        (scalar.TryGetValue<string>(out _) || scalar.TryGetValue<bool>(out _) || Finite(value));
    internal static bool Finite(JsonNode? value) => TryNumber(value, out _);
    internal static bool Integer(JsonNode? value) => TryNumber(value, out var number) && number == Math.Truncate(number);
    private static bool Ratio(JsonNode? value) => TryNumber(value, out var number) && number >= 0 && number <= 1;
    private static bool NumberEquals(JsonNode? value, double expected) => TryNumber(value, out var number) && number == expected;
    internal static bool Same(JsonNode? first, JsonNode? second) =>
        TryNumber(first, out var firstNumber) && TryNumber(second, out var secondNumber)
            ? firstNumber == secondNumber : Empire.Core.Determinism.EcmaJson.DeepEquals(first, second);
    private static bool OneOf(JsonNode? value, params string[] choices) =>
        value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && choices.Contains(text, StringComparer.Ordinal);
    private static bool TryNumber(JsonNode? value, out double number)
    {
        number = 0;
        if (value is not JsonValue scalar) return false;
        if (scalar.TryGetValue<double>(out number)) return double.IsFinite(number);
        if (scalar.TryGetValue<float>(out var single))
        {
            number = single;
            return float.IsFinite(single);
        }
        return scalar.GetValueKind() == JsonValueKind.Number &&
            double.TryParse(scalar.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }
    internal static double Number(JsonNode? value) => TryNumber(value, out var number) ? number :
        throw new CampaignInvariantException("campaign value must be a finite number");
    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text :
        throw new CampaignInvariantException("campaign value must be a string");
    private static string Display(JsonObject value, string key) => !value.ContainsKey(key) ? "undefined" : value[key]?.ToString() ?? "null";
    private static JsonObject Object(JsonNode? value, string subject) => value as JsonObject ??
        throw new CampaignInvariantException($"campaign {subject} must be an object");
    private static JsonArray Array(JsonNode? value, string subject) => value as JsonArray ??
        throw new CampaignInvariantException($"campaign {subject} must be an array");
    private static JsonObject OptionalObject(JsonNode? value, string subject) => value is null ? new JsonObject() : Object(value, subject);
    private static JsonArray OptionalArray(JsonNode? value, string subject) => value is null ? new JsonArray() : Array(value, subject);
}
