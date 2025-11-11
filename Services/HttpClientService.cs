using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace AntManager.Services;

public class HttpClientService
{
    private readonly HttpClient _httpClient;
    
    public HttpClientService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    
    public async Task<HttpResponseMessage> SendRequestAsync(
        string method,
        string url,
        string? requestBody = null,
        Dictionary<string, string>? headers = null,
        string? authType = null,
        string? authValue = null)
    {
        try
        {
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod(method),
                RequestUri = new Uri(url)
            };
            
            // Add headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            // Add authentication
            if (!string.IsNullOrEmpty(authType) && !string.IsNullOrEmpty(authValue))
            {
                switch (authType.ToLower())
                {
                    case "bearer":
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authValue);
                        break;
                    case "basic":
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                        break;
                    case "apikey":
                        request.Headers.TryAddWithoutValidation("X-API-Key", authValue);
                        break;
                }
            }
            
            // Add request body for POST/PUT/PATCH
            if (!string.IsNullOrEmpty(requestBody) && 
                (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                 method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }
            
            return await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            throw new Exception($"HTTP Request failed: {ex.Message}", ex);
        }
    }
}
