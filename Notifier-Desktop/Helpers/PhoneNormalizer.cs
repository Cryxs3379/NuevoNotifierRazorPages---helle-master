namespace NotifierDesktop.Helpers;

public static class PhoneNormalizer
{
    /// <summary>
    /// Normaliza un número telefónico a formato E.164 (+XXXXXXXX)
    /// </summary>
    /// <param name="input">Número telefónico en cualquier formato</param>
    /// <returns>Número normalizado en formato E.164, o string vacío si no se puede normalizar</returns>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Trim y quitar espacios
        var normalized = input.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        // Si empieza con "00", convertir a "+"
        if (normalized.StartsWith("00") && normalized.Length > 2)
        {
            normalized = "+" + normalized.Substring(2);
        }

        // Si no empieza con '+', añadirlo
        if (!normalized.StartsWith("+"))
        {
            normalized = "+" + normalized;
        }

        // Validar que después del '+' solo hay dígitos y que tiene longitud válida
        var digitsOnly = normalized.Substring(1);
        if (digitsOnly.Length < 6 || digitsOnly.Length > 15 || !System.Text.RegularExpressions.Regex.IsMatch(digitsOnly, @"^\d+$"))
        {
            return string.Empty; // No válido
        }

        return normalized;
    }

    /// <summary>
    /// Intenta normalizar y retorna si fue exitoso
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = Normalize(input);
        return !string.IsNullOrEmpty(normalized);
    }
}
