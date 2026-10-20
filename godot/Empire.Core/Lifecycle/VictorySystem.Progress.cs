using System.Globalization;
using System.Text.RegularExpressions;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public sealed record ObjectiveProgress(string Id, string Description, string Status, string Progress, string Role);

public sealed partial class VictorySystem
{
    public IReadOnlyList<ObjectiveProgress> Objectives(JsonObject state, int owner) =>
        Objects(Player(state, owner)["objectives"], "objectives")
            .Where(objective => !IsTrue(Player(state, owner)["objectiveStates"]?[Text(objective, "id")]?["hidden"]))
            .Select(objective => new ObjectiveProgress(Text(objective, "id"), objective["label"] is { } label ? Text(label, "label") : Describe(objective, extraObjectiveDescription),
                Status(state, owner, objective), Progress(state, owner, objective), Role(objective))).ToArray();

    public string Progress(JsonObject state, int owner, JsonObject objective)
    {
        var terminal = Status(state, owner, objective) switch
        {
            "completed" => "已完成", "failed" => "已失败", "inactive" => "未激活", "cancelled" => "已取消", _ => null,
        };
        if (terminal is not null) return terminal;
        bool Enemy(int id) => id != 0 && owner != 0 && Team(state, id) != Team(state, owner);
        var type = Text(objective, "type");
        switch (type)
        {
            case "routEnemies": return $"剩余敌军 {Players(state).Where(player => Enemy(Integer(player, "id"))).Sum(player => Units(state).Count(unit => Integer(unit, "owner") == Integer(player, "id")))}";
            case "captureHQ": return $"敌方城堡 {Players(state).Where(player => Enemy(Integer(player, "id"))).Sum(player => OwnedTerrains(state, Integer(player, "id")).Count(terrain => IsTrue(terrain["hq"])))}";
            case "holdAllVillages":
                var map = Object(state["map"], "map");
                var sites = Array(map["tiles"], "tiles").Select((tile, index) => (tile, index)).Where(entry => IsTrue(content.Get("terrains", Text(entry.tile, "terrain"))["capturable"])).ToArray();
                return $"{sites.Count(entry => Integer(map["owners"]![entry.index], "owner") == owner)}/{sites.Length} 据点";
            case "surviveTurns": return $"{Math.Min(Number(state["turn"], "turn"), Number(objective["turns"], "turns"))}/{Number(objective["turns"], "turns")} 回合";
            case "eliminate": return $"剩余 {scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count}";
            case "destroy": return $"剩余结构 {Array(objective["structures"], "structures").Count(id => Objects(state["structures"], "structures").Any(structure => Text(structure, "id") == Text(id, "id") && Number(structure["hp"], "hp") > 0))}";
            case "protect": return $"{scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count}/{Number(objective["minimumAlive"], "minimumAlive")} 存活";
            case "escort":
                var zone = ObjectiveZone(state, Text(objective, "zone")).ToArray();
                return $"{scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count(unit => zone.Any(cell => ScenarioSystem.SameCell(cell, unit)))}/{Number(objective["count"], "count")} 抵达";
            case "control":
                var cells = ObjectiveZone(state, Text(objective, "zone")).ToArray();
                var field = Object(state["map"], "map");
                return $"{cells.Count(cell => Integer(field["owners"]![Integer(cell, "y") * Integer(field, "width") + Integer(cell, "x")], "owner") == owner)}/{cells.Length} 区域格";
            case "score": return $"{ScoreNumber(state["scenario"]!["variables"]![Text(objective, "variable")])}/{Number(objective["atLeast"], "atLeast")}";
            case "interact": return "进行中";
            case "all": case "any": case "sequence":
                var children = Children(objective).ToArray();
                return $"{children.Count(child => Status(state, owner, child) == "completed")}/{children.Length} 阶段";
            case "optional": case "failOn": return Progress(state, owner, Object(objective["objective"], "objective"));
            default: return extraObjectiveProgress?.Invoke(state, owner, objective)
                ?? throw new DomainInvariantException($"Unsupported objective \"{type}\"");
        }
    }

    private static string ScoreNumber(JsonNode? value)
    {
        if (value is null) return "0";
        if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean)) return boolean ? "1" : "0";
        var text = value is JsonValue stringValue && stringValue.TryGetValue<string>(out var str) ? str.Trim() : value.ToJsonString();
        if (text.Length == 0) return "0";
        double number;
        if (text is "Infinity" or "+Infinity") return "Infinity";
        if (text == "-Infinity") return "-Infinity";
        if (text.Length > 2 && text[0] == '0' && "xXoObB".Contains(text[1]))
        {
            var radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            number = 0;
            foreach (var digit in text[2..])
            {
                var valueOfDigit = "0123456789abcdef".IndexOf(char.ToLowerInvariant(digit));
                if (valueOfDigit < 0 || valueOfDigit >= radix) return "NaN";
                number = number * radix + valueOfDigit;
            }
        }
        else if (!Regex.IsMatch(text, @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$") ||
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return "NaN";
        return double.IsPositiveInfinity(number) ? "Infinity" : double.IsNegativeInfinity(number) ? "-Infinity" : EcmaNumber.Format(number);
    }
}
