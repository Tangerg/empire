using System;
using System.Text.Json.Nodes;

namespace Empire.Core.State;

internal static class StateMap
{
    internal static JsonObject Create(StateContent content, JsonObject level)
    {
        var widthValue = StateJson.Number(level["width"], "width");
        var heightValue = StateJson.Number(level["height"], "height");
        if (widthValue != Math.Truncate(widthValue) || heightValue != Math.Truncate(heightValue) ||
            widthValue < 1 || heightValue < 1 || widthValue * heightValue > int.MaxValue)
            throw new StoredDocumentException($"bad map size {widthValue}x{heightValue}");
        var width = (int)widthValue;
        var height = (int)heightValue;
        var size = checked(width * height);
        var terrain = StateJson.Array(level["terrain"], "terrain");
        if (terrain.Count != height)
            throw new StoredDocumentException($"terrain has {terrain.Count} rows, expected {height}");

        // The TypeScript constructor asks for the catalog default even when every tile is authored.
        _ = content.DefaultTerrain;
        var tiles = new JsonArray();
        for (var y = 0; y < height; y++)
        {
            var row = StateJson.Text(terrain[y], "terrain row");
            if (row.Length != width)
                throw new StoredDocumentException($"terrain row {y} has {row.Length} chars, expected {width}");
            for (var x = 0; x < width; x++)
            {
                var character = row[x].ToString();
                var tile = content.Terrain(character);
                if (tile is null)
                    throw new StoredDocumentException($"unknown terrain char \"{character}\" at {x},{y}");
                tiles.Add(tile);
            }
        }
        var elevation = level["elevation"] is { } authoredElevation
            ? StateJson.CopyArray(authoredElevation, "elevation") : StateJson.Zeros(size);
        if (elevation.Count != size)
            throw new StoredDocumentException($"elevation has {elevation.Count} cells, expected {size}");
        foreach (var node in elevation)
        {
            var value = StateJson.Number(node, "elevation value");
            if (value != Math.Truncate(value))
                throw new StoredDocumentException("elevation values must be integers");
        }

        var cliffs = new JsonArray();
        foreach (var edge in StateJson.Objects(level["cliffs"], "cliffs"))
        {
            var from = StateJson.Object(edge["from"], "cliff from");
            var to = StateJson.Object(edge["to"], "cliff to");
            var fromX = StateJson.Number(from["x"], "cliff x");
            var fromY = StateJson.Number(from["y"], "cliff y");
            var toX = StateJson.Number(to["x"], "cliff x");
            var toY = StateJson.Number(to["y"], "cliff y");
            if (!ValidCell(fromX, fromY, width, height) || !ValidCell(toX, toY, width, height) ||
                Math.Abs(fromX - toX) + Math.Abs(fromY - toY) != 1)
                throw new StoredDocumentException($"invalid cliff edge {fromX},{fromY} -> {toX},{toY}");
            cliffs.Add(new JsonObject { ["from"] = from.DeepClone(), ["to"] = to.DeepClone() });
        }

        var directionalCover = new JsonArray();
        foreach (var cover in StateJson.Objects(level["directionalCover"], "directionalCover"))
        {
            var at = StateJson.Object(cover["at"], "directional cover at");
            var x = StateJson.Number(at["x"], "cover x");
            var y = StateJson.Number(at["y"], "cover y");
            if (!InBounds(x, y, width, height))
                throw new StoredDocumentException($"directional cover out of bounds at {x},{y}");
            var sides = StateJson.Object(cover["sides"], "directional cover sides");
            foreach (var pair in sides)
            {
                var strength = pair.Value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
                    ? text : pair.Value?.ToJsonString() ?? "null";
                if (strength != "half" && strength != "full")
                    throw new StoredDocumentException($"invalid directional cover {pair.Key}:{strength}");
            }
            directionalCover.Add(new JsonObject { ["at"] = at.DeepClone(), ["sides"] = sides.DeepClone() });
        }

        var owners = StateJson.Zeros(size);
        foreach (var owned in StateJson.Objects(level["owners"], "owners"))
        {
            var x = StateJson.Integer(owned, "x");
            var y = StateJson.Integer(owned, "y");
            if (!InBounds(x, y, width, height))
                throw new StoredDocumentException($"owner entry out of bounds at {x},{y}");
            var index = y * width + x;
            var tile = StateJson.Text(tiles[index], "terrain");
            if (!StateJson.IsTrue(content.Get("terrains", tile)["capturable"]))
                throw new StoredDocumentException($"tile {x},{y} ({tile}) cannot be owned");
            owners[index] = StateJson.Required(owned, "owner").DeepClone();
        }
        return new JsonObject
        {
            ["width"] = width,
            ["height"] = height,
            ["tiles"] = tiles,
            ["owners"] = owners,
            ["captureProgress"] = StateJson.Zeros(size),
            ["elevation"] = elevation,
            ["cliffs"] = cliffs,
            ["directionalCover"] = directionalCover,
        };
    }

    private static bool InBounds(double x, double y, int width, int height) =>
        x >= 0 && y >= 0 && x < width && y < height;

    private static bool ValidCell(double x, double y, int width, int height) =>
        x == Math.Truncate(x) && y == Math.Truncate(y) && InBounds(x, y, width, height);
}
