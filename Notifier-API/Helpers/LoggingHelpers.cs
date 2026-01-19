namespace NotifierAPI.Helpers;

/// <summary>
/// Helpers para logging seguro (enmascarar PII)
/// </summary>
public static class LoggingHelpers
{
    /// <summary>
    /// Enmascara un número telefónico mostrando solo los primeros 6 dígitos
    /// </summary>
    /// <param name="phone">Número telefónico a enmascarar</param>
    /// <returns>Número enmascarado (ej: "34612345XXXX" o "XXXX" si es muy corto)</returns>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "XXXX";

        var normalized = phone.Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("+", "");

        if (normalized.Length <= 6)
            return "XXXX";

        // Mostrar primeros 6 dígitos + "XXXX"
        return normalized.Substring(0, Math.Min(6, normalized.Length)) + "XXXX";
    }
}
