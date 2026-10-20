namespace Empire.Core.Geometry;

internal static class GridDirections
{
    internal static readonly IReadOnlyList<DirectionDef> Square = Array.AsReadOnly(new DirectionDef[]
    {
        new("north", "北"), new("east", "东"), new("south", "南"), new("west", "西"),
    });

    internal static string Opposite(string grid, string direction) => direction switch
    {
        "north" => "south",
        "south" => "north",
        "east" => "west",
        "west" => "east",
        "northeast" => "southwest",
        "southwest" => "northeast",
        "southeast" => "northwest",
        "northwest" => "southeast",
        "hexEast" => "hexWest",
        "hexWest" => "hexEast",
        "hexNortheast" => "hexSouthwest",
        "hexSouthwest" => "hexNortheast",
        "hexNorthwest" => "hexSoutheast",
        "hexSoutheast" => "hexNorthwest",
        _ => throw Unknown(grid, direction),
    };

    internal static ArgumentOutOfRangeException Unknown(string grid, string direction) =>
        new(nameof(direction), direction, $"grid \"{grid}\" has no direction \"{direction}\"");

    // ECMAScript resolves exact halves toward +infinity, including negative halves.
    internal static double Round(double value)
    {
        var floor = Math.Floor(value);
        return value - floor < 0.5 ? floor : floor + 1;
    }
}
