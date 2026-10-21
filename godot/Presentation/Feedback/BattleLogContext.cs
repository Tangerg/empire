using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record BattleLogNames(Func<string, string> UnitType, Func<string, string> Career,
    Func<string, string> Resource, Func<int, string> Player);

/// <summary>Names are projections of one committed state and its injected ruleset.</summary>
public sealed class BattleLogContext
{
    private readonly Func<string, string> unitTypeName;
    private readonly Func<int, string> playerName;
    private readonly JsonElement before;
    private readonly Dictionary<int, (string Type, int Owner)> created = new();
    public JsonElement State { get; }
    public Func<string, string> CareerName { get; }
    public Func<string, string> ResourceName { get; }

    public BattleLogContext(BattleLogNames names, JsonElement before, JsonElement after)
    {
        State = after.Clone();
        this.before = before.Clone();
        unitTypeName = names.UnitType;
        CareerName = names.Career;
        ResourceName = names.Resource;
        playerName = names.Player;
    }

    internal void Observe(JsonElement value)
    {
        var kind = FeedbackJson.Text(value.GetProperty("type"));
        if (kind is not ("unitSpawned" or "recruit")) return;
        var id = value.GetProperty("unit").GetInt32();
        var unitType = value.GetProperty("unitType");
        if (unitType.ValueKind != JsonValueKind.String)
            throw new LocalizationException("Creation event unitType must be a content identity string.");
        var type = FeedbackJson.Text(unitType);
        var owner = value.GetProperty("owner").GetInt32();
        if (id <= 0 || string.IsNullOrWhiteSpace(type) || UnitRecords(before).Any(unit => unit.GetProperty("id").GetInt32() == id))
            throw new LocalizationException("Creation event identity contradicts the pre-action snapshot.");
        _ = unitTypeName(type);
        _ = playerName(owner);
        if (!created.TryAdd(id, (type, owner)))
            throw new LocalizationException($"Repeated creation event for unit '{id}'.");
    }

    public string UnitName(double id)
    {
        if (!double.IsFinite(id) || id != Math.Truncate(id) || id <= 0 || id > int.MaxValue)
            throw new LocalizationException("Committed event unit identity must be a positive integer.");
        if (created.TryGetValue((int)id, out var identity)) return unitTypeName(identity.Type);
        foreach (var unit in UnitRecords(State).Concat(UnitRecords(before)))
            if (unit.GetProperty("id").GetDouble() == id)
                return unitTypeName(FeedbackJson.Text(unit.GetProperty("type")));
        throw new LocalizationException($"Committed event references unavailable unit identity '{id}'.");
    }

    private static IEnumerable<JsonElement> UnitRecords(JsonElement state)
    {
        foreach (var unit in state.GetProperty("units").EnumerateArray()) yield return unit;
        foreach (var entry in state.GetProperty("embarkedUnits").EnumerateArray()) yield return entry.GetProperty("unit");
        foreach (var marker in state.GetProperty("markers").EnumerateArray())
            if (marker.TryGetProperty("fallenUnit", out var fallen)) yield return fallen;
    }

    public string PlayerName(double id) => playerName(checked((int)id));

    internal string HolderName(JsonElement subject)
    {
        var id = subject.GetProperty("id");
        var kind = FeedbackJson.Text(subject.GetProperty("kind"));
        if (kind is not ("player" or "unit" or "weapon"))
            throw new LocalizationException($"No resource holder presentation is registered for '{kind}'.");
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
