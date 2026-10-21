namespace Empire.Core.Registries;

/// <summary>A consumer's read port over the actual registry owner, not a copied set of feature names.</summary>
public interface IKeyedRegistryView
{
    bool Has(string key);
    IReadOnlyList<string> Keys();
}
