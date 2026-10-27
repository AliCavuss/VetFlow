namespace VetFlow.Domain.Common;

/// <summary>
/// Normalises Turkish phone numbers to E.164 (<c>+90XXXXXXXXXX</c>).
/// Accepts "0532 123 45 67", "+90 (532) 123-45-67", "5321234567", "0212 555 12 34" …
/// The 10-digit national number must start with 2, 3, 4 (landline) or 5 (mobile).
/// </summary>
public static class TurkishPhone
{
    public static bool IsValid(string? value) => TryNormalize(value, out _);

    public static string Normalize(string value) =>
        TryNormalize(value, out var normalized)
            ? normalized
            : throw new DomainException($"'{value}' is not a valid Turkish phone number.");

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12) digits = digits[2..];
        else if (digits.StartsWith('0') && digits.Length == 11) digits = digits[1..];

        if (digits.Length != 10 || digits[0] is not ('2' or '3' or '4' or '5'))
        {
            return false;
        }

        normalized = "+90" + digits;
        return true;
    }

    /// <summary>"+905321234567" → "+90 532 123 45 67".</summary>
    public static string Format(string? e164)
    {
        if (e164 is null || e164.Length != 13 || !e164.StartsWith("+90"))
        {
            return e164 ?? string.Empty;
        }

        var n = e164[3..];
        return $"+90 {n[..3]} {n[3..6]} {n[6..8]} {n[8..]}";
    }
}
