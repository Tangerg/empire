using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Combat;

public sealed record StatusModifiers(
    double AttackMultiplier, double DefenseDelta, double MovementDelta, bool CannotCapture);

public sealed class CombatEffects
{
    private readonly StateContent content;
    public CombatEffects(JsonArray packs) => content = new StateContent(packs);

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

    public void AddStatus(JsonObject unit, string id, double duration, int? sourceUnitId, Action<JsonObject>? emit)
    {
        if (!double.IsFinite(duration) || duration < 1 || duration != Math.Truncate(duration))
            throw new DomainInvariantException("status duration must be >= 1");
        var definition = content.Get("statuses", id);
        var statuses = unit["statuses"]!.AsArray();
        var carried = statuses.OfType<JsonObject>().FirstOrDefault(value => StateJson.Text(value, "id") == id);
        if (carried is null)
        {
            carried = new JsonObject { ["id"] = id, ["remaining"] = duration, ["stacks"] = 1 };
            if (sourceUnitId is not null) carried["sourceUnitId"] = sourceUnitId.Value;
            statuses.Add(carried);
        }
        else
        {
            var remaining = StateJson.Number(carried, "remaining", 0);
            switch (StateJson.Text(definition, "stackMode"))
            {
                case "refresh": carried["remaining"] = Math.Max(remaining, duration); break;
                case "extend": carried["remaining"] = remaining + duration; break;
                case "stack":
                    carried["stacks"] = Math.Min(StateJson.Number(definition, "maxStacks", 1),
                        StateJson.Number(carried, "stacks", 1) + 1);
                    carried["remaining"] = Math.Max(remaining, duration);
                    break;
                default: throw new DomainInvariantException($"unknown status stack mode for \"{id}\"");
            }
            if (sourceUnitId is not null) carried["sourceUnitId"] = sourceUnitId.Value;
        }
        emit?.Invoke(new JsonObject
        {
            ["type"] = "statusApplied", ["unit"] = unit["id"]!.DeepClone(), ["status"] = id,
            ["remaining"] = carried["remaining"]!.DeepClone(), ["stacks"] = carried["stacks"]!.DeepClone(),
        });
    }

    public bool RemoveStatus(JsonObject unit, string id, Action<JsonObject>? emit)
    {
        var statuses = unit["statuses"]!.AsArray();
        var instance = statuses.OfType<JsonObject>().FirstOrDefault(value => StateJson.Text(value, "id") == id);
        if (instance is null) return false;
        statuses.Remove(instance);
        emit?.Invoke(new JsonObject { ["type"] = "statusRemoved", ["unit"] = unit["id"]!.DeepClone(), ["status"] = id });
        return true;
    }

    private static readonly double[] RankThresholds = [120, 320];
    public double? NextRankThreshold(int rank) => rank >= 0 && rank < RankThresholds.Length ? RankThresholds[rank] : null;
    public int RankFor(double progress) => progress >= RankThresholds[1] ? 2 : progress >= RankThresholds[0] ? 1 : 0;

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
