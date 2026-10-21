using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

/// <summary>The named queries consulted by modifier providers, not an engine/service locator.</summary>
public sealed record CombatModifierRules(ReadOnlyContent Content, GridRegistry Grids,
    Func<string, string, double> Effectiveness);

public sealed record UnitDamageContext(CombatModifierRules Rules, JsonObject State, JsonObject Attacker,
    Coord AttackerAt, JsonObject Defender, Coord DefenderAt, JsonObject Weapon,
    TacticalBattlefield Battlefield, TacticalBoard Board)
{
    public ReadOnlyContent Content => Rules.Content;
}

public sealed record CombatModifierProvider(string Id, double Priority,
    Func<UnitDamageContext, IReadOnlyList<CombatModifier>> Provide) : IPrioritizedEntry;

public sealed class CombatModifierProviderRegistry : PriorityRegistry<CombatModifierProvider, CombatModifierProviderRegistry>
{
    public CombatModifierProviderRegistry() : base("combat modifier provider") { }
    public CombatModifierProviderRegistry Clone() => CopyInto(new CombatModifierProviderRegistry());
}

public sealed record ModifierPipelineResult(IReadOnlyList<CombatModifier> Modifiers, double Mitigation, double Damage);

/// <summary>Single arithmetic owner for ordered power, capped mitigation, and affine final phases.</summary>
public sealed class CombatModifierPipeline
{
    private readonly CombatModifierProviderRegistry providers;
    private readonly double mitigationCap;
    public CombatModifierPipeline(CombatModifierProviderRegistry providers, double mitigationCap = 0.6)
    {
        this.providers = providers;
        this.mitigationCap = mitigationCap;
    }
    public CombatModifierPipeline Register(CombatModifierProvider provider) { providers.Register(provider); return this; }
    public CombatModifierPipeline Replace(CombatModifierProvider provider) { providers.Replace(provider); return this; }
    public CombatModifierPipeline Seal() { providers.Seal(); return this; }
    public IReadOnlyList<string> ProviderIds => providers.Keys();
    public IReadOnlyList<CombatModifier> Collect(UnitDamageContext context)
    {
        var result = new List<CombatModifier>();
        foreach (var provider in providers.Ordered())
            foreach (var modifier in provider.Provide(context))
            {
                if (modifier.Kind != provider.Id)
                    throw new DomainInvariantException($"Combat modifier provider '{provider.Id}' emitted undeclared kind '{modifier.Kind}'.");
                result.Add(modifier);
            }
        return Array.AsReadOnly(result.ToArray());
    }
    public ModifierPipelineResult Evaluate(double @base, UnitDamageContext context)
    {
        var modifiers = Collect(context);
        var power = @base;
        double mitigation = 0, finalMultiplier = 1, finalOffset = 0;
        foreach (var modifier in modifiers)
        {
            if (modifier.Stage == "power") power = modifier.Operation == "multiply" ? power * modifier.Value : power + modifier.Value;
            else if (modifier.Stage == "mitigation") mitigation = modifier.Operation == "multiply" ? mitigation * modifier.Value : mitigation + modifier.Value;
            else if (modifier.Operation == "multiply") { finalMultiplier *= modifier.Value; finalOffset *= modifier.Value; }
            else finalOffset += modifier.Value;
        }
        mitigation = Math.Max(0, Math.Min(mitigationCap, mitigation));
        var damage = Math.Max(1, StateJson.Round(power * (1 - mitigation) * finalMultiplier + finalOffset));
        return new(modifiers, mitigation, damage);
    }
}

public static class DefaultCombatModifiers
{
    private static readonly CombatModifierProviderRegistry defaults = new CombatModifierProviderRegistry()
        .Register(new("core.matchup", 100, Matchup))
        .Register(new("core.weapon-target-tags", 200, TargetTags))
        .Register(new("core.attacker-strength", 300, Strength))
        .Register(new("core.rank", 350, Rank))
        .Register(new("core.status", 400, Status))
        .Register(new("core.commander", 500, Commander))
        .Register(new("core.formation-attack", 510, Formation))
        .Register(new("core.elevation", 520, Elevation))
        .Register(new("core.position", 530, Position))
        .Register(new("core.cover", 590, Cover))
        .Register(new("core.defense", 600, Defense))
        .Seal();

    public static CombatModifierProviderRegistry CreateProviders() => defaults.Clone();
    public static CombatModifierPipeline Create() => new(CreateProviders());

    internal static double AttackerStrength(StateContent content, JsonObject unit)
    {
        var maximum = StateJson.Number(content.Read("units", StateJson.Text(unit, "type")), "maxHp", 1);
        return 0.5 + 0.5 * (maximum > 0 ? Math.Clamp(StateJson.Number(unit, "hp", 0) / maximum, 0, 1) : 1);
    }


    private static IReadOnlyList<CombatModifier> Matchup(UnitDamageContext context)
    {
        var selected = context.Weapon;
        var defenderDef = context.Battlefield.Content.Read("units", StateJson.Text(context.Defender, "type"));
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.matchup", source, stage, operation, value, facts));
        Add("matchup.effectiveness", "matchup", "power", "multiply",
            context.Rules.Effectiveness(StateJson.Text(selected, "damageType"), StateJson.Text(defenderDef, "armorClass")));
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> TargetTags(UnitDamageContext context)
    {
        var selected = context.Weapon;
        var defenderDef = context.Battlefield.Content.Read("units", StateJson.Text(context.Defender, "type"));
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.weapon-target-tags", source, stage, operation, value, facts));
        foreach (var bonus in StateJson.Objects(selected["bonuses"], "bonuses"))
            if (defenderDef.GetProperty("tags").EnumerateArray().Any(tag => StateJson.Text(tag) == StateJson.Text(bonus, "targetTag")))
                Add("weapon.bonus." + StateJson.Text(selected, "id") + "." + StateJson.Text(bonus, "id"), "weapon", "power", "multiply", StateJson.Number(bonus, "multiplier", 1),
                    new JsonObject { ["weaponId"] = StateJson.Text(selected, "id"), ["bonusId"] = StateJson.Text(bonus, "id") });
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Strength(UnitDamageContext context)
    {
        var content = context.Battlefield.Content;
        var attacker = context.Attacker;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.attacker-strength", source, stage, operation, value, facts));
        Add("unit.hp-strength", "unit", "power", "multiply", AttackerStrength(content, attacker));
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Rank(UnitDamageContext context)
    {
        var attacker = context.Attacker;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.rank", source, stage, operation, value, facts));
        var rank = StateJson.Integer(attacker, "rank");
        Add("rank.attack", "unit", "power", "multiply", 1 + rank * 0.04, new JsonObject { ["rank"] = rank });
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Status(UnitDamageContext context)
    {
        var content = context.Battlefield.Content;
        var attacker = context.Attacker;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.status", source, stage, operation, value, facts));
        Add("status.attack", "status", "power", "multiply", CombatEffects.CalculateModifiers(content, attacker).AttackMultiplier);
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Commander(UnitDamageContext context)
    {
        var state = context.State;
        var attacker = context.Attacker;
        var grid = context.Board.Grid;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.commander", source, stage, operation, value, facts));
        Add("commander.attack", "commander", "power", "multiply", TacticalModifiers.CommanderAura(grid, state, attacker).AttackMultiplier);
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Formation(UnitDamageContext context)
    {
        var content = context.Battlefield.Content;
        var state = context.State;
        var attacker = context.Attacker;
        var grid = context.Board.Grid;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.formation-attack", source, stage, operation, value, facts));
        var formation = TacticalModifiers.ActiveFormation(content, grid, state, attacker);
        if (formation is not null && StateJson.Number(formation, "attackMultiplier", 1) != 1)
            Add("formation.attack." + StateJson.Text(formation, "id"), "formation", "power", "multiply", StateJson.Number(formation, "attackMultiplier", 1),
                new JsonObject { ["formationId"] = StateJson.Text(formation, "id") });
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Elevation(UnitDamageContext context)
    {
        var from = context.AttackerAt;
        var to = context.DefenderAt;
        var battlefield = context.Battlefield;
        var rules = context.State["rules"]!.AsObject();
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.elevation", source, stage, operation, value, facts));
        var elevationDelta = battlefield.Cell(from).Elevation - battlefield.Cell(to).Elevation;
        if (elevationDelta >= StateJson.Number(rules, "highGroundThreshold", 0))
            Add("elevation.high-ground", "elevation", "power", "multiply", StateJson.Number(rules, "highGroundDamageMultiplier", 1),
                new JsonObject { ["delta"] = elevationDelta });
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Position(UnitDamageContext context)
    {
        var state = context.State;
        var attacker = context.Attacker;
        var defender = context.Defender;
        var from = context.AttackerAt;
        var selected = context.Weapon;
        var grid = context.Board.Grid;
        var rules = context.State["rules"]!.AsObject();
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.position", source, stage, operation, value, facts));
        var defenderPosition = TacticalQueries.Position(defender);
        if (grid.Distance(from, defenderPosition) == 1 && (selected["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == "melee") || StateJson.Number(selected, "maxRange", 1) <= 1))
        {
            var incoming = grid.Toward(defenderPosition, from);
            var facing = StateJson.Text(defender, "facing");
            if (incoming == grid.Opposite(facing)) Add("position.back-attack", "position", "power", "multiply", StateJson.Number(rules, "backAttackMultiplier", 1), new JsonObject { ["position"] = "back" });
            else if (incoming != facing) Add("position.side-attack", "position", "power", "multiply", StateJson.Number(rules, "sideAttackMultiplier", 1), new JsonObject { ["position"] = "side" });
            var behind = grid.Step(defenderPosition, grid.Opposite(incoming));
            var flanker = TacticalQueries.UnitAt(state, behind);
            if (flanker is not null && StateJson.Integer(flanker, "id") != StateJson.Integer(attacker, "id") && TacticalQueries.AreAllies(state, StateJson.Integer(flanker, "owner"), StateJson.Integer(attacker, "owner")))
                Add("position.flank", "position", "power", "multiply", StateJson.Number(rules, "flankAttackMultiplier", 1), new JsonObject { ["position"] = "flank" });
        }
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Cover(UnitDamageContext context)
    {
        var from = context.AttackerAt;
        var to = context.DefenderAt;
        var selected = context.Weapon;
        var grid = context.Board.Grid;
        var battlefield = context.Battlefield;
        var rules = context.State["rules"]!.AsObject();
        var elevationDelta = context.Battlefield.Cell(context.AttackerAt).Elevation - context.Battlefield.Cell(context.DefenderAt).Elevation;
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.cover", source, stage, operation, value, facts));
        if (grid.Distance(from, to) > 1 && StateJson.Text(selected, "lineOfSight") != "arc" && !selected["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == "ignores-cover"))
        {
            var cell = battlefield.Cell(to);
            static int Score(string value) => value == "full" ? 2 : value == "half" ? 1 : 0;
            var directional = cell.DirectionalCoverFrom(grid, from);
            var cover = Score(directional) > Score(cell.Cover) ? directional : cell.Cover;
            if (elevationDelta >= StateJson.Number(rules, "highGroundThreshold", 0)) cover = cover == "full" ? "half" : "none";
            var value = cover == "full" ? StateJson.Number(rules, "fullCoverDefense", 0) : cover == "half" ? StateJson.Number(rules, "halfCoverDefense", 0) : 0;
            if (value > 0) Add("cover." + cover, "cover", "mitigation", "add", value, new JsonObject { ["cover"] = cover });
        }
        return modifiers.AsReadOnly();
    }

    private static IReadOnlyList<CombatModifier> Defense(UnitDamageContext context)
    {
        var content = context.Battlefield.Content;
        var state = context.State;
        var defender = context.Defender;
        var to = context.DefenderAt;
        var grid = context.Board.Grid;
        var battlefield = context.Battlefield;
        var defenderDef = context.Battlefield.Content.Read("units", StateJson.Text(context.Defender, "type"));
        var modifiers = new List<CombatModifier>();
        void Add(string id, string source, string stage, string operation, double value, JsonObject? facts = null) =>
            modifiers.Add(new(id, "core.defense", source, stage, operation, value, facts));
        var defenderStatus = CombatEffects.CalculateModifiers(content, defender);
        var defenderCommand = TacticalModifiers.CommanderAura(grid, state, defender);
        var defenderFormation = TacticalModifiers.ActiveFormation(content, grid, state, defender);
        var defense = Math.Max(0, StateJson.Number(defenderDef, "defense", 0) + StateJson.Number(defender, "rank", 0) * 0.02
            + defenderStatus.DefenseDelta + defenderCommand.DefenseDelta + (defenderFormation is null ? 0 : StateJson.Number(defenderFormation, "defenseDelta", 0)));
        Add("defense.terrain", "terrain", "mitigation", "add", battlefield.Cell(to).Defense, new JsonObject { ["component"] = "terrain" });
        Add("defense.unit", "unit", "mitigation", "add", defense, new JsonObject { ["component"] = "unit" });
        return modifiers.AsReadOnly();
    }
}
