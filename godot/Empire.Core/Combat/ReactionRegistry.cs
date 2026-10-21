using Empire.Core.Registries;

namespace Empire.Core.Combat;

public sealed record ReactionBehavior(string Id, string Name, bool Intercepts, double IncomingMultiplier, bool Retaliates, bool ConservesResources, string Hint);

public sealed class ReactionRegistry() : KeyedRegistry<ReactionBehavior, ReactionRegistry>("reaction stance")
{
    protected override string KeyOf(ReactionBehavior entry) => entry.Id;
    public ReactionRegistry Clone() => CopyInto(new ReactionRegistry());
    public static ReactionRegistry Core() => new ReactionRegistry()
        .Register(new("counter", "反击", false, 1, true, false, "被攻击时用射程覆盖得到的最强武器还击。"))
        .Register(new("guard", "防御", false, 0.7, false, false, "减少受到的伤害，但放弃还击。"))
        .Register(new("support", "援护", true, 1, true, false, "替相邻友军挡下攻击，每轮一次。"))
        .Register(new("conserve", "节制", false, 1, true, true, "只用无消耗的武器还击，保留弹药与冷却。"));
}
