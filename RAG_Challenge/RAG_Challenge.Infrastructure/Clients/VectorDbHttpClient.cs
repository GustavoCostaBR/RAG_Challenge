using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.VectorSearch;
using RAG_Challenge.Infrastructure.Configuration;

namespace RAG_Challenge.Infrastructure.Clients;

internal sealed class VectorDbHttpClient(
    HttpClient httpClient,
    IOptions<VectorDbOptions> options,
    ILogger<VectorDbHttpClient> logger) : IVectorDbClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly VectorDbOptions _options = options.Value;
    private readonly ILogger<VectorDbHttpClient> _logger = logger;

    public async Task<IReadOnlyList<VectorDbSearchResult>> SearchAsync(VectorSearchRequest requestPayload,
        CancellationToken cancellationToken = default)
    {
        var path = $"indexes/{_options.IndexName}/docs/search?api-version={_options.ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = JsonContent.Create(requestPayload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("api-key", _options.ApiKey);
        }

        request.Headers.Accept.ParseAdd("application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Vector DB search failed with status {StatusCode}", response.StatusCode);
            return [];
        }

        var search =
            await response.Content.ReadFromJsonAsync<VectorSearchResponse>(cancellationToken: cancellationToken);
        return search?.Value ?? [];
    }
}