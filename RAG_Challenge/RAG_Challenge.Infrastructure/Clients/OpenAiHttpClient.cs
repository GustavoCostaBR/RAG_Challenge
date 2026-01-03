using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Infrastructure.Configuration;

namespace RAG_Challenge.Infrastructure.Clients;

internal sealed class OpenAiHttpClient(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiHttpClient> logger) : IOpenAiClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly OpenAiOptions _options = options.Value;
    private readonly ILogger<OpenAiHttpClient> _logger = logger;
    private const string EmbeddingsPath = "embeddings";
    private const string ChatCompletionsPath = "chat/completions";
    private const string EmbeddingModel = "text-embedding-3-large";
    private const string ChatModel = "gpt-4o";

    public async Task<EmbeddingResponse?> CreateEmbeddingAsync(string input,
        CancellationToken cancellationToken = default)
    {
        var payload = new EmbeddingRequest(EmbeddingModel, input);

        using var request = new HttpRequestMessage(HttpMethod.Post, EmbeddingsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI embeddings call failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ChatCompletionResponse?> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var payload = new ChatCompletionRequest(ChatModel, messages);

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
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