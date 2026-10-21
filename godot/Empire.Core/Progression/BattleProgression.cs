using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Progression;

public sealed class BattleProgression
{
    private readonly TacticalCommands commands;
    public CareerRules Careers { get; }
    public FormationRules Formations { get; }
    public BattleProgression(ReadOnlyContent catalog, GridRegistry grids, BattleResources resources,
        TacticalCommands commands, CombatEffects effects)
    { Careers = new CareerRules(catalog, resources, effects); Formations = new FormationRules(catalog, grids); this.commands = commands; }
    public void ChangeCareer(JsonObject state, int unitId, string career, Action<JsonObject> emit) =>
        Careers.Change(state, commands.CommandableUnit(state, unitId, "转职"), career, emit);
    public void ChangeFormation(JsonObject state, int unitId, string? formation, Action<JsonObject> emit)
    {
        var unit = commands.CommandableUnit(state, unitId, "调整阵形");
        Formations.ValidateChange(state, unit, formation);
        var previous = unit["formation"]?.GetValue<string>();
        if (previous == formation) return;
        unit["formation"] = formation;
        emit(new JsonObject { ["type"] = "formationChanged", ["unit"] = unit["id"]!.DeepClone(), ["from"] = previous, ["to"] = formation });
    }
}
