using System;
using System.Globalization;

namespace Empire.Core.Determinism;

internal static class EcmaNumber
{
    internal static uint ToUint32(double value)
    {
        if (!double.IsFinite(value) || value == 0) return 0;
        var remainder = Math.Truncate(value) % 4294967296.0;
        if (remainder < 0) remainder += 4294967296.0;
        return (uint)remainder;
    }

    internal static string Format(double value)
    {
        // Unlike JSON.stringify's lossy null substitution, reject invalid state.
        if (!double.IsFinite(value))
            throw new DomainInvariantException("state digest cannot contain a non-finite number");
        if (value == 0) return "0";
        if (value < 0) return "-" + Format(-value);

        // Modern .NET's R format supplies shortest-roundtrip significant digits.
        // ECMAScript has different fixed/scientific cutoffs and exponent spelling.
        var shortest = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = shortest.IndexOf('E');
        if (exponentAt < 0) exponentAt = shortest.IndexOf('e');
        var mantissa = exponentAt < 0 ? shortest : shortest[..exponentAt];
        var exponent = exponentAt < 0 ? 0
            : int.Parse(shortest[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var decimalAt = mantissa.IndexOf('.');
        var decimalPosition = (decimalAt < 0 ? mantissa.Length : decimalAt) + exponent;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal);

        var firstDigit = 0;
        while (firstDigit < digits.Length - 1 && digits[firstDigit] == '0')
            firstDigit++;
        decimalPosition -= firstDigit;
        digits = digits[firstDigit..].TrimEnd('0');

        if (digits.Length <= decimalPosition && decimalPosition <= 21)
            return digits + new string('0', decimalPosition - digits.Length);
        if (0 < decimalPosition && decimalPosition <= 21)
            return digits[..decimalPosition] + "." + digits[decimalPosition..];
        if (-6 < decimalPosition && decimalPosition <= 0)
            return "0." + new string('0', -decimalPosition) + digits;

        var scientificExponent = decimalPosition - 1;
        var significand = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
        return significand + "e" + (scientificExponent >= 0 ? "+" : "")
            + scientificExponent.ToString(CultureInfo.InvariantCulture);
    }
}
