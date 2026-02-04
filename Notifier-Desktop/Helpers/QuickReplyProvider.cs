using System.Text.RegularExpressions;

namespace NotifierDesktop.Helpers;

public sealed class QuickReplyOption
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Lang { get; init; } = "en";
}

public static class QuickReplyProvider
{
    private const string DefaultLang = "en";

    public static List<QuickReplyOption> GetForPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return new List<QuickReplyOption>();
        }

        var digits = NormalizeToDigits(phone);
        var lang = ResolveLang(digits);

        return new List<QuickReplyOption>
        {
            new()
            {
                Id = $"courtesy-bus-{lang}",
                Lang = lang,
                Label = $"Bienvenida ({lang.ToUpperInvariant()})",
                Message = GetCourtesyBusMessage(lang)
            }
        };
    }

    private static string NormalizeToDigits(string phone)
    {
        var normalized = phone.Trim();
        if (normalized.StartsWith("00", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        normalized = normalized.TrimStart('+');
        normalized = Regex.Replace(normalized, @"\D", "");
        return normalized;
    }

    private static string ResolveLang(string digits)
    {
        if (digits.StartsWith("34", StringComparison.Ordinal)) return "es";
        if (digits.StartsWith("44", StringComparison.Ordinal)) return "en";
        if (digits.StartsWith("49", StringComparison.Ordinal)) return "de";
        if (digits.StartsWith("45", StringComparison.Ordinal)) return "da";
        if (digits.StartsWith("33", StringComparison.Ordinal)) return "fr";
        return DefaultLang;
    }

    private static string GetCourtesyBusMessage(string lang)
    {
        return lang switch
        {
            "es" => "Bienvenido/a a Málaga. Nuestro autobús de cortesía está de camino para recogerle.",
            "en" => "Welcome to Malaga. Our courtesy bus is on its way to collect you.",
            "de" => "Willkommen in Málaga. Unser kostenloser Shuttlebus ist unterwegs, um Sie abzuholen.",
            "da" => "Velkommen til Málaga. Vores gratis shuttlebus er på vej for at hente dig.",
            "fr" => "Bienvenue à Malaga. Notre navette gratuite est en route pour venir vous chercher.",
            _ => "Welcome to Malaga. Our courtesy bus is on its way to collect you."
        };
    }
}
