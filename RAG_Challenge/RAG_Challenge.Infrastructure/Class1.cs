using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain;

namespace RAG_Challenge.Infrastructure;

public sealed class OpenAiOptions
{
    public const string SectionName = "ExternalApis:OpenAI";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed class VectorDbOptions
{
    public const string SectionName = "ExternalApis:VectorDb";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2023-11-01";
}

internal sealed class OpenAiHttpClient(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAiHttpClient> logger) : IOpenAiClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly OpenAiOptions _options = options.Value;
    private readonly ILogger<OpenAiHttpClient> _logger = logger;
    private const string EmbeddingsPath = "embeddings";
    private const string ChatCompletionsPath = "chat/completions";
    private const string DefaultModel = "text-embedding-3-large";

    public async Task<OpenAiItemResponse?> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        // placeholder retained; not used in RAG flow
        using var request = new HttpRequestMessage(HttpMethod.Get, $"items/{id}");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-API-KEY", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI item call failed with status {StatusCode} for id {Id}", response.StatusCode, id);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OpenAiItemResponse>(cancellationToken: cancellationToken);
    }

    public async Task<EmbeddingResponse?> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        var payload = new EmbeddingRequest(DefaultModel, input);

        using var request = new HttpRequestMessage(HttpMethod.Post, EmbeddingsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI embeddings call failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ChatCompletionResponse?> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var payload = new ChatCompletionRequest("gpt-4o", messages);

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI chat completion failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
    }
}

internal sealed class VectorDbHttpClient(HttpClient httpClient, IOptions<VectorDbOptions> options, ILogger<VectorDbHttpClient> logger) : IVectorDbClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly VectorDbOptions _options = options.Value;
    private readonly ILogger<VectorDbHttpClient> _logger = logger;

    public async Task<IReadOnlyList<VectorDbSearchResult>> SearchAsync(VectorSearchRequest requestPayload, CancellationToken cancellationToken = default)
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

        var search = await response.Content.ReadFromJsonAsync<VectorSearchResponse>(cancellationToken: cancellationToken);
        return search?.Value ?? [];
    }
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<VectorDbOptions>(configuration.GetSection(VectorDbOptions.SectionName));

        services.AddHttpClient<IOpenAiClient, OpenAiHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddHttpClient<IVectorDbClient, VectorDbHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VectorDbOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<IRagOrchestrator, RagOrchestrator>();

        return services;
    }
}

internal sealed class RagOrchestrator(IOpenAiClient openAi, IVectorDbClient vectorDb, ILogger<RagOrchestrator> logger) : IRagOrchestrator
{
    private readonly IOpenAiClient _openAi = openAi;
    private readonly IVectorDbClient _vectorDb = vectorDb;
    private readonly ILogger<RagOrchestrator> _logger = logger;

    public async Task<ChatOrchestrationResult> GenerateAnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        // Step 1: embed question
        var embedding = await _openAi.CreateEmbeddingAsync(question, cancellationToken);
        if (embedding?.Data.FirstOrDefault()?.Embedding is not { } vector)
        {
            _logger.LogWarning("Embedding generation failed");
            return new ChatOrchestrationResult("Embedding failed", embedding, Array.Empty<VectorDbSearchResult>(), null);
        }

        // Step 2: search vector DB (Azure AI Search style)
        var vectorQuery = new VectorQuery(vector.ToArray(), 3, "embeddings");
        var searchRequest = new VectorSearchRequest(
            Count: true,
            Select: "content,type",
            Top: 10,
            Filter: "projectName eq 'tesla_motors'",
            VectorQueries: [vectorQuery]
        );

        var retrieved = await _vectorDb.SearchAsync(searchRequest, cancellationToken);

        // Step 3: build chat with context (placeholder system+user/messages; can enrich later)
        var system = new ChatMessage("system", "You are a helpful assistant. Use the provided context to answer succinctly.");
        var contextChunk = string.Join("\n\n", retrieved.Select(r => $"[{r.Type}] {r.Content}"));
        var user = new ChatMessage("user", $"Question: {question}\n\nContext:\n{contextChunk}");
        var chat = await _openAi.CreateChatCompletionAsync([system, user], cancellationToken);

        var answer = chat?.Choices.FirstOrDefault()?.Message.Content ?? "No answer";
        return new ChatOrchestrationResult(answer, embedding, retrieved, chat);
    }
}
