using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Engine;

/// <summary>Native level document defaults, separate from battle state construction.</summary>
public static class LevelNormalizer
{
    public const int Schema = 3;
    public static JsonObject Normalize(JsonNode? raw)
    {
        if (raw is not JsonObject source) throw new StoredDocumentException("关卡数据不是对象");
        var loaded = (JsonObject)source.DeepClone();
        if (loaded["schema"] is not JsonValue schema || StateJson.Number(schema, "schema") != Schema)
            throw new StoredDocumentException($"Unsupported level schema: expected {Schema}, received {loaded["schema"]}.");
        if (loaded.ContainsKey("name") || loaded.ContainsKey("description") || loaded.ContainsKey("levelName"))
            throw new StoredDocumentException("Level wording belongs to presentation metadata, not gameplay definitions.");
        if (loaded["terrain"] is not JsonArray terrain) throw new StoredDocumentException("缺少 terrain");
        var height = loaded["height"]?.DeepClone() ?? JsonValue.Create(terrain.Count)!;
        var width = loaded["width"]?.DeepClone() ?? JsonValue.Create(terrain.FirstOrDefault()?.GetValue<string>().Length ?? 0)!;
        var players = loaded["players"]?.DeepClone() ?? DefaultPlayers();
        foreach (var player in StateJson.Objects(players, "players"))
            if (player["resources"] is not JsonObject)
                throw new StoredDocumentException($"玩家 {player["id"]?.ToJsonString() ?? "?"} 缺少 resources；旧资金/指挥点字段已停止支持");
        var elevation = new JsonArray();
        if (loaded["elevation"] is JsonArray values)
            foreach (var value in values) elevation.Add(ToNumber(value));
        else
            for (var index = 0; index < StateJson.Integer(width, "width") * StateJson.Integer(height, "height"); index++) elevation.Add(0);
        var result = new JsonObject
        {
            ["schema"] = Schema, ["id"] = loaded["id"]?.DeepClone() ?? JsonValue.Create("untitled"),
            ["width"] = width, ["height"] = height, ["terrain"] = terrain.DeepClone(), ["elevation"] = elevation,
            ["cliffs"] = loaded["cliffs"] is JsonArray cliffs ? cliffs.DeepClone() : new JsonArray(),
            ["directionalCover"] = loaded["directionalCover"] is JsonArray cover ? cover.DeepClone() : new JsonArray(),
            ["players"] = players, ["rules"] = loaded["rules"]?.DeepClone() ?? new JsonObject(),
            ["victory"] = loaded["victory"]?.DeepClone() ?? StateDefaults.Victory(),
        };
        foreach (var name in new[] { "owners", "units", "commanders", "structures", "composites" })
            result[name] = loaded[name]?.DeepClone() ?? new JsonArray();
        foreach (var name in new[] { "author", "scenario", "deployment", "extra" })
            StateJson.CopyIfPresent(loaded, result, name);
        return result;
    }

    public static JsonArray DefaultPlayers() => new()
    {
        DefaultPlayer(1, "#3f7fd8", "human"), DefaultPlayer(2, "#d8483f", "ai")
    };

    private static JsonObject DefaultPlayer(int id, string color, string controller) => new()
    {
        ["id"] = id, ["team"] = id, ["color"] = color, ["controller"] = controller,
        ["resources"] = new JsonObject
        {
            ["funds"] = new JsonObject { ["current"] = 0, ["capacity"] = null },
            ["command_points"] = new JsonObject { ["current"] = 0, ["capacity"] = 5 },
        },
        ["ai"] = new JsonObject { ["aggression"] = 0.5 },
    };

    private static double ToNumber(JsonNode? value)
    {
        if (value is null) return 0;
        if (value is JsonValue scalar)
        {
            if (scalar.TryGetValue<bool>(out var boolean)) return boolean ? 1 : 0;
            if (scalar.TryGetValue<string>(out var text))
            {
                text = text.Trim();
                if (text.Length == 0) return 0;
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)) return parsed;
                throw new StoredDocumentException("elevation must normalize to finite numbers");
            }
        }
        return StateJson.Number(value, "elevation");
    }
}
