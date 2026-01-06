using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;
using RAG_Challenge.Infrastructure.Configuration;

namespace RAG_Challenge.Infrastructure.Clients;

internal sealed class VectorDbHttpClient(
    HttpClient httpClient,
    IOptions<VectorDbOptions> options,
    ILogger<VectorDbHttpClient> logger) : IVectorDbClient
{
    private readonly VectorDbOptions _options = options.Value;

    public async Task<Result<IReadOnlyList<VectorDbSearchResult>>> SearchAsync(VectorSearchRequest requestPayload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var path = $"indexes/{_options.IndexName}/docs/search?api-version={_options.ApiVersion}";
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = JsonContent.Create(requestPayload);

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("api-key", _options.ApiKey);
            }

            request.Headers.Accept.ParseAdd("application/json");

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vector DB search failed with status {StatusCode}", response.StatusCode);
                return Result<IReadOnlyList<VectorDbSearchResult>>.Failure(
                    $"Vector DB search failed with status {response.StatusCode}");
            }

            var search =
                await response.Content.ReadFromJsonAsync<VectorSearchResponse>(cancellationToken: cancellationToken);
            return Result<IReadOnlyList<VectorDbSearchResult>>.Success(search?.Value ?? []);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during Vector DB search");
            return Result<IReadOnlyList<VectorDbSearchResult>>.Failure(
                $"An error occurred during Vector DB search: {ex.Message}");
        }
    }
}