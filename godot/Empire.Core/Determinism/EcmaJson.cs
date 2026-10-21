using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Empire.Core.Determinism;

/// <summary>
/// The JavaScript-compatible JSON text boundary. Strings and object keys are UTF-16
/// code units, not Unicode scalar values. System.Text.Json's default string writer
/// and DeepEquals replace lone surrogates, so canonical state must cross this boundary.
/// Numeric values use the same finite binary64 representation as the state digest.
/// </summary>
public static class EcmaJson
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static JsonNode? Parse(string json, bool rejectDuplicateProperties = false)
    {
        ArgumentNullException.ThrowIfNull(json);
        // JSON.parse also accepts a literal unpaired code unit in a CLR/JS string.
        // Escape before UTF-8 transcoding, which would otherwise replace that unit.
        var bytes = StrictUtf8.GetBytes(EscapeLoneUnits(json));
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read())
            throw new JsonException("Expected a JSON value.");
        var value = ReadValue(ref reader, rejectDuplicateProperties);
        if (reader.Read())
            throw new JsonException("Only one top-level JSON value is permitted.");
        return value;
    }

    public static string Stringify(JsonNode? value)
    {
        var text = new StringBuilder();
        AppendValue(text, value);
        return text.ToString();
    }

    public static string ReadString(JsonNode? value)
    {
        if (value is JsonValue scalar)
        {
            if (scalar.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.String)
                return DecodeStringToken(element.GetRawText());
            if (scalar.TryGetValue<string>(out var text))
                return text;
        }
        throw new DomainInvariantException("JSON value must be a string");
    }

    /// <summary>
    /// Creates an eager, detached canonical tree. An already-lazy JsonObject with
    /// lone-surrogate keys cannot expose its raw tokens through public JsonNode APIs;
    /// reject it explicitly and require Parse(originalText), rather than repair data.
    /// </summary>
    public static JsonNode? Normalize(JsonNode? value) => Parse(Stringify(value));

    /// <summary>Preserves escaped raw code units for immutable snapshots. Read them using this boundary.</summary>
    public static JsonElement ToElement(JsonNode? value)
    {
        using var document = JsonDocument.Parse(Stringify(value));
        return document.RootElement.Clone();
    }

    /// <summary>Reads an immutable object's UTF-16 key without transcoding an unpaired code unit.</summary>
    public static bool TryGetProperty(JsonElement value, string key, out JsonElement property)
    {
        ArgumentNullException.ThrowIfNull(key);
        for (var index = 0; index < key.Length; index++)
        {
            if (!char.IsSurrogate(key[index]))
                continue;
            if (char.IsHighSurrogate(key[index]) && index + 1 < key.Length && char.IsLowSurrogate(key[index + 1]))
            {
                index++;
                continue;
            }

            // JsonElement's string-key lookup requires valid UTF-8. The canonical
            // JSON model permits every UTF-16 key, so decode those objects here.
            var obj = Parse(value.GetRawText())!.AsObject();
            if (obj.TryGetPropertyValue(key, out var node))
            {
                property = ToElement(node);
                return true;
            }
            property = default;
            return false;
        }
        return value.TryGetProperty(key, out property);
    }

    public static bool DeepEquals(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (left is JsonObject leftObject && right is JsonObject rightObject)
        {
            var first = Properties(leftObject);
            var second = Properties(rightObject).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return first.Length == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && DeepEquals(pair.Value, value));
        }
        if (left is JsonArray leftArray && right is JsonArray rightArray)
            return leftArray.Count == rightArray.Count && leftArray.Select((value, index) => DeepEquals(value, rightArray[index])).All(value => value);
        if (left is not JsonValue a || right is not JsonValue b || a.GetValueKind() != b.GetValueKind())
            return false;
        return a.GetValueKind() switch
        {
            JsonValueKind.String => StringComparer.Ordinal.Equals(ReadString(a), ReadString(b)),
            JsonValueKind.Number => Number(a) == Number(b),
            JsonValueKind.True or JsonValueKind.False => a.GetValue<bool>() == b.GetValue<bool>(),
            JsonValueKind.Null => true,
            _ => throw new DomainInvariantException("Canonical equality requires plain JSON values"),
        };
    }

    private static JsonNode? ReadValue(ref Utf8JsonReader reader, bool rejectDuplicateProperties)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var obj = new JsonObject();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return obj;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("Expected an object property.");
                    var name = ReadToken(ref reader);
                    if (!reader.Read())
                        throw new JsonException("Expected a property value.");
                    if (rejectDuplicateProperties && obj.ContainsKey(name))
                        throw new JsonException($"Duplicate JSON property: {name}.");
                    // JSON.parse gives the last duplicate its value, retaining the key's insertion position.
                    obj[name] = ReadValue(ref reader, rejectDuplicateProperties);
                }
                throw new JsonException("Unterminated JSON object.");
            case JsonTokenType.StartArray:
                var array = new JsonArray();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        return array;
                    array.Add(ReadValue(ref reader, rejectDuplicateProperties));
                }
                throw new JsonException("Unterminated JSON array.");
            case JsonTokenType.String:
                return JsonValue.Create(ReadToken(ref reader));
            case JsonTokenType.Number:
                var number = reader.GetDouble();
                if (!double.IsFinite(number))
                    throw new JsonException("JSON number is outside the finite binary64 range.");
                // Keep JsonElement-backed numeric access (GetValue<int>, double, etc.) while
                // rounding through binary64 exactly once, as JSON.parse does.
                return JsonValue.Create(JsonSerializer.SerializeToElement(number));
            case JsonTokenType.True:
                return JsonValue.Create(true);
            case JsonTokenType.False:
                return JsonValue.Create(false);
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException("Expected a JSON value.");
        }
    }

    private static string ReadToken(ref Utf8JsonReader reader) =>
        DecodeStringToken("\"" + StrictUtf8.GetString(reader.ValueSpan) + "\"");

    private static KeyValuePair<string, JsonNode?>[] Properties(JsonObject obj)
    {
        try
        {
            return obj.ToArray();
        }
        catch (InvalidOperationException error)
        {
            throw new DomainInvariantException("Cannot materialize this System.Text.Json lazy object without losing UTF-16 keys. Use EcmaJson.Parse on its original JSON text.", error);
        }
    }

    private static void AppendValue(StringBuilder text, JsonNode? value)
    {
        switch (value)
        {
            case null:
                text.Append("null");
                return;
            case JsonObject obj:
                text.Append('{');
                var first = true;
                // JavaScript enumerates array-index keys first, then other strings in insertion order.
                foreach (var property in Properties(obj).OrderBy(pair => ArrayIndex(pair.Key)))
                {
                    if (!first)
                        text.Append(',');
                    first = false;
                    AppendString(text, property.Key);
                    text.Append(':');
                    AppendValue(text, property.Value);
                }
                text.Append('}');
                return;
            case JsonArray array:
                text.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index != 0)
                        text.Append(',');
                    AppendValue(text, array[index]);
                }
                text.Append(']');
                return;
            case JsonValue scalar:
                switch (scalar.GetValueKind())
                {
                    case JsonValueKind.String:
                        AppendString(text, ReadString(scalar));
                        return;
                    case JsonValueKind.Number:
                        text.Append(EcmaNumber.Format(Number(scalar)));
                        return;
                    case JsonValueKind.True:
                        text.Append("true");
                        return;
                    case JsonValueKind.False:
                        text.Append("false");
                        return;
                    case JsonValueKind.Null:
                        text.Append("null");
                        return;
                }
                break;
        }
        throw new DomainInvariantException("Canonical serialization requires plain JSON values");
    }

    private static ulong ArrayIndex(string key) => uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
        && index != uint.MaxValue && index.ToString(CultureInfo.InvariantCulture) == key ? index : ulong.MaxValue;

    private static double Number(JsonValue value)
    {
        double number;
        if (value.TryGetValue<double>(out number))
        {
        }
        else if (value.TryGetValue<float>(out var single))
            number = single;
        else if (!double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            throw new DomainInvariantException("Canonical JSON requires a binary64 number");
        if (!double.IsFinite(number))
            throw new DomainInvariantException("Canonical JSON cannot contain a non-finite number");
        return number;
    }

    private static string EscapeLoneUnits(string json)
    {
        StringBuilder? escaped = null;
        for (var index = 0; index < json.Length; index++)
        {
            var unit = json[index];
            if (char.IsHighSurrogate(unit) && index + 1 < json.Length && char.IsLowSurrogate(json[index + 1]))
            {
                if (escaped is not null)
                {
                    escaped.Append(unit);
                    escaped.Append(json[index + 1]);
                }
                index++;
            }
            else if (char.IsSurrogate(unit))
            {
                // Escaping an invalid character after a single JSON backslash would
                // accidentally turn malformed input into a valid escaped backslash.
                var precedingSlashes = 0;
                for (var previous = index - 1; previous >= 0 && json[previous] == '\\'; previous--)
                    precedingSlashes++;
                if (precedingSlashes % 2 != 0)
                    throw new JsonException("Invalid escape before an unpaired UTF-16 code unit.");
                escaped ??= new StringBuilder(json.Length + 8).Append(json.AsSpan(0, index));
                escaped.Append("\\u").Append(((int)unit).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
                escaped?.Append(unit);
        }
        return escaped?.ToString() ?? json;
    }

    internal static string DecodeStringToken(string token)
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
                case '"':
                    value.Append('"');
                    break;
                case '\\':
                    value.Append('\\');
                    break;
                case '/':
                    value.Append('/');
                    break;
                case 'b':
                    value.Append('\b');
                    break;
                case 'f':
                    value.Append('\f');
                    break;
                case 'n':
                    value.Append('\n');
                    break;
                case 'r':
                    value.Append('\r');
                    break;
                case 't':
                    value.Append('\t');
                    break;
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

    internal static void AppendString(StringBuilder text, string value)
    {
        text.Append('"');
        for (var index = 0; index < value.Length; index++)
        {
            var codeUnit = value[index];
            switch (codeUnit)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\b':
                    text.Append("\\b");
                    break;
                case '\f':
                    text.Append("\\f");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
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
                        if (char.IsHighSurrogate(codeUnit))
                            text.Append(value[++index]);
                    }
                    break;
            }
        }
        text.Append('"');
    }
}
