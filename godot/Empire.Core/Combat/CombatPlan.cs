using System.Text.Json.Nodes;
using Empire.Core.Geometry;

namespace Empire.Core.Combat;

public sealed record PlannedUnitHit(int Target, Coord At, bool Primary, int? ProtectedUnit,
    DamageBreakdown Damage, double HpBefore, double HpAfter, bool Killed, IReadOnlyList<JsonObject> Effects);
public sealed record PlannedStructureHit(string Target, Coord At, bool Primary, StructureCombatForecast Forecast);
public sealed record PlannedSupportAttack(int Attacker, int Target, string Weapon, DamageBreakdown Damage,
    double HpBefore, double HpAfter, bool Killed, IReadOnlyList<JsonObject> Effects);
public sealed record CombatPlan(int Attacker, string Weapon, Coord From, Coord AimedAt, string Area,
    IReadOnlyList<Coord> AffectedCells, CombatForecast? PrimaryUnit, StructureCombatForecast? PrimaryStructure,
    IReadOnlyList<PlannedUnitHit> UnitHits, IReadOnlyList<PlannedStructureHit> StructureHits, PlannedSupportAttack? SupportAttack);
public sealed record ForcedMovementResult(Coord From, Coord To, IReadOnlyList<Coord> Path, bool Killed);
