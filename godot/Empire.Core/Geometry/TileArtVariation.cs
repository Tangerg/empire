using Empire.Core.Determinism;

namespace Empire.Core.Geometry;

/// <summary>Legacy per-tile visual variation, never a gameplay random source.</summary>
public static class TileArtVariation
{
    public static double Hash(double x, double y, double salt = 0)
    {
        var bits = EcmaNumber.ToUint32(x * 374761393 + y * 668265263 + salt * 2246822519);
        var mixed = unchecked((int)(bits ^ (bits >> 13)));
        // The source multiplies a signed JS number as a double, then truncates at
        // the next bitwise operation. Integer multiplication gives different art.
        bits = EcmaNumber.ToUint32(mixed * 1274126177.0);
        bits ^= bits >> 16;
        return bits % 100000 / 100000.0;
    }
}
