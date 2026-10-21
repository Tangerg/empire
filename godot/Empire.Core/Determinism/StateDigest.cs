using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Empire.Core.Determinism;

public static class StateDigest
{
    public static string Hash(JsonNode state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var text = new StringBuilder();
        AppendCanonical(text, state);
        return Utf16Hash.Hash(text.ToString()).ToString("x8", CultureInfo.InvariantCulture);
    }

    private static void AppendCanonical(StringBuilder text, JsonNode? value)
    {
        switch (value)
        {
            case null:
                text.Append("null");
                return;
            case JsonArray array:
                text.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index != 0)
                        text.Append(',');
                    AppendCanonical(text, array[index]);
                }
                text.Append(']');
                return;
            case JsonObject obj:
                // Pair arrays preserve ordinal key ordering even for numeric-looking keys;
                // JSON object serialization would apply its own property ordering.
                text.Append('[');
                var first = true;
                foreach (var property in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!first)
                        text.Append(',');
                    first = false;
                    text.Append('[');
                    EcmaJson.AppendString(text, property.Key);
                    text.Append(',');
                    AppendCanonical(text, property.Value);
                    text.Append(']');
                }
                text.Append(']');
                return;
            case JsonValue primitive:
                AppendPrimitive(text, primitive);
                return;
            default:
                throw new DomainInvariantException("state digest requires plain JSON values");
        }
    }

    private static void AppendPrimitive(StringBuilder text, JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element)
            && element.ValueKind == JsonValueKind.String)
        {
            // GetString rejects escaped lone surrogates, but JSON.stringify
            // preserves them. Decode the already-validated token as code units.
            EcmaJson.AppendString(text, EcmaJson.DecodeStringToken(element.GetRawText()));
            return;
        }
        if (value.TryGetValue<string>(out var characters))
        {
            EcmaJson.AppendString(text, characters);
            return;
        }
        if (value.TryGetValue<bool>(out var boolean))
        {
            text.Append(boolean ? "true" : "false");
            return;
        }
        if (value.TryGetValue<double>(out var number))
        {
            text.Append(EcmaNumber.Format(number));
            return;
        }
        if (value.TryGetValue<float>(out var single))
        {
            text.Append(EcmaNumber.Format(single));
            return;
        }

        // CLR integer/decimal JsonValues must cross the same binary64 boundary
        // as JSON.parse in the original engine, rather than retaining CLR precision.
        if (value.GetValueKind() == JsonValueKind.Number
            && double.TryParse(value.ToJsonString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number))
        {
            text.Append(EcmaNumber.Format(number));
            return;
        }
        throw new DomainInvariantException("state digest requires plain JSON scalar values");
    }

}
