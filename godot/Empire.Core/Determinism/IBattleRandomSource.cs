using System.Text.Json.Nodes;

namespace Empire.Core.Determinism;

public interface IBattleRandomSource
{
    string Id
    {
        get;
    }
    double Next(JsonObject state, string stream);
    double Peek(JsonObject state, string stream);
    double Int(JsonObject state, string stream, double bound);
    bool Chance(JsonObject state, string stream, double percent);
    T Pick<T>(JsonObject state, string stream, IReadOnlyList<T> items);
}
