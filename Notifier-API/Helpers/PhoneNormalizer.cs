namespace NotifierAPI.Helpers;

/// <summary>
/// Helper para normalizar números telefónicos al formato canónico (sin '+')
/// 
/// NOTA: Esta implementación es ESTRICTA (lanza ArgumentException en caso de error).
/// Esto es intencional para la API, donde se requiere validación explícita y fallo rápido
/// si los datos de entrada no son válidos.
/// 
/// Para normalización permisiva (retorna string vacío), usar NotifierDesktop.Helpers.PhoneNormalizer.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Normaliza un número telefónico al formato canónico (sin '+', sin espacios, sin guiones, sin paréntesis)
    /// </summary>
    /// <param name="phone">Número telefónico a normalizar</param>
    /// <returns>Número normalizado sin '+'</returns>
    /// <exception cref="ArgumentException">Si el teléfono es null o vacío después de normalizar (comportamiento estricto para API)</exception>
    public static string NormalizePhone(string? phone)
    {
        if (phone == null)
            throw new ArgumentException("Phone cannot be null", nameof(phone));

        // Trim
        var normalized = phone.Trim();

        // Eliminar espacios, guiones y paréntesis
        normalized = normalized.Replace(" ", "")
                              .Replace("-", "")
                              .Replace("(", "")
                              .Replace(")", "");

        // Si empieza por '+', quitarlo (formato canónico: sin '+')
        if (normalized.StartsWith("+"))
        {
            System.Diagnostics.Debug.WriteLine($"[PhoneNormalizer] Detected phone with '+': '{phone}' -> removing '+'");
            normalized = normalized.Substring(1);
        }

        // Validar que no esté vacío después de normalizar
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Phone cannot be empty after normalization", nameof(phone));
        }

        return normalized;
    }

    /// <summary>
    /// Alias para NormalizePhone (compatibilidad con requerimientos)
    /// </summary>
    public static string Normalize(string? phone) => NormalizePhone(phone);

    /// <summary>
    /// Intenta normalizar un teléfono y retorna si fue exitoso
    /// </summary>
    public static bool TryNormalizePhone(string? phone, out string normalized)
    {
        normalized = string.Empty;
        try
        {
            normalized = NormalizePhone(phone);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
