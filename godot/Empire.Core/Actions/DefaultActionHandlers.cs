using System.Text.Json.Nodes;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Actions;

public static class DefaultActionHandlers
{
    public static BattleActionRegistry Create() => new BattleActionRegistry()
        .Register("deployUnit", (context, action) => context.Rules.Deployment.Deploy(context.State,
            StateJson.Integer(action, "unit"), TacticalQueries.Position(action["at"]!.AsObject()), context.Emit), duringDeployment: true)
        .Register("finishDeployment", (context, _) => context.Rules.Deployment.Confirm(context.Lifecycle.BeginPlaying,
            context.State, context.Emit), handsOffTurn: true, duringDeployment: true)
        .Register("command", (context, action) => context.Rules.Abilities.Execute(context.State, action, context.Emit))
        .Register("tactic", (context, action) => context.Rules.Support.ExecuteTactic(context.State,
            StateJson.Text(action, "commander"), StateJson.Text(action, "tactic"),
            action["target"] is JsonObject target ? TacticalQueries.Position(target) : null, context.Emit))
        .Register("reaction", (context, action) =>
        {
            var unit = context.Rules.Commands.CommandableUnit(context.State, StateJson.Integer(action, "unit"));
            context.Rules.Commands.ChangeReaction(unit, StateJson.Text(action, "stance"), context.Emit);
        })
        .Register("face", (context, action) =>
        {
            var unit = context.Rules.Commands.CommandableUnit(context.State, StateJson.Integer(action, "unit"));
            context.Rules.Commands.TurnToFace(context.State, unit, StateJson.Text(action, "facing"), context.Emit);
        })
        .Register("changeCareer", (context, action) => context.Rules.Progression.ChangeCareer(context.State,
            StateJson.Integer(action, "unit"), StateJson.Text(action, "career"), context.Emit))
        .Register("changeFormation", (context, action) => context.Rules.Progression.ChangeFormation(context.State,
            StateJson.Integer(action, "unit"), action["formation"]?.GetValue<string>(), context.Emit))
        .Register("embark", (context, action) =>
        {
            var passenger = context.Rules.Commands.CommandableUnit(context.State, StateJson.Integer(action, "unit"));
            var carrier = context.Rules.Commands.OwnUnit(context.State, StateJson.Integer(action, "carrier"));
            context.Rules.Transport.Embark(context.State, StateJson.Integer(passenger, "id"), StateJson.Integer(carrier, "id"), context.Emit);
        })
        .Register("disembark", (context, action) =>
        {
            var carrier = context.Rules.Commands.CommandableUnit(context.State, StateJson.Integer(action, "carrier"));
            context.Rules.Transport.Disembark(context.State, StateJson.Integer(carrier, "id"), StateJson.Integer(action, "unit"),
                TacticalQueries.Position(action["at"]!.AsObject()), context.Emit);
            TacticalCommands.FinishCommand(context.State, StateJson.Integer(carrier, "id"));
        })
        .Register("recruit", (context, action) => context.Rules.Recruitment.Recruit(context.State,
            StateJson.Text(action, "unit"), TacticalQueries.Position(action["at"]!.AsObject()), context.Emit))
        .Register("endTurn", (context, _) => context.Lifecycle.AdvanceTurn(), handsOffTurn: true);
}
