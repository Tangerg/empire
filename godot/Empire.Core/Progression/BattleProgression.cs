using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Progression;

public sealed record BattleProgressionRules(
    ReadOnlyContent Content,
    GridRegistry Grids,
    BattleResources Resources,
    TacticalCommands Commands,
    CombatEffects Effects);

public sealed class BattleProgression
{
    private readonly TacticalCommands commands;
    public CareerRules Careers
    {
        get;
    }
    public FormationRules Formations
    {
        get;
    }
    public BattleProgression(BattleProgressionRules rules)
    {
        Careers = new CareerRules(new CareerRuleServices(rules.Content, rules.Resources, rules.Effects));
        Formations = new FormationRules(new FormationRuleServices(rules.Content, rules.Grids));
        commands = rules.Commands;
    }
    public void ChangeCareer(JsonObject state, int unitId, string career, Action<JsonObject> emit) =>
        Careers.Change(state, commands.CommandableUnit(state, unitId), career, emit);
    public void ChangeFormation(JsonObject state, int unitId, string? formation, Action<JsonObject> emit)
    {
        var unit = commands.CommandableUnit(state, unitId);
        Formations.ValidateChange(state, unit, formation);
        var previous = unit["formation"]?.GetValue<string>();
        if (previous == formation)
            return;
        unit["formation"] = formation;
        emit(new JsonObject { ["type"] = "formationChanged", ["unit"] = unit["id"]!.DeepClone(), ["from"] = previous, ["to"] = formation });
    }
}
