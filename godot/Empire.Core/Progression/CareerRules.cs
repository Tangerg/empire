using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Progression;

public sealed record CareerOption(JsonObject Career, bool Eligible, bool Unlocked, bool Mastered, IReadOnlyList<ActionRefusal> Reasons);

/// <summary>Career paths and transitions under an explicitly supplied content/resource ruleset.</summary>
public sealed class CareerRules
{
    private readonly ReadOnlyContent catalog;
    private readonly StateContent content;
    private readonly BattleResources resources;
    private readonly CombatEffects effects;

    public CareerRules(ReadOnlyContent catalog, BattleResources resources, CombatEffects effects)
    { this.catalog = catalog; content = catalog.Source; this.resources = resources; this.effects = effects; }

    public IReadOnlyList<string> UnitAbilityIds(JsonObject unit) =>
        content.Read("units", Text(unit, "type")).GetProperty("abilities").EnumerateArray().Select(Text)
            .Concat(Array(unit["learnedAbilities"], "learnedAbilities").Select(value => Text(value, "ability")))
            .Distinct(StringComparer.Ordinal).ToArray();

    public double Mastery(JsonObject unit, string career) =>
        Math.Max(0, Number(Object(Object(unit["career"], "career")["mastery"], "mastery"), career, 0));

    public IReadOnlyList<CareerOption> Options(JsonObject state, JsonObject unit)
    {
        var careerState = Object(unit["career"], "career");
        var current = careerState["current"]?.GetValue<string>();
        var currentMastery = string.IsNullOrEmpty(current) ? 0 : Mastery(unit, current);
        var owner = new BattleResources.Subject("player", TacticalQueries.Player(state, Integer(unit, "owner")));
        var currentRoots = !string.IsNullOrEmpty(current) && content.TryGet("careers", current) is { } currentDefinition
            ? RootsOf(currentDefinition, new HashSet<string>(StringComparer.Ordinal)) : new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CareerOption>();
        bool Unlocked(string id) => Array(careerState["unlocked"], "unlocked").Any(value => Text(value, "career") == id);
        foreach (var career in content.All("careers"))
        {
            var id = Text(career, "id");
            if (id == current) continue;
            var unlocked = Unlocked(id);
            if (!unlocked && (currentRoots.Count == 0 ? Text(career, "unitType") != Text(unit, "type") :
                    !RootsOf(career, new HashSet<string>(StringComparer.Ordinal)).Overlaps(currentRoots))) continue;
            var reasons = new List<ActionRefusal>();
            if (!unlocked)
            {
                var from = Array(career["from"], "from");
                if (from.Count == 0 && (current is not null || Text(career, "unitType") != Text(unit, "type")))
                    reasons.Add(new("career.root_mismatch"));
                else if (from.Count > 0 && (string.IsNullOrEmpty(current) || !from.Any(value => Text(value, "career") == current)))
                    reasons.Add(new("career.path_mismatch"));
                var minimumRank = Number(career["minimumRank"], "minimumRank");
                var minimumMastery = Number(career["minimumMastery"], "minimumMastery");
                if (Number(unit["rank"], "rank") < minimumRank) reasons.Add(new("career.minimum_rank", new() { ["required"] = minimumRank }));
                if (currentMastery < minimumMastery) reasons.Add(new("career.minimum_mastery", new() { ["required"] = minimumMastery }));
            }
            if (!resources.CanAfford(owner, Array(career["costs"], "costs"))) reasons.Add(new("career.insufficient_resources"));
            result.Add(new CareerOption((JsonObject)career.DeepClone(), reasons.Count == 0, unlocked,
                Mastery(unit, id) >= Number(career["masteryThreshold"], "masteryThreshold"), reasons.AsReadOnly()));
        }
        return result.OrderBy(option => Number(option.Career["tier"], "tier"))
            .ThenBy(option => Text(option.Career, "id"), StringComparer.Ordinal).ToArray();
    }

    private HashSet<string> RootsOf(JsonObject career, HashSet<string> visiting)
    {
        var id = Text(career, "id"); var from = Array(career["from"], "from");
        if (from.Count == 0 || visiting.Contains(id)) return new HashSet<string>(StringComparer.Ordinal) { id };
        visiting.Add(id);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var predecessor in from)
            if (content.TryGet("careers", Text(predecessor, "predecessor")) is { } definition)
                roots.UnionWith(RootsOf(definition, new HashSet<string>(visiting, StringComparer.Ordinal)));
        return roots;
    }

    public void Change(JsonObject state, JsonObject unit, string requested, Action<JsonObject> emit)
    {
        var option = Options(state, unit).FirstOrDefault(candidate => Text(candidate.Career, "id") == requested)
            ?? throw new IllegalActionException(new ActionRefusal("career.unavailable", new() { ["careerId"] = requested }));
        if (!option.Eligible) throw new IllegalActionException(new ActionRefusal("career.requirements", new()
        { ["reasons"] = System.Text.Json.JsonSerializer.SerializeToNode(option.Reasons) }));
        var career = option.Career;
        var health = new UnitHealth(catalog, unit);
        var nextType = Text(career, "unitType");
        health.ValidateTypeChange(nextType);
        var nextDefinition = content.Get("units", nextType);
        var weaponState = InitialWeaponState(unit, nextDefinition);
        var subject = new BattleResources.Subject("player", TacticalQueries.Player(state, Integer(unit, "owner")));
        foreach (var cost in resources.SpendAll(subject, Array(career["costs"], "costs")))
            resources.Announce(subject, cost.Resource, -cost.Value, emit);
        var careerState = Object(unit["career"], "career");
        var previous = careerState["current"]?.DeepClone();
        careerState["current"] = requested;
        health.ChangeType(nextType);
        unit["weaponState"] = weaponState;
        var accounts = Object(unit["resources"], "resources");
        foreach (var (resource, account) in Object(nextDefinition["resources"], "resources"))
            if (accounts[resource] is null) accounts[resource] = account!.DeepClone();
        var unlocked = Array(careerState["unlocked"], "unlocked");
        if (!unlocked.Any(value => Text(value, "career") == requested)) unlocked.Add(requested);
        var mastery = Object(careerState["mastery"], "mastery");
        if (!mastery.ContainsKey(requested)) mastery[requested] = 0;
        unit["done"] = true;
        emit(new JsonObject { ["type"] = "careerChanged", ["unit"] = unit["id"]!.DeepClone(), ["from"] = previous,
            ["to"] = requested, ["unitType"] = career["unitType"]!.DeepClone() });
    }

    private JsonObject InitialWeaponState(JsonObject unit, JsonObject nextDefinition)
    {
        var result = new JsonObject(); var existing = Object(unit["weaponState"], "weaponState");
        foreach (var node in Array(nextDefinition["weapons"], "weapons"))
        {
            var id = Text(node, "weapon");
            result[id] = existing[id] is JsonObject carried ? carried.DeepClone() :
                new JsonObject { ["cooldownRemaining"] = 0, ["resources"] = content.Get("weapons", id)["resources"]!.DeepClone() };
        }
        return result;
    }

    public void AwardProgress(JsonObject unit, double amount, Action<JsonObject> emit) => effects.AwardCareerProgress(unit, amount, emit);
}
