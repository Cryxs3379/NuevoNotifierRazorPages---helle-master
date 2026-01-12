using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NotifierAPI.Configuration;

namespace NotifierAPI.Services;

public class EsendexSendService : ISendService
{
    private readonly HttpClient _http;
    private readonly EsendexSettings _settings;
    private static readonly Regex E164 = new(@"^\+\d{6,15}$", RegexOptions.Compiled);

    public EsendexSendService(HttpClient http, IOptions<EsendexSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task<SendResult> SendAsync(string to, string message, string? accountRef, CancellationToken ct = default)
    {
        if (!E164.IsMatch(to))
            throw new ArgumentException("Destination number must be E.164 (+XXXXXXXX).", nameof(to));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        // Elegir accountRef: si viene en la request y es válida, usarla; si no, usar settings.
        var acc = string.IsNullOrWhiteSpace(accountRef) ? _settings.AccountReference : accountRef;
        acc = acc?.Trim();

        static bool IsValidAcc(string? s) => !string.IsNullOrWhiteSpace(s) && System.Text.RegularExpressions.Regex.IsMatch(s!, @"^EX\d{7}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Si es inválida (incluye EX000000), forzar a settings; si sigue inválida, lanzar error claro.
        if (!IsValidAcc(acc) || acc!.Equals("EX000000", StringComparison.OrdinalIgnoreCase))
        {
            acc = _settings.AccountReference?.Trim();
            if (!IsValidAcc(acc) || acc!.Equals("EX000000", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid Esendex AccountReference. Configure a real reference like EX0375657 in appsettings.");
        }

        // Log mínimo para depurar (sin exponer secreto)
        Console.WriteLine($"[EsendexSendService] Using accountreference={acc}");

        var payload = new
        {
            accountreference = acc,
            messages = new[] { new { to, body = message } }
        };

        // IMPORTANTE: no repetir v1.0 — la BaseAddress ya lo incluye.
        using var req = new HttpRequestMessage(HttpMethod.Post, "messagedispatcher")
        {
            Content = JsonContent.Create(payload) // Content-Type: application/json
        };
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Esendex send failed {(int)resp.StatusCode}: {body}");

        // Intentar leer el id del ACK JSON; si no, generar uno.
        try
        {
            using var doc = JsonDocument.Parse(body);
            var headers = doc.RootElement.GetProperty("messageheaders");
            var first = headers.EnumerateArray().FirstOrDefault();
            var id = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var idProp)
                ? idProp.GetString()
                : null;

            return new SendResult(id ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        }
        catch
        {
            return new SendResult(Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        }
    }
}