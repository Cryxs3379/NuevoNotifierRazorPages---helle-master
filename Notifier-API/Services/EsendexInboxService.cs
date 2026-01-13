using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using NotifierAPI.Configuration;
using NotifierAPI.Models;

namespace NotifierAPI.Services;

public class EsendexInboxService : IInboxService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EsendexInboxService> _logger;
    private readonly EsendexSettings _settings;
    private readonly string _username;
    private readonly string _apiPassword;

    public EsendexInboxService(
        HttpClient httpClient,
        ILogger<EsendexInboxService> logger,
        EsendexSettings settings,
        string username,
        string apiPassword)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
        _username = username;
        _apiPassword = apiPassword;

        // Configure Basic Auth headers (BaseAddress and Timeout are already configured in Program.cs)
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_apiPassword}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        
        // Set Accept header based on PreferredFormat
        if (_settings.PreferredFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 1.0));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 1.0));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.9));
        }
    }

    public async Task<MessagesResponse> GetMessagesAsync(
        string direction, 
        int page, 
        int pageSize, 
        string? accountRef = null,
        CancellationToken cancellationToken = default)
    {
        var startIndex = (page - 1) * pageSize;

        // Try endpoints in order (without leading slash to preserve BaseAddress path)
        var endpoints = BuildEndpoints(pageSize, startIndex, direction, accountRef);

        // Try primary base URL first
        var result = await TryEndpointsWithBaseUrl(_httpClient.BaseAddress!.ToString(), endpoints, cancellationToken);
        
        // If failed and alternative URL exists, try it
        if (result == null && !string.IsNullOrEmpty(_settings.AlternativeBaseUrl))
        {
            _logger.LogWarning("All endpoints failed with primary URL, trying alternative URL: {AlternativeUrl}", 
                _settings.AlternativeBaseUrl.Replace(_apiPassword, "***"));
            
            result = await TryEndpointsWithBaseUrl(_settings.AlternativeBaseUrl, endpoints, cancellationToken);
        }

        if (result == null)
        {
            _logger.LogError("All Esendex endpoints failed with all base URLs");
            throw new HttpRequestException("Esendex service unavailable", null, HttpStatusCode.BadGateway);
        }

        // Parse response
        var content = await result.Content.ReadAsStringAsync(cancellationToken);
        var esendexResponse = ParseEsendexResponse(content, page, pageSize);

        return esendexResponse;
    }

    private string[] BuildEndpoints(int pageSize, int startIndex, string direction, string? accountRef)
    {
        var accountRefParam = !string.IsNullOrEmpty(accountRef) 
            ? $"&accountreference={Uri.EscapeDataString(accountRef)}" 
            : "";

        // Build endpoints based on direction
        if (direction.Equals("outbound", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                $"messages?direction=outbound&pagesize={pageSize}&startindex={startIndex}{accountRefParam}",
                $"messageheaders?inbound=false&pagesize={pageSize}&startindex={startIndex}{accountRefParam}"
            };
        }
        else // inbound (default)
        {
            return new[]
            {
                $"inbox/messages?pagesize={pageSize}&startindex={startIndex}{accountRefParam}",
                $"messages?direction=inbound&pagesize={pageSize}&startindex={startIndex}{accountRefParam}",
                $"messageheaders?inbound=true&pagesize={pageSize}&startindex={startIndex}{accountRefParam}"
            };
        }
    }

    private async Task<HttpResponseMessage?> TryEndpointsWithBaseUrl(
        string baseUrl,
        string[] endpoints,
        CancellationToken cancellationToken)
    {
        foreach (var endpoint in endpoints)
        {
            try
            {
                // Combine URLs properly
                var fullUrl = CombineUrls(baseUrl, endpoint);
                
                _logger.LogDebug("Attempting Esendex endpoint: {Url}", SanitizeUrl(fullUrl));

                var response = await _httpClient.GetAsync(fullUrl, cancellationToken);

                // Log response (without sensitive data)
                _logger.LogInformation("Esendex endpoint {Endpoint} returned {StatusCode}", 
                    endpoint.Split('?')[0], 
                    (int)response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogError("Esendex authentication failed with status {StatusCode}", response.StatusCode);
                    throw new UnauthorizedAccessException(
                        "Esendex authentication failed — check ESENDEX_USER / ESENDEX_API_PASSWORD");
                }

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully connected to Esendex endpoint: {Endpoint}", endpoint.Split('?')[0]);
                    return response;
                }

                _logger.LogWarning("Esendex endpoint {Endpoint} returned {StatusCode}", endpoint.Split('?')[0], response.StatusCode);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calling Esendex endpoint {Endpoint}", endpoint.Split('?')[0]);
            }
        }

        return null;
    }

    private string CombineUrls(string baseUrl, string relativeUrl)
    {
        // Ensure baseUrl ends with / and relativeUrl doesn't start with /
        baseUrl = baseUrl.TrimEnd('/') + '/';
        relativeUrl = relativeUrl.TrimStart('/');
        
        return baseUrl + relativeUrl;
    }

    private async Task<string?> FetchBodyFromUri(string bodyUri, CancellationToken ct)
    {
        try
        {
            string fullUri;
            
            // Determinar si es URL absoluta o relativa
            if (Uri.TryCreate(bodyUri, UriKind.Absolute, out var absoluteUri))
            {
                // URL absoluta: usar tal cual
                fullUri = bodyUri;
                _logger.LogDebug("Fetching body from absolute URI: {Uri}", SanitizeUrl(fullUri));
            }
            else
            {
                // URL relativa: combinar con BaseAddress
                var baseAddress = _httpClient.BaseAddress?.ToString() ?? "";
                fullUri = CombineUrls(baseAddress, bodyUri);
                _logger.LogDebug("Fetching body from relative URI: {Uri}", SanitizeUrl(fullUri));
            }

            var response = await _httpClient.GetAsync(fullUri, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogError("Esendex authentication failed when fetching body URI with status {StatusCode}", response.StatusCode);
                throw new UnauthorizedAccessException("Esendex authentication failed");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch body from URI: {StatusCode}", response.StatusCode);
                return null;
            }

            var bodyContent = await response.Content.ReadAsStringAsync(ct);
            
            // Intentar parsear como XML para extraer bodytext
            try
            {
                var xdoc = XDocument.Parse(bodyContent);
                XNamespace ns = "http://api.esendex.com/ns/";
                
                // Buscar <bodytext> en el XML (puede estar en diferentes niveles)
                var bodyTextElement = xdoc.Descendants(ns + "bodytext").FirstOrDefault();
                if (bodyTextElement != null)
                {
                    // Extraer solo el texto, sin etiquetas XML
                    var cleanText = bodyTextElement.Value;
                    _logger.LogDebug("Successfully extracted bodytext from XML response, length: {Length}", cleanText.Length);
                    return cleanText;
                }
                
                // Si no hay bodytext, buscar <body> con texto
                var bodyElement = xdoc.Descendants(ns + "body").FirstOrDefault();
                if (bodyElement != null && !string.IsNullOrWhiteSpace(bodyElement.Value))
                {
                    var cleanText = bodyElement.Value;
                    _logger.LogDebug("Successfully extracted body from XML response, length: {Length}", cleanText.Length);
                    return cleanText;
                }
                
                // Si no se encuentra estructura XML esperada, devolver el contenido tal cual
                _logger.LogDebug("Body response is not XML or doesn't contain expected elements, returning raw content");
            }
            catch (Exception ex)
            {
                // Si no es XML válido o falla el parseo, devolver el contenido tal cual
                _logger.LogDebug(ex, "Body response is not valid XML, returning raw content");
            }
            
            _logger.LogDebug("Successfully fetched body from URI (raw), length: {Length}", bodyContent.Length);
            return bodyContent;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching body from URI: {Uri}", SanitizeUrl(bodyUri));
            return null;
        }
    }

    private string SanitizeUrl(string url)
    {
        // Remove credentials from URL for logging
        var uri = new Uri(url);
        return $"{uri.Scheme}://{uri.Host}{uri.PathAndQuery}";
    }

    private string CleanTextContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Si el texto parece ser XML, intentar extraer solo el contenido de texto
        if (text.TrimStart().StartsWith("<"))
        {
            try
            {
                var xdoc = XDocument.Parse(text);
                // Extraer todo el texto del documento sin etiquetas
                return string.Join(" ", xdoc.Descendants()
                    .Where(e => !e.HasElements)
                    .Select(e => e.Value))
                    .Trim();
            }
            catch
            {
                // Si no es XML válido, devolver tal cual
            }
        }

        return text.Trim();
    }

    private MessagesResponse ParseEsendexResponse(string content, int page, int pageSize)
    {
        try
        {
            // Esendex returns XML, not JSON
            var xdoc = XDocument.Parse(content);
            XNamespace ns = "http://api.esendex.com/ns/";

            var messages = new List<MessageDto>();
            
            // Parse messageheaders
            var messageHeaders = xdoc.Descendants(ns + "messageheader");
            var messageCount = 0;
            
            foreach (var header in messageHeaders)
            {
                var message = MapEsendexXmlMessage(header, ns);
                messages.Add(message);
                messageCount++;
            }

            // Get total count from root element
            var root = xdoc.Root;
            int total = messages.Count; // Default
            
            if (root != null)
            {
                var totalCountAttr = root.Attribute("totalcount");
                if (totalCountAttr != null && int.TryParse(totalCountAttr.Value, out var parsedTotal))
                {
                    total = parsedTotal;
                }
            }

            // Log summary (no sensitive data, no message bodies)
            _logger.LogInformation(
                "Parsed {Count} messages from Esendex (page {Page}, pageSize {PageSize}, total {Total}). Message IDs: {Ids}",
                messageCount,
                page,
                pageSize,
                total,
                string.Join(", ", messages.Take(3).Select(m => m.Id.Substring(0, Math.Min(8, m.Id.Length)) + "...")));

            return new MessagesResponse
            {
                Items = messages,
                Page = page,
                PageSize = pageSize,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Esendex XML response. Content length: {Length}", content.Length);
            throw new InvalidOperationException("Failed to parse Esendex response", ex);
        }
    }

    private MessageDto MapEsendexXmlMessage(XElement header, XNamespace ns)
    {
        var idAttr = header.Attribute("id");
        var id = idAttr?.Value ?? Guid.NewGuid().ToString();

        // Get phone numbers
        var fromElement = header.Element(ns + "from")?.Element(ns + "phonenumber");
        var toElement = header.Element(ns + "to")?.Element(ns + "phonenumber");
        
        var from = fromElement?.Value ?? "";
        var to = toElement?.Value ?? "";

        // Get message body/summary
        var summary = header.Element(ns + "summary")?.Value ?? "";
        var body = header.Element(ns + "body")?.Value;
        var messageText = string.IsNullOrWhiteSpace(body) ? summary : body;

        // Get received date
        var receivedAtStr = header.Element(ns + "receivedat")?.Value 
            ?? header.Element(ns + "submittedat")?.Value 
            ?? header.Element(ns + "sentat")?.Value;
        
        var receivedUtc = ParseDateTime(receivedAtStr);

        return new MessageDto
        {
            Id = id,
            From = from,
            To = to,
            Message = messageText,
            ReceivedUtc = receivedUtc
        };
    }

    private DateTime ParseDateTime(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return DateTime.UtcNow;
        }

        // Handle various date formats from Esendex
        if (DateTime.TryParse(dateString, out var dateTime))
        {
            // Ensure UTC
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                // Assume UTC if no timezone info
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }
            return dateTime.ToUniversalTime();
        }

        _logger.LogWarning("Could not parse date: {DateString}, using current UTC time", dateString);
        return DateTime.UtcNow;
    }

    public bool IsConfigured() => true;

    public async Task<bool> DeleteMessageAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            // Solo soportamos eliminar mensajes ENTRANTES del inbox
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"inbox/messages/{Uri.EscapeDataString(id)}");
            var resp = await _httpClient.SendAsync(req, ct);
            _logger.LogInformation("Esendex delete inbox/messages/{Id} -> {Status}", id, (int)resp.StatusCode);
            return resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NoContent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting inbound message {Id}", id);
            return false;
        }
    }

    public async Task<MessageDto?> GetMessageByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("GetMessageByIdAsync called with empty ID");
            return null;
        }

        try
        {
            // Intentar en este orden: inbox/messages/{id}, messages/{id}, messageheaders/{id}
            var endpoints = new[]
            {
                $"inbox/messages/{Uri.EscapeDataString(id)}",
                $"messages/{Uri.EscapeDataString(id)}",
                $"messageheaders/{Uri.EscapeDataString(id)}"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync(endpoint, ct);

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogError("Esendex authentication failed with status {StatusCode}", response.StatusCode);
                        throw new UnauthorizedAccessException("Esendex authentication failed");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.LogDebug("Message {Id} not found at endpoint {Endpoint}", id, endpoint);
                            continue; // Intentar siguiente endpoint
                        }
                        _logger.LogWarning("Failed to fetch message {Id} from {Endpoint}: {StatusCode}", id, endpoint, response.StatusCode);
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync(ct);
                    var parsedInfo = ParseFullMessageXml(content, id);
                    
                    if (parsedInfo != null)
                    {
                        // Validar que el ID del mensaje devuelto coincida con el ID solicitado (case-insensitive)
                        if (!string.Equals(parsedInfo.ActualId, id, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning(
                                "Esendex returned message with different ID. Requested: {RequestedId}, Actual: {ActualId}, Endpoint: {Endpoint}. Trying next endpoint.",
                                id, parsedInfo.ActualId, endpoint);
                            continue; // Ignorar esta respuesta y probar siguiente endpoint
                        }

                        string finalMessage;
                        string messageSource;

                        // Si bodyText está vacío pero bodyUri existe, hacer GET a esa URI
                        if (string.IsNullOrWhiteSpace(parsedInfo.BodyText) && !string.IsNullOrWhiteSpace(parsedInfo.BodyUri))
                        {
                            try
                            {
                                var bodyContent = await FetchBodyFromUri(parsedInfo.BodyUri, ct);
                                if (!string.IsNullOrWhiteSpace(bodyContent))
                                {
                                    // FetchBodyFromUri ya devuelve texto limpio, pero asegurémonos
                                    finalMessage = CleanTextContent(bodyContent);
                                    messageSource = "bodyUri";
                                    _logger.LogInformation("Message {Id}: Fetched body from URI (length: {Length})", id, finalMessage.Length);
                                }
                                else
                                {
                                    // Fallback a summary si el fetch falla
                                    finalMessage = parsedInfo.Summary;
                                    messageSource = "summary (bodyUri fetch returned empty)";
                                    _logger.LogWarning("Message {Id}: bodyUri fetch returned empty, using summary fallback", id);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Fallback a summary si hay error al obtener el body
                                finalMessage = parsedInfo.Summary;
                                messageSource = "summary (bodyUri fetch failed)";
                                _logger.LogWarning(ex, "Message {Id}: Failed to fetch body from URI, using summary fallback", id);
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(parsedInfo.BodyText))
                        {
                            // Usar body inline (limpiar cualquier etiqueta XML que pueda tener)
                            finalMessage = CleanTextContent(parsedInfo.BodyText);
                            messageSource = "bodyInline";
                            _logger.LogInformation("Message {Id}: Using inline body (length: {Length})", id, finalMessage.Length);
                        }
                        else
                        {
                            // Fallback a summary
                            finalMessage = parsedInfo.Summary;
                            messageSource = "summary";
                            _logger.LogInformation("Message {Id}: No body found, using summary fallback (length: {Length})", id, finalMessage.Length);
                        }

                        var message = new MessageDto
                        {
                            Id = id,
                            From = parsedInfo.From,
                            To = parsedInfo.To,
                            Message = finalMessage,
                            ReceivedUtc = parsedInfo.ReceivedUtc
                        };

                        _logger.LogDebug("Successfully retrieved message {Id} from endpoint {Endpoint}, body source: {Source}, body length: {Length}", 
                            id, endpoint, messageSource, finalMessage.Length);
                        _logger.LogInformation("Message {Id} retrieved successfully from {Endpoint} using {Source}", 
                            id, endpoint, messageSource);
                        return message;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching message {Id} from {Endpoint}", id, endpoint);
                }
            }

            _logger.LogWarning("Message {Id} not found in any endpoint", id);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMessageByIdAsync for ID {Id}", id);
            return null;
        }
    }

    private class ParsedMessageInfo
    {
        public string ActualId { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string BodyText { get; set; } = "";
        public string? BodyUri { get; set; }
        public string Summary { get; set; } = "";
        public DateTime ReceivedUtc { get; set; }
    }

    private ParsedMessageInfo? ParseFullMessageXml(string xmlContent, string expectedId)
    {
        try
        {
            var xdoc = XDocument.Parse(xmlContent);
            XNamespace ns = "http://api.esendex.com/ns/";

            // El XML de detalle suele traer <message> o <messageheader>
            var msg = xdoc.Descendants(ns + "message").FirstOrDefault()
                      ?? xdoc.Descendants(ns + "messageheader").FirstOrDefault();

            if (msg == null)
            {
                _logger.LogWarning("No message element found in XML for ID {Id}", expectedId);
                return null;
            }

            // Extraer el ID real del mensaje: primero del atributo "id", luego del elemento <id>
            var actualId = msg.Attribute("id")?.Value 
                          ?? msg.Element(ns + "id")?.Value 
                          ?? "";

            var from = msg.Element(ns + "from")?.Element(ns + "phonenumber")?.Value ?? "";
            var to = msg.Element(ns + "to")?.Element(ns + "phonenumber")?.Value ?? "";

            // Extraer body: usar Descendants para ser más robusto (puede no ser hijo directo)
            var bodyElement = msg.Descendants(ns + "body").FirstOrDefault();
            var bodyText = bodyElement?.Value ?? "";
            var bodyUri = bodyElement?.Attribute("uri")?.Value;

            // Extraer summary: usar Descendants para ser más robusto
            var summary = msg.Descendants(ns + "summary").FirstOrDefault()?.Value ?? "";

            var receivedAtStr =
                msg.Element(ns + "receivedat")?.Value ??
                msg.Element(ns + "submittedat")?.Value ??
                msg.Element(ns + "sentat")?.Value;

            return new ParsedMessageInfo
            {
                ActualId = actualId,
                From = from,
                To = to,
                BodyText = bodyText,
                BodyUri = bodyUri,
                Summary = summary,
                ReceivedUtc = ParseDateTime(receivedAtStr)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing full message XML for ID {Id}. Content length: {Length}", expectedId, xmlContent.Length);
            return null;
        }
    }
}
