using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

internal enum StoredKind
{
    Object, Array, String, Number, Integer, Boolean, NullableObject, NullableNumber
}

internal static class StoredSchema
{
    internal static JsonObject Read(string subject, int schema, JsonNode? raw)
    {
        if (raw is not JsonObject original)
            throw new StoredDocumentException($"{subject} must be an object");
        var value = (JsonObject)original.DeepClone();
        if (!Matches(value["schema"], StoredKind.Integer))
            throw new StoredDocumentException($"{subject} has invalid schema");
        if (StateJson.Number(value["schema"], "schema") != schema)
            throw new StoredDocumentException($"unsupported {subject} schema {value["schema"]!.ToJsonString()}");
        return value;
    }

    internal static void Require(JsonNode? value, string owner, params (string Field, StoredKind Kind)[] shape)
    {
        if (value is not JsonObject aggregate)
            Reject($"{owner}缺失或损坏，存档内容不是一场战斗");
        else
            foreach (var (field, kind) in shape)
                if (!aggregate.ContainsKey(field) || !Matches(aggregate[field], kind))
                    Reject($"{owner}的「{field}」缺失或损坏，存档内容不是一场战斗");
    }

    internal static bool Matches(JsonNode? value, StoredKind kind)
    {
        if (value is null)
            return kind is StoredKind.NullableObject or StoredKind.NullableNumber;
        if (value is JsonValue scalar)
        {
            if (scalar.TryGetValue<double>(out var direct))
                return Numeric(direct, kind);
            if (scalar.TryGetValue<float>(out var single))
                return Numeric(single, kind);
        }
        return kind switch
        {
            StoredKind.Object or StoredKind.NullableObject => value is JsonObject,
            StoredKind.Array => value is JsonArray,
            StoredKind.String => value.GetValueKind() == JsonValueKind.String,
            StoredKind.Boolean => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            StoredKind.Number or StoredKind.NullableNumber or StoredKind.Integer =>
                value.GetValueKind() == JsonValueKind.Number &&
                double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                double.IsFinite(number) && (kind != StoredKind.Integer || number == Math.Truncate(number)),
            _ => false,
        };
    }

    private static bool Numeric(double number, StoredKind kind) =>
        kind is StoredKind.Number or StoredKind.NullableNumber or StoredKind.Integer &&
        double.IsFinite(number) && (kind != StoredKind.Integer || number == Math.Truncate(number));

    internal static bool ValidTimestamp(string text) => DateTimeOffset.TryParse(text,
        CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out _);

    internal static void Reject(string message) => throw new StoredDocumentException($"战斗存档无法读取：{message}");
}
