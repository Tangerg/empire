namespace Empire.Core.Composition;

public sealed class DependencyOrderOptions<T>
{
    public required Func<T, string> IdOf
    {
        get; init;
    }
    public required Func<T, IReadOnlyList<string>> DependenciesOf
    {
        get; init;
    }
    public Func<string, bool>? IsSatisfiedExternally
    {
        get; init;
    }
    public required Func<string, Exception> Duplicate
    {
        get; init;
    }
    public required Func<T, string, Exception> Missing
    {
        get; init;
    }
    public required Func<IReadOnlyList<string>, Exception> Cycle
    {
        get; init;
    }
}

public static class DependencyOrder
{
    public static IReadOnlyList<T> Order<T>(IReadOnlyList<T> items, DependencyOrderOptions<T> options)
    {
        var byId = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = options.IdOf(item);
            if (!byId.TryAdd(id, item))
                throw options.Duplicate(id);
        }

        var visiting = new Dictionary<string, int>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        var ordered = new List<T>();

        void Visit(T item)
        {
            var id = options.IdOf(item);
            if (visited.Contains(id))
                return;
            if (visiting.TryGetValue(id, out var cycleStart))
                throw options.Cycle(path.Skip(cycleStart).Append(id).ToArray());

            visiting.Add(id, path.Count);
            path.Add(id);
            foreach (var dependency in options.DependenciesOf(item))
            {
                if (byId.TryGetValue(dependency, out var pending))
                    Visit(pending);
                else if (options.IsSatisfiedExternally?.Invoke(dependency) != true)
                    throw options.Missing(item, dependency);
            }
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            ordered.Add(item);
        }

        foreach (var item in items)
            Visit(item);
        return ordered.AsReadOnly();
    }
}
