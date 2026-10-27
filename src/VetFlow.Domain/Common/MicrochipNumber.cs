namespace VetFlow.Domain.Common;

/// <summary>
/// ISO 11784/11785 FDX-B pet microchips carry a 15-digit code. The first three digits are either an ISO 3166
/// country code (e.g. 792 = Türkiye) or a manufacturer code (900–998).
/// </summary>
public static class MicrochipNumber
{
    public static bool IsValid(string? value)
    {
        var digits = Normalize(value);
        return digits.Length == 15 && digits.All(char.IsAsciiDigit);
    }

    public static string Normalize(string? value) =>
        value is null ? string.Empty : new string(value.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    /// <summary>"792" → "Country code 792", "981" → "Manufacturer code 981".</summary>
    public static string DescribePrefix(string value)
    {
        var prefix = int.Parse(Normalize(value)[..3]);
        return prefix is >= 900 and <= 998 ? $"Manufacturer code {prefix}" : $"Country code {prefix:D3}";
    }
}
