using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Progression;

public sealed record CareerOption(JsonObject Career, bool Eligible, bool Unlocked, bool Mastered, IReadOnlyList<string> Reasons);

/// <summary>Career paths and transitions under an explicitly supplied content/resource ruleset.</summary>
public sealed class CareerRules
{
    private readonly StateContent content;
    private readonly BattleResources resources;
    private readonly CombatEffects effects;

    public CareerRules(JsonArray packs, BattleResources resources, CombatEffects effects)
    { content = new StateContent(packs); this.resources = resources; this.effects = effects; }

    public IReadOnlyList<string> UnitAbilityIds(JsonObject unit) =>
        Array(content.Get("units", Text(unit, "type"))["abilities"], "abilities")
            .Concat(Array(unit["learnedAbilities"], "learnedAbilities"))
            .Select(value => Text(value, "ability")).Distinct(StringComparer.Ordinal).ToArray();

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
            var reasons = new List<string>();
            if (!unlocked)
            {
                var from = Array(career["from"], "from");
                if (from.Count == 0 && (current is not null || Text(career, "unitType") != Text(unit, "type")))
                    reasons.Add("不能跨越到另一棵职业树的根职业");
                else if (from.Count > 0 && (string.IsNullOrEmpty(current) || !from.Any(value => Text(value, "career") == current)))
                    reasons.Add("当前职业不在可转职路径上");
                var minimumRank = Number(career["minimumRank"], "minimumRank");
                var minimumMastery = Number(career["minimumMastery"], "minimumMastery");
                if (Number(unit["rank"], "rank") < minimumRank) reasons.Add($"需要军衔 {Empire.Core.Determinism.EcmaNumber.Format(minimumRank)}");
                if (currentMastery < minimumMastery) reasons.Add($"需要当前职业熟练度 {Empire.Core.Determinism.EcmaNumber.Format(minimumMastery)}");
            }
            if (!resources.CanAfford(owner, Array(career["costs"], "costs"))) reasons.Add("转职资源不足");
            result.Add(new CareerOption((JsonObject)career.DeepClone(), reasons.Count == 0, unlocked,
                Mastery(unit, id) >= Number(career["masteryThreshold"], "masteryThreshold"), reasons));
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
            ?? throw new IllegalActionException($"无法转为当前职业或未知职业「{requested}」");
        if (!option.Eligible) throw new IllegalActionException(string.Join("；", option.Reasons));
        var career = option.Career;
        var subject = new BattleResources.Subject("player", TacticalQueries.Player(state, Integer(unit, "owner")));
        foreach (var cost in resources.SpendAll(subject, Array(career["costs"], "costs")))
            resources.Announce(subject, cost.Resource, -cost.Value, emit);
        var careerState = Object(unit["career"], "career");
        var previous = careerState["current"]?.DeepClone();
        var fromMaximum = Number(content.Get("units", Text(unit, "type"))["maxHp"], "maxHp");
        var nextDefinition = content.Get("units", Text(career, "unitType"));
        var weaponState = InitialWeaponState(unit, nextDefinition);
        careerState["current"] = requested;
        unit["type"] = nextDefinition["id"]!.DeepClone();
        unit["weaponState"] = weaponState;
        var nextMaximum = Number(nextDefinition["maxHp"], "maxHp");
        if (fromMaximum < 1 || nextMaximum < 1) throw new DomainInvariantException("health maxima must be positive");
        var scaled = Round(nextMaximum * Math.Clamp(Number(unit["hp"], "hp") / fromMaximum, 0, 1));
        unit["hp"] = Math.Max(1, Math.Min(nextMaximum, scaled));
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
