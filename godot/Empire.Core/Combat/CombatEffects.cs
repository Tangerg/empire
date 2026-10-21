using System.Text.Json.Nodes;
using Empire.Core.State;
using Empire.Core.Progression;
using Empire.Core.Statuses;

namespace Empire.Core.Combat;

public sealed record StatusModifiers(
    double AttackMultiplier, double DefenseDelta, double MovementDelta, bool CannotCapture);

public sealed class CombatEffects
{
    private readonly StateContent content;
    private readonly ReadOnlyContent catalog;
    private readonly IRankProgressionPolicy progression;
    public CombatEffects(ReadOnlyContent catalog, IRankProgressionPolicy progression)
    {
        this.catalog = catalog;
        content = catalog.Source;
        this.progression = progression;
    }

    public StatusModifiers Modifiers(JsonObject unit) => CalculateModifiers(content, unit);
    public bool CannotCapture(JsonObject unit) => Modifiers(unit).CannotCapture;

    internal static StatusModifiers CalculateModifiers(StateContent content, JsonObject unit)
    {
        double attack = 1, defense = 0, movement = 0;
        var cannotCapture = false;
        foreach (var instance in StateJson.Objects(unit["statuses"], "statuses"))
        {
            var modifiers = content.Get("statuses", StateJson.Text(instance, "id"))["modifiers"]!.AsObject();
            var stacks = Math.Max(1, StateJson.Number(instance, "stacks", 1));
            if (modifiers["attackMultiplier"] is not null)
                attack *= Math.Pow(StateJson.Number(modifiers, "attackMultiplier", 1), stacks);
            defense += StateJson.Number(modifiers, "defenseDelta", 0) * stacks;
            movement += StateJson.Number(modifiers, "movementDelta", 0) * stacks;
            cannotCapture |= StateJson.IsTrue(modifiers["cannotCapture"]);
        }
        return new StatusModifiers(attack, defense, movement, cannotCapture);
    }

    public string? BlockedAbilityStatus(JsonObject unit, IEnumerable<string> tags)
    {
        var requested = tags.ToHashSet(StringComparer.Ordinal);
        foreach (var instance in StateJson.Objects(unit["statuses"], "statuses"))
        {
            var id = StateJson.Text(instance, "id");
            if (content.Get("statuses", id)["blockedAbilityTags"]!.AsArray()
                .Any(value => requested.Contains(value!.GetValue<string>()))) return id;
        }
        return null;
    }

    public void AddStatus(JsonObject unit, string id, double duration, int? sourceUnitId, Action<JsonObject>? emit) =>
        StatusTransitions.Add(catalog, unit, id, duration, sourceUnitId, emit);

    public bool RemoveStatus(JsonObject unit, string id, Action<JsonObject>? emit) =>
        StatusTransitions.Remove(unit, id, emit);

    public double? NextRankThreshold(int rank) => progression.NextThreshold(rank);
    public int RankFor(double progress) => progression.RankFor(progress);

    public void AwardRankProgress(JsonObject unit, double requested, Action<JsonObject> emit)
    {
        var gained = Math.Max(0, StateJson.Round(requested));
        unit["rankProgress"] = StateJson.Number(unit, "rankProgress", 0) + gained;
        if (gained <= 0) return;
        AwardCareerProgress(unit, gained, emit);
        emit(new JsonObject { ["type"] = "rankProgressChanged", ["unit"] = unit["id"]!.DeepClone(),
            ["amount"] = gained, ["current"] = unit["rankProgress"]!.DeepClone() });
        var progress = StateJson.Number(unit, "rankProgress", 0);
        var next = RankFor(progress);
        var previous = StateJson.Integer(unit, "rank");
        if (next <= previous) return;
        unit["rank"] = next;
        emit(new JsonObject { ["type"] = "rankChanged", ["unit"] = unit["id"]!.DeepClone(),
            ["from"] = previous, ["to"] = next });
    }

    public void AwardCombatProgress(JsonObject unit, double damage, bool defeated, Action<JsonObject> emit) =>
        AwardRankProgress(unit, Math.Max(1, StateJson.Round(damage)) + (defeated ? 50 : 0), emit);

    public void AwardCareerProgress(JsonObject unit, double amount, Action<JsonObject> emit)
    {
        var career = unit["career"]!.AsObject();
        var current = career["current"]?.GetValue<string>();
        if (current is null || content.TryGet("careers", current) is not { } definition) return;
        var gained = Math.Max(0, StateJson.Round(amount));
        if (gained == 0) return;
        var mastery = career["mastery"]!.AsObject();
        var before = Math.Max(0, StateJson.Number(mastery, current, 0));
        var after = before + gained;
        mastery[current] = after;
        emit(new JsonObject { ["type"] = "careerProgressChanged", ["unit"] = unit["id"]!.DeepClone(),
            ["career"] = current, ["amount"] = gained, ["current"] = after });
        var threshold = StateJson.Number(definition, "masteryThreshold", 0);
        if (before >= threshold || after < threshold) return;
        var known = unit["learnedAbilities"]!.AsArray();
        var learned = definition["masteryAbilities"]!.AsArray()
            .Select(value => value!.GetValue<string>())
            .Where(value => !known.Any(existing => existing!.GetValue<string>() == value)).ToArray();
        foreach (var ability in learned) known.Add(ability);
        emit(new JsonObject { ["type"] = "careerMastered", ["unit"] = unit["id"]!.DeepClone(),
            ["career"] = current, ["abilities"] = new JsonArray(learned.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) });
    }
}
