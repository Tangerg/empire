using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Objectives;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Ai;

public sealed record AiRules(ReadOnlyContent Content, GridRegistry Grids, ITacticalSpace Space,
    AbilityCommands Commands, BattleCombat Combat, SupportOrders Support, BattleResources Resources,
    UnitDirectiveRegistry Directives, TurnOrderSystem Turns, ObjectiveHandlerRegistry Objectives);

public sealed record AiPlanningDependencies(AiRules Rules, AbilityAiEvaluatorRegistry AbilityEvaluators,
    AiObjectiveAdvisorRegistry ObjectiveAdvisors);
