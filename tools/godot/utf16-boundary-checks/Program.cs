using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Encodings.Web;
using System.Text;

static string Units(string value) => string.Join(" ", value.Select(c => ((int)c).ToString("x4")));
static void Probe(string name, Func<string> test)
{
    try
    {
        Console.WriteLine(name + ": " + test());
    }
    catch (Exception e) { Console.WriteLine(name + ": THROWS " + e.GetType().Name + ": " + e.Message); }
}
if (args.Length == 2) { CanonicalChecks.Run(args[0], args[1]); return; }
var raw = "\"\\ud800\"";
var parsed = JsonNode.Parse(raw)!;
var literal = JsonValue.Create("\ud800")!;
var obj = new JsonObject { ["\ud800"] = literal };
var parsedObj = JsonNode.Parse("{\"\\ud800\":\"\\udfff\"}")!.AsObject();
var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
Probe("parsed scalar GetValue", () => Units(parsed.GetValue<string>()));
Probe("parsed scalar TryGetValue", () => parsed.AsValue().TryGetValue<string>(out var s) ? Units(s) : "false");
Probe("parsed scalar ToJsonString", () => parsed.ToJsonString());
Probe("parsed scalar DeepClone ToJsonString", () => parsed.DeepClone().ToJsonString());
Probe("literal scalar GetValue", () => Units(literal.GetValue<string>()));
Probe("literal scalar ToJsonString", () => literal.ToJsonString());
Probe("literal scalar relaxed ToJsonString", () => literal.ToJsonString(options));
Probe("literal scalar DeepClone GetValue", () => Units(literal.DeepClone().GetValue<string>()));
Probe("literal object key", () => string.Join("|", obj.Select(p => Units(p.Key))));
Probe("literal object index", () => Units(obj["\ud800"]!.GetValue<string>()));
Probe("literal object ToJsonString", () => obj.ToJsonString());
Probe("literal object DeepClone key", () => string.Join("|", obj.DeepClone().AsObject().Select(p => Units(p.Key))));
Probe("parsed object Count", () => parsedObj.Count.ToString());
Probe("parsed object index", () => Units(parsedObj["\ud800"]!.GetValue<string>()));
Probe("parsed object ToJsonString", () => parsedObj.ToJsonString());
Probe("parsed object DeepClone ToJsonString", () => parsedObj.DeepClone().ToJsonString());
var convertOptions = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
convertOptions.Converters.Add(new CodeUnitStringConverter());
var custom = JsonValue.Create("\ud800", (JsonTypeInfo<string>)convertOptions.GetTypeInfo(typeof(string)))!;
Probe("custom scalar GetValue", () => Units(custom.GetValue<string>()));
Probe("custom scalar ToJsonString", () => custom.ToJsonString());
Probe("custom scalar DeepClone GetValue", () => Units(custom.DeepClone().GetValue<string>()));
Probe("custom scalar DeepClone ToJsonString", () => custom.DeepClone().ToJsonString());
Probe("custom object key ToJsonString", () => new JsonObject { ["\ud800"] = custom }.ToJsonString(convertOptions));
Probe("reader raw property bytes", () => { var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("{\"\\ud800\":1}")); reader.Read(); reader.Read(); return Encoding.UTF8.GetString(reader.ValueSpan); });
Probe("writer raw scalar", () => { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) { writer.WriteRawValue(raw, skipInputValidation: false); } return Encoding.UTF8.GetString(stream.ToArray()); });

Probe("DeepEquals lone vs replacement", () => JsonNode.DeepEquals(JsonValue.Create("\ud800"), JsonValue.Create("\ufffd")).ToString());
Probe("DeepEquals high vs low", () => JsonNode.DeepEquals(JsonValue.Create("\ud800"), JsonValue.Create("\udfff")).ToString());
Probe("DeepEquals lone object vs replacement object", () => JsonNode.DeepEquals(new JsonObject { ["\ud800"] = 1 }, new JsonObject { ["\ufffd"] = 1 }).ToString());
Probe("DeepEquals literal vs parsed escaped lone", () => JsonNode.DeepEquals(literal, parsed).ToString());
Probe("Parse direct lone scalar", () => Units(JsonNode.Parse("\"\ud800\"")!.GetValue<string>()));
Probe("Parsed object GetValue<JsonElement>", () => parsedObj.GetValue<JsonElement>().GetRawText());

sealed class CodeUnitStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotImplementedException();
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteRawValue("\"" + string.Concat(value.Select(c => "\\u" + ((int)c).ToString("x4"))) + "\"");
}
