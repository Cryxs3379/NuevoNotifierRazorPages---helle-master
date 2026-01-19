namespace NotifierDesktop.Helpers;

/// <summary>
/// Helper para normalizar números telefónicos al formato canónico (sin '+')
/// Formato canónico: SIEMPRE sin '+', sin espacios, sin guiones, sin paréntesis
/// 
/// NOTA: Esta implementación es PERMISIVA (retorna string vacío en lugar de lanzar excepciones).
/// Esto es intencional para la UI de Desktop, donde es preferible manejar errores de forma silenciosa
/// en lugar de interrumpir la experiencia del usuario.
/// 
/// Para validación estricta, usar NotifierAPI.Helpers.PhoneNormalizer que lanza ArgumentException.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Normaliza un número telefónico al formato canónico (sin '+')
    /// </summary>
    /// <param name="input">Número telefónico en cualquier formato</param>
    /// <returns>Número normalizado sin '+', o string vacío si no se puede normalizar (comportamiento permisivo para UI)</returns>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Trim
        var normalized = input.Trim();

        // Eliminar espacios, guiones y paréntesis
        normalized = normalized.Replace(" ", "")
                              .Replace("-", "")
                              .Replace("(", "")
                              .Replace(")", "");

        // Si empieza por '+', quitarlo (formato canónico: sin '+')
        if (normalized.StartsWith("+"))
        {
            normalized = normalized.Substring(1);
        }

        // Validar que no esté vacío después de normalizar
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        // Validar que solo contiene dígitos y tiene longitud válida
        if (normalized.Length < 6 || normalized.Length > 15 || !System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\d+$"))
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
