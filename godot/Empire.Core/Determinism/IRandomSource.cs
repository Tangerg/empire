using System.Collections.Generic;

namespace Empire.Core.Determinism;

public interface IRandomSource
{
    string Id
    {
        get;
    }
    double Next(RandomState state, string stream);
    double Peek(RandomState state, string stream);
    int Int(RandomState state, string stream, int bound);
    double Int(RandomState state, string stream, double bound);
    bool Chance(RandomState state, string stream, double percent);
    T Pick<T>(RandomState state, string stream, IReadOnlyList<T> items);
}
