using System.Text.RegularExpressions;

namespace Notifier.Messages.Domain;

public sealed record PhoneNumber
{
    private static readonly Regex E164Regex = new(@"^\+\d{6,15}$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnly = new(@"^\d{6,15}$", RegexOptions.Compiled);

    private PhoneNumber(string e164)
    {
        E164 = e164;
        Canonical = e164.StartsWith("+") ? e164[1..] : e164;
    }

    public string E164 { get; }
    public string Canonical { get; }

    public static bool TryParse(string? input, out PhoneNumber phoneNumber)
    {
        phoneNumber = null!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = Regex.Replace(input.Trim(), @"[\s-]", "");

        if (normalized.StartsWith("00", StringComparison.Ordinal) && normalized.Length > 2)
        {
            normalized = "+" + normalized[2..];
        }

        if (!normalized.StartsWith("+", StringComparison.Ordinal) && DigitsOnly.IsMatch(normalized))
        {
            normalized = "+" + normalized;
        }

        if (!E164Regex.IsMatch(normalized))
        {
            return false;
        }

        phoneNumber = new PhoneNumber(normalized);
        return true;
    }
}
