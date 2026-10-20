using Empire.Core.Registries;

namespace Empire.Core.Abilities;

public sealed class AbilityRegistry : KeyedRegistry<AbilityDefinition, AbilityRegistry>
{
    public AbilityRegistry() : base("ability") { }
    protected override string KeyOf(AbilityDefinition entry) => entry.Id;
    public AbilityRegistry Clone() => CopyInto(new AbilityRegistry());
}
