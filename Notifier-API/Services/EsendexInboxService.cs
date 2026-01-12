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

    private string SanitizeUrl(string url)
    {
        // Remove credentials from URL for logging
        var uri = new Uri(url);
        return $"{uri.Scheme}://{uri.Host}{uri.PathAndQuery}";
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
            // Intentar primero con inbox/messages/{id} (para inbound), luego messages/{id}
            var endpoints = new[]
            {
                $"inbox/messages/{Uri.EscapeDataString(id)}",
                $"messages/{Uri.EscapeDataString(id)}"
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
                    var message = ParseFullMessageXml(content, id);
                    
                    if (message != null)
                    {
                        // Usar el ID original que se pasó como parámetro para mantener consistencia
                        message.Id = id;
                        _logger.LogInformation("Retrieved full message for ID {Id}, body length: {Length}", id, message.Message.Length);
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

    private MessageDto? ParseFullMessageXml(string xmlContent, string expectedId)
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

            var from = msg.Element(ns + "from")?.Element(ns + "phonenumber")?.Value ?? "";
            var to = msg.Element(ns + "to")?.Element(ns + "phonenumber")?.Value ?? "";

            // Obtener body completo (en el detalle SÍ viene el body completo)
            var body = msg.Element(ns + "body")?.Value ?? "";
            var summary = msg.Element(ns + "summary")?.Value ?? "";

            var receivedAtStr =
                msg.Element(ns + "receivedat")?.Value ??
                msg.Element(ns + "submittedat")?.Value ??
                msg.Element(ns + "sentat")?.Value;

            return new MessageDto
            {
                Id = expectedId, // Usar el ID que se pasó como parámetro para mantener consistencia
                From = from,
                To = to,
                Message = string.IsNullOrWhiteSpace(body) ? summary : body,
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
