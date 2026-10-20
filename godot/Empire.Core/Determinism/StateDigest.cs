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
        AppendCanonical(text, state, new List<string>());
        return Utf16Hash.Hash(text.ToString()).ToString("x8", CultureInfo.InvariantCulture);
    }

    private static void AppendCanonical(StringBuilder text, JsonNode? value, List<string> path)
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
                    if (index != 0) text.Append(',');
                    path.Add(index.ToString(CultureInfo.InvariantCulture));
                    AppendCanonical(text, array[index], path);
                    path.RemoveAt(path.Count - 1);
                }
                text.Append(']');
                return;
            case JsonObject obj:
                // The TypeScript canonical form is an array of [key, value]
                // pairs, not a JSON object with sorted property insertion order.
                text.Append('[');
                var first = true;
                foreach (var property in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (IgnoredPath(path, property.Key)) continue;
                    if (!first) text.Append(',');
                    first = false;
                    text.Append('[');
                    AppendString(text, property.Key);
                    text.Append(',');
                    path.Add(property.Key);
                    AppendCanonical(text, property.Value, path);
                    path.RemoveAt(path.Count - 1);
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

    private static bool IgnoredPath(IReadOnlyList<string> path, string key)
    {
        if (path.Count == 0 && key == "levelName") return true;
        return path.Count == 2 && path[0] == "players"
            && path[1].Length != 0 && path[1].All(character => character is >= '0' and <= '9')
            && key == "controller";
    }

    private static void AppendPrimitive(StringBuilder text, JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element)
            && element.ValueKind == JsonValueKind.String)
        {
            // GetString rejects escaped lone surrogates, but JSON.stringify
            // preserves them. Decode the already-validated token as code units.
            AppendString(text, DecodeStringToken(element.GetRawText()));
            return;
        }
        if (value.TryGetValue<string>(out var characters))
        {
            AppendString(text, characters);
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

    private static string DecodeStringToken(string token)
    {
        var value = new StringBuilder(token.Length - 2);
        for (var index = 1; index < token.Length - 1; index++)
        {
            var codeUnit = token[index];
            if (codeUnit != '\\')
            {
                value.Append(codeUnit);
                continue;
            }

            switch (token[++index])
            {
                case '"': value.Append('"'); break;
                case '\\': value.Append('\\'); break;
                case '/': value.Append('/'); break;
                case 'b': value.Append('\b'); break;
                case 'f': value.Append('\f'); break;
                case 'n': value.Append('\n'); break;
                case 'r': value.Append('\r'); break;
                case 't': value.Append('\t'); break;
                case 'u':
                    value.Append((char)ushort.Parse(token.AsSpan(index + 1, 4),
                        NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                    index += 4;
                    break;
                default:
                    throw new DomainInvariantException("state digest encountered an invalid JSON string escape");
            }
        }
        return value.ToString();
    }

    private static void AppendString(StringBuilder text, string value)
    {
        text.Append('"');
        for (var index = 0; index < value.Length; index++)
        {
            var codeUnit = value[index];
            switch (codeUnit)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\f': text.Append("\\f"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                default:
                    if (codeUnit < 0x20 || char.IsLowSurrogate(codeUnit)
                        || (char.IsHighSurrogate(codeUnit)
                            && (index + 1 == value.Length || !char.IsLowSurrogate(value[index + 1]))))
                    {
                        text.Append("\\u");
                        text.Append(((int)codeUnit).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        // JSON.stringify leaves non-ASCII (including U+2028/29)
                        // and valid surrogate pairs intact; default JSON encoders do not.
                        text.Append(codeUnit);
                        if (char.IsHighSurrogate(codeUnit)) text.Append(value[++index]);
                    }
                    break;
            }
        }
        text.Append('"');
    }
}
