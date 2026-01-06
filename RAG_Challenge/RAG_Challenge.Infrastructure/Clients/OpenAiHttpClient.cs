using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Infrastructure.Configuration;

namespace RAG_Challenge.Infrastructure.Clients;

internal sealed class OpenAiHttpClient(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiHttpClient> logger) : IOpenAiClient
{
    private readonly OpenAiOptions _options = options.Value;


    public async Task<Result<EmbeddingResponse>> CreateEmbeddingAsync(string input,
        CancellationToken cancellationToken = default)
    {
        var payload = new EmbeddingRequest(OpenAiOptions.EmbeddingModel, input);

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiOptions.EmbeddingsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI embeddings call failed with status {StatusCode}", response.StatusCode);
            return Result<EmbeddingResponse>.Failure(
                $"OpenAI embeddings call failed with status {response.StatusCode}");
        }

        var embeddingResponse =
            await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);

        return embeddingResponse is not null
            ? Result<EmbeddingResponse>.Success(embeddingResponse)
            : Result<EmbeddingResponse>.Failure("Failed to deserialize embedding response");
    }

    public async Task<Result<ChatCompletionResponse>> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var payload = new ChatCompletionRequest(OpenAiOptions.ChatModel, messages, 0.2);

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiOptions.ChatCompletionsPath);
        request.Content = JsonContent.Create(payload);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI chat completion failed with status {StatusCode}", response.StatusCode);
            return Result<ChatCompletionResponse>.Failure(
                $"OpenAI chat completion failed with status {response.StatusCode}");
        }

        var chatResponse =
            await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        return chatResponse is not null
            ? Result<ChatCompletionResponse>.Success(chatResponse)
            : Result<ChatCompletionResponse>.Failure("Failed to deserialize chat completion response");
    }
}