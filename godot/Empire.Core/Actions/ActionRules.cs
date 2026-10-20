using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Deployment;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Progression;
using Empire.Core.Tactics;
using Empire.Core.Transport;

namespace Empire.Core.Actions;

public sealed record ActionRules(TacticalCommands Commands, DeploymentSystem Deployment,
    BattleTransport Transport, BattleProgression Progression, AbilityCommands Abilities,
    RecruitmentOrders Recruitment, SupportOrders Support, BattleCombat Combat,
    Func<JsonObject, Action<JsonObject>, BattleLifecycle> CreateLifecycle);
