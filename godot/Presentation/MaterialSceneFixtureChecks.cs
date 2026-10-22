#nullable enable
using System.Text.Json.Nodes;

namespace Empire.Presentation;

/// <summary>Small authored material contract, deliberately independent of campaign artwork and balance.</summary>
internal static class MaterialSceneFixtureChecks
{
    internal static AuthoredScene Scene => new()
    {
        LevelId = "material-contract",
        MapSize = [8, 5],
        RuntimeReady = false,
        FramePlacements = [],
        RoadCorridor = new()
        {
            Width = 20,
            MaxTangentCut = 16,
            MaxPlainOverhang = 0,
            ProtectedCells = [[3, 2]]
        },
        River = new()
        {
            Width = 24,
            BankWidth = 34,
            Knots = [[3.5, 0], [3.5, 2.5], [3.5, 5]],
            Widths = [24, 16, 24]
        }
    };
    internal static JsonObject Level => new()
    {
        ["schema"] = 3,
        ["id"] = Scene.LevelId,
        ["width"] = 8,
        ["height"] = 5,
        ["terrain"] = new JsonArray("_..~..._", ".T.~.h..", "---=----", "...~....", "_..~..._"),
        ["units"] = new JsonArray(
            new JsonObject { ["unit"] = "c01.swordsman", ["owner"] = 1, ["x"] = 1, ["y"] = 3 },
            new JsonObject { ["unit"] = "c01.swordsman", ["owner"] = 2, ["x"] = 6, ["y"] = 3 }),
        ["players"] = new JsonArray(
            new JsonObject { ["id"] = 1, ["color"] = "#4d86b8", ["controller"] = "human" },
            new JsonObject { ["id"] = 2, ["color"] = "#a8493f", ["controller"] = "ai" })
    };
}
