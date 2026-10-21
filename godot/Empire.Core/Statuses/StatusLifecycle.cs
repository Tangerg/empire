using System.Text.Json.Nodes;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Statuses;

public sealed class StatusLifecycle(ReadOnlyContent content, StatusBehaviorRegistry behaviors, ILifecycleEffects damage)
{
    public void Resolve(JsonObject state, IReadOnlyList<JsonObject> scope, Action<JsonObject> emit)
    {
        for (var index = 0; index < scope.Count; index++)
        {
            var unit = scope[index];
            foreach (var instance in Objects(unit["statuses"], "statuses").ToArray())
            {
                var definition = content.Source.Get("statuses", Text(instance, "id"));
                var context = new StatusLifecycleContext(content, damage, state, unit, instance, emit);
                if (definition["periodic"] is JsonObject periodic && Text(periodic, "timing") == "ownerTurnStart")
                {
                    var maximum = Number(content.Source.Get("units", Text(unit, "type")), "maxHp", 0);
                    var requested = Math.Max(1, Round(maximum * Number(periodic["maxHpFraction"], "maxHpFraction") * Number(instance["stacks"], "stacks")));
                    context.Damage(requested, IsTrue(periodic["nonlethal"]));
                    if (!Present())
                        break;
                }
                behaviors.OwnerTurnStart(context);
                if (!Present())
                    break;
                instance["remaining"] = Number(instance["remaining"], "remaining") - 1;
                if (Number(instance["remaining"], "remaining") <= 0 && Present())
                    StatusTransitions.Remove(unit, Text(instance, "id"), emit);

                bool Present() => Objects(state["units"], "units").Any(candidate => Integer(candidate, "id") == Integer(unit, "id"));
            }
        }
    }
}
