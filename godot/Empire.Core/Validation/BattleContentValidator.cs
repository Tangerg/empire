using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Persistence;
using Empire.Core.Engine;
using Empire.Core.References;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Validation;

/// <summary>Source catalog installation invariants, followed by actual runtime capability checks.</summary>
public sealed class BattleContentValidator
{
    private readonly StateContent content;
    private readonly RuleReferenceRules rules;
    private readonly BattleReferenceCheckRegistry referenceChecks;
    public BattleContentValidator(RuleReferenceRules rules, BattleReferenceCheckRegistry referenceChecks)
    {
        content = rules.Content.Source;
        this.rules = rules;
        this.referenceChecks = referenceChecks;
    }
    private static double N(JsonNode? raw) => raw is not null && raw.GetValueKind() == JsonValueKind.Number &&
        double.TryParse(raw.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static bool Whole(double value) => double.IsFinite(value) && value == Math.Truncate(value);
    private void RequireId(string family, string id, string owner)
    {
        if (content.TryGet(family, id) is null) throw new DomainInvariantException($"{owner} references missing content id \"{id}\"");
    }
    private static void Accounts(JsonNode? raw, string owner)
    {
        foreach (var (id, node) in Object(raw, "resource accounts"))
        {
            if (RulePayloadReferences.Blank(id)) throw new DomainInvariantException($"{owner} has an empty resource id");
            var account = Object(node, "resource account"); var current = N(account["current"]);
            if (!double.IsFinite(current) || current < 0)
                throw new DomainInvariantException($"{owner} resource \"{id}\" current must be finite and non-negative");
            if (!account.ContainsKey("capacity") || account["capacity"] is { } capacity && (!double.IsFinite(N(capacity)) || N(capacity) < current))
                throw new DomainInvariantException($"{owner} resource \"{id}\" capacity must be null or at least current");
        }
    }
    private static void Amounts(JsonNode? raw, string owner)
    {
        foreach (var amount in Objects(raw, "resource amounts"))
        {
            var id = Text(amount, "resource");
            if (RulePayloadReferences.Blank(id)) throw new DomainInvariantException($"{owner} has an empty resource id");
            if (!double.IsFinite(N(amount["amount"])) || N(amount["amount"]) <= 0)
                throw new DomainInvariantException($"{owner} resource \"{id}\" amount must be finite and positive");
        }
    }
    public void Validate()
    {
        // StateContent has already checked pack identity/dependencies, duplicate
        // definitions and terrain encoding. The remaining checks keep source order.
        foreach (var family in rules.Content.Families)
            foreach (var definition in content.All(family))
                if (definition.ContainsKey("name") || definition.ContainsKey("blurb"))
                    throw new DomainInvariantException($"content {family} \"{Text(definition, "id")}\" contains obsolete presentation text");
        var matchups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pack in Objects(rules.Content.CopyPacks(), "packs")) foreach (var matchup in Objects(pack["damageMatchups"], "damage matchups"))
        {
            var damage = Text(matchup, "damageType"); var armor = Text(matchup, "armorClass");
            if (!double.IsFinite(N(matchup["multiplier"])) || N(matchup["multiplier"]) <= 0)
                throw new DomainInvariantException($"damage matchup \"{damage}\" -> \"{armor}\" must be > 0");
            if (!matchups.Add(damage + "\0" + armor))
                throw new DomainInvariantException($"damage matchup already registered: \"{damage}\" -> \"{armor}\"");
        }
        foreach (var unit in content.All("units"))
        {
            var owner = $"unit \"{Text(unit, "id")}\"";
            if (!double.IsFinite(N(unit["value"])) || N(unit["value"]) < 0)
                throw new DomainInvariantException($"{owner} value must be finite and non-negative");
            Accounts(unit["resources"], owner); Amounts(unit["recruitCosts"], owner + " recruit cost");
            if (unit["morale"] is JsonObject morale && (!double.IsFinite(N(morale["maximum"])) || N(morale["maximum"]) < 1 ||
                !double.IsFinite(N(morale["resilience"])) || N(morale["resilience"]) < 0 || N(morale["resilience"]) > 0.9))
                throw new DomainInvariantException($"{owner} has an invalid morale profile");
            if (unit["transport"] is JsonObject transport && (!Whole(N(transport["capacity"])) || N(transport["capacity"]) < 1))
                throw new DomainInvariantException($"{owner} transport capacity must be a positive integer");
            RequireId("movementProfiles", Text(unit, "movementClass"), owner);
            RequireId("armorClasses", Text(unit, "armorClass"), owner);
            var weapons = Array(unit["weapons"], "weapons");
            if (weapons.Count == 0) throw new DomainInvariantException($"{owner} must define at least one weapon");
            foreach (var weapon in weapons) RequireId("weapons", Text(weapon, "weapon"), owner);
            foreach (var formation in unit["formations"] as JsonArray ?? new JsonArray()) RequireId("formations", Text(formation, "formation"), owner);
        }
        foreach (var weapon in content.All("weapons"))
        {
            var owner = $"weapon \"{Text(weapon, "id")}\"";
            var bonusIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bonus in Objects(weapon["bonuses"], "weapon bonuses"))
            {
                var id = Text(bonus, "id");
                if (RulePayloadReferences.Blank(id) || !bonusIds.Add(id))
                    throw new DomainInvariantException($"{owner} bonus identities must be nonempty and unique");
                if (bonus.ContainsKey("reason"))
                    throw new DomainInvariantException($"{owner} bonus \"{id}\" contains obsolete presentation text");
            }
            Accounts(weapon["resources"], owner); Amounts(weapon["resourceRequirements"], owner + " requirement"); Amounts(weapon["resourceCosts"], owner + " cost");
            RequireId("damageTypes", Text(weapon, "damageType"), owner);
            foreach (var effect in Objects(weapon["hitEffects"], "hit effects"))
            {
                if (rules.HitEffects.Has(Text(effect, "type")))
                    rules.HitEffects.Validate(rules.Content, effect, owner);
            }
        }
        foreach (var terrain in content.All("terrains"))
        {
            var owner = $"terrain \"{Text(terrain, "id")}\"";
            Amounts(terrain["ownerTurnGrants"], owner + " grant");
            foreach (var unit in Array(terrain["produces"], "produces")) RequireId("units", Text(unit, "unit"), owner);
        }
        foreach (var career in content.All("careers"))
        {
            var owner = $"career \"{Text(career, "id")}\""; Amounts(career["costs"], owner + " cost");
            if (!Whole(N(career["tier"])) || N(career["tier"]) < 0) throw new DomainInvariantException($"{owner} tier must be a non-negative integer");
            if (!double.IsFinite(N(career["minimumMastery"])) || N(career["minimumMastery"]) < 0 ||
                !double.IsFinite(N(career["masteryThreshold"])) || N(career["masteryThreshold"]) < 1)
                throw new DomainInvariantException($"{owner} mastery values are invalid");
            RequireId("units", Text(career, "unitType"), owner);
            foreach (var from in Array(career["from"], "from")) RequireId("careers", Text(from, "career"), owner);
            foreach (var ability in Array(career["masteryAbilities"], "masteryAbilities"))
                if (RulePayloadReferences.Blank(Text(ability, "ability"))) throw new DomainInvariantException($"{owner} has an empty mastery ability id");
        }
        var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonObject career)
        {
            var id = Text(career, "id"); if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new DomainInvariantException($"cyclic career dependency involving \"{id}\"");
            foreach (var predecessor in Array(career["from"], "from")) Visit(content.Get("careers", Text(predecessor, "career")));
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var career in content.All("careers")) Visit(career);
        foreach (var formation in content.All("formations"))
            if (!double.IsFinite(N(formation["attackMultiplier"])) || N(formation["attackMultiplier"]) <= 0 ||
                !double.IsFinite(N(formation["defenseDelta"])) || !Whole(N(formation["movementDelta"])) ||
                !Whole(N(formation["minimumAdjacentAllies"])) || N(formation["minimumAdjacentAllies"]) < 0)
                throw new DomainInvariantException($"formation \"{Text(formation, "id")}\" has invalid modifiers");
        foreach (var tactic in content.All("tactics"))
        {
            var owner = $"tactic \"{Text(tactic, "id")}\""; Amounts(tactic["costs"], owner + " cost");
            foreach (var effect in Objects(tactic["effects"], "effects")) RequireId("statuses", Text(effect, "status"), owner);
        }
        foreach (var overlay in content.All("terrainOverlays"))
            if (overlay["turnStartStatus"] is JsonObject status)
                RequireId("statuses", Text(status, "id"), $"terrain overlay \"{Text(overlay, "id")}\"");
        foreach (var pack in Objects(rules.Content.CopyPacks(), "packs"))
        {
            var units = Objects(pack["units"], "units").ToArray();
            var damageTypes = Objects(pack["weapons"], "weapons").Select(weapon => Text(weapon, "damageType"))
                .Concat(units.SelectMany(unit => Array(unit["weapons"], "weapons").Select(id => Text(content.Get("weapons", Text(id, "weapon")), "damageType")))).Distinct(StringComparer.Ordinal);
            var armors = units.Select(unit => Text(unit, "armorClass")).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var damage in damageTypes) foreach (var armor in armors)
                if (!matchups.Contains(damage + "\0" + armor)) throw new DomainInvariantException($"missing damage matchup: \"{damage}\" -> \"{armor}\"");
        }
        var issues = CapabilityIssues();
        if (issues.Count > 0) throw new BattleEngineConfigurationException(issues);
    }

    public IReadOnlyList<string> CapabilityIssues() => referenceChecks.ContentIssues(rules);
}
