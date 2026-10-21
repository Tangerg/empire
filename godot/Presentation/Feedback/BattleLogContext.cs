using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Presentation.Feedback;

/// <summary>Names are projections of one committed state and its injected ruleset.</summary>
public sealed class BattleLogContext
{
    private readonly Func<string, string> unitTypeName;
    private readonly Func<int, string> playerName;
    public JsonElement State { get; }
    public Func<string, string> CareerName { get; }
    public Func<string, string> ResourceName { get; }

    public BattleLogContext(JsonElement state, Func<string, string> unitTypeName,
        Func<string, string> careerName, Func<string, string> resourceName, Func<int, string> playerName)
    {
        State = state.Clone();
        this.unitTypeName = unitTypeName;
        CareerName = careerName;
        ResourceName = resourceName;
        this.playerName = playerName;
    }

    public string UnitName(double id)
    {
        foreach (var unit in State.GetProperty("units").EnumerateArray())
            if (unit.GetProperty("id").GetDouble() == id)
                return unitTypeName(FeedbackJson.Text(unit.GetProperty("type")));
        return "单位";
    }

    public string PlayerName(double id) => playerName(checked((int)id));

    internal string HolderName(JsonElement subject)
    {
        var id = subject.GetProperty("id");
        var kind = FeedbackJson.Text(subject.GetProperty("kind"));
        if (kind is not ("player" or "unit" or "weapon")) return FeedbackJson.Text(id);
        var numeric = id.ValueKind == JsonValueKind.Number ? id.GetDouble() :
            double.TryParse(FeedbackJson.Text(id), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
        return kind == "player" ? PlayerName(numeric) : UnitName(numeric);
    }
}

internal static class FeedbackJson
{
    internal static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? EcmaJson.ReadString(EcmaJson.Parse(value.GetRawText()))
        : EcmaJson.Stringify(EcmaJson.Parse(value.GetRawText()));

    internal static string Number(double value) => EcmaJson.Stringify(JsonValue.Create(value));
}
