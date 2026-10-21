using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Validation;

/// <summary>Structural authored-document checks, independent of runtime payload semantics.</summary>
internal static class RulePayloadReferences
{
    internal static IEnumerable<JsonObject> DeclaredObjectiveChildren(JsonObject objective) => objective["objectives"] is JsonArray children
        ? children.Select(child => StateJson.Object(child, "objective"))
        : objective["objective"] is JsonObject child ? new[] { child } : Array.Empty<JsonObject>();

    internal static IEnumerable<string> ScheduleFaults(JsonObject trigger)
    {
        if (trigger["repeat"] is not JsonObject repeat)
            yield break;
        if (!Whole(repeat["everyRounds"], 1))
            yield return "循环间隔必须是正整数";
        if (repeat.ContainsKey("startTurn") && Number(repeat["startTurn"]) < 1)
            yield return "起始回合必须 >= 1";
        if (repeat.ContainsKey("endTurn") && repeat.ContainsKey("startTurn") &&
            Number(repeat["endTurn"]) < Number(repeat["startTurn"]))
            yield return "结束回合早于起始回合";
        if (repeat.ContainsKey("maxFirings") && !Whole(repeat["maxFirings"], 1))
            yield return "最大触发次数必须是正整数";
    }

    internal static double Number(JsonNode? value)
    {
        if (value is not JsonValue scalar || scalar.GetValueKind() != System.Text.Json.JsonValueKind.Number)
            return double.NaN;
        return double.TryParse(scalar.ToJsonString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }
    internal static bool Blank(string value) => value.All(character => character is
        '\u0009' or '\u000a' or '\u000b' or '\u000c' or '\u000d' or '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff');
    private static bool Whole(JsonNode? value, double minimum = double.NegativeInfinity)
    {
        var number = Number(value);
        return double.IsFinite(number) && number == Math.Truncate(number) && number >= minimum;
    }
}
