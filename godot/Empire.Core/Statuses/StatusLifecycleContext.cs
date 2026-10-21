using System.Text.Json.Nodes;
using Empire.Core.Lifecycle;
using Empire.Core.State;

namespace Empire.Core.Statuses;

public sealed class StatusLifecycleContext(ReadOnlyContent content, ILifecycleEffects damage,
    JsonObject state, JsonObject unit, JsonObject status, Action<JsonObject> emit)
{
    public ReadOnlyContent Content { get; } = content;
    public JsonObject State { get; } = state;
    public JsonObject Unit { get; } = unit;
    public JsonObject Status { get; } = status;
    public Action<JsonObject> Emit { get; } = emit;
    public double Damage(double requested, bool nonlethal = false) => damage.DamageStatus(State, Unit, Status, requested, nonlethal, Emit);
}
