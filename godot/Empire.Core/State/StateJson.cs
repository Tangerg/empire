using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Empire.Core.State;

internal static class StateJson
{
    internal static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? Empire.Core.Determinism.EcmaJson.DecodeStringToken(value.GetRawText())
        : throw new DomainInvariantException("JSON value must be a string");

    internal static string Text(JsonElement value, string key) => Text(value.GetProperty(key));

    internal static double Number(JsonElement value, string subject)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result) && double.IsFinite(result))
            return result;
        throw new DomainInvariantException($"{subject} must be a finite number");
    }

    internal static double Number(JsonElement value, string key, double fallback) =>
        value.TryGetProperty(key, out var property) && property.ValueKind != JsonValueKind.Null
            ? Number(property, key) : fallback;

    internal static int Integer(JsonElement value, string key)
    {
        var number = Number(value.GetProperty(key), key);
        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
            throw new DomainInvariantException($"{key} must be an integer");
        return (int)number;
    }

    internal static bool IsTrue(JsonElement value) => value.ValueKind == JsonValueKind.True;

    internal static JsonObject Object(JsonNode? value, string subject) =>
        value as JsonObject ?? throw new DomainInvariantException($"{subject} must be an object");

    internal static JsonArray Array(JsonNode? value, string subject) =>
        value as JsonArray ?? throw new DomainInvariantException($"{subject} must be an array");

    internal static IEnumerable<JsonObject> Objects(JsonNode? value, string subject) =>
        value is null ? Enumerable.Empty<JsonObject>() :
        Array(value, subject).Select((entry, index) => Object(entry, $"{subject}[{index}]"));

    internal static JsonNode Required(JsonObject value, string key) =>
        value[key] ?? throw new DomainInvariantException($"missing {key}");

    internal static string Text(JsonNode? value, string subject)
    {
        if (value is JsonValue scalar && scalar.GetValueKind() == System.Text.Json.JsonValueKind.String)
            return Empire.Core.Determinism.EcmaJson.ReadString(scalar);
        throw new DomainInvariantException($"{subject} must be a string");
    }

    internal static string Text(JsonObject value, string key) => Text(value[key], key);

    internal static double Number(JsonNode? value, string subject)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<double>(out var result) && double.IsFinite(result))
            return result;
        // JsonValue created in code retains its CLR numeric type, unlike parsed JSON.
        if (value is JsonValue number && number.GetValueKind() == System.Text.Json.JsonValueKind.Number &&
            double.TryParse(number.ToJsonString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out result) && double.IsFinite(result))
            return result;
        throw new DomainInvariantException($"{subject} must be a finite number");
    }

    internal static double Number(JsonObject value, string key, double fallback) =>
        value[key] is { } node ? Number(node, key) : fallback;

    internal static int Integer(JsonNode? value, string subject)
    {
        var number = Number(value, subject);
        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
            throw new DomainInvariantException($"{subject} must be an integer");
        return (int)number;
    }

    internal static int Integer(JsonObject value, string key) => Integer(value[key], key);

    internal static bool IsTrue(JsonNode? value) =>
        value is JsonValue scalar && scalar.TryGetValue<bool>(out var result) && result;

    internal static bool IsFalse(JsonNode? value) =>
        value is JsonValue scalar && scalar.TryGetValue<bool>(out var result) && !result;

    internal static JsonObject CopyObject(JsonNode? value, string subject) =>
        value is null ? new JsonObject() : (JsonObject)Object(value, subject).DeepClone();

    internal static JsonArray CopyArray(JsonNode? value, string subject) =>
        value is null ? new JsonArray() : (JsonArray)Array(value, subject).DeepClone();

    internal static void CopyIfPresent(JsonObject source, JsonObject destination, string key)
    {
        if (source.TryGetPropertyValue(key, out var value))
            destination[key] = value?.DeepClone();
    }

    internal static JsonObject MergeObjects(JsonNode? first, JsonNode? second, string subject)
    {
        var result = CopyObject(first, subject);
        if (second is not null)
            foreach (var pair in Object(second, subject))
                result[pair.Key] = pair.Value?.DeepClone();
        return result;
    }

    internal static JsonArray UniqueStrings(JsonNode? value, string subject)
    {
        var result = new JsonArray();
        if (value is null)
            return result;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Array(value, subject))
        {
            var text = Text(entry, subject);
            if (seen.Add(text))
                result.Add(text);
        }
        return result;
    }

    // JavaScript Math.round chooses the higher integer at a half, not banker's rounding.
    internal static double Round(double value)
    {
        var lower = Math.Floor(value);
        return value - lower < 0.5 ? lower : lower + 1;
    }

    internal static JsonArray Zeros(int count)
    {
        var result = new JsonArray();
        for (var index = 0; index < count; index++)
            result.Add(0);
        return result;
    }
}
