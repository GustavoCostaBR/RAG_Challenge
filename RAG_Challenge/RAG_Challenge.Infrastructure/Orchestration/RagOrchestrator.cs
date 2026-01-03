using Microsoft.Extensions.Logging;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Infrastructure.Orchestration;

internal sealed class RagOrchestrator(IOpenAiClient openAi, IVectorDbClient vectorDb, ILogger<RagOrchestrator> logger)
    : IRagOrchestrator
{
    private readonly IOpenAiClient _openAi = openAi;
    private readonly IVectorDbClient _vectorDb = vectorDb;
    private readonly ILogger<RagOrchestrator> _logger = logger;

    public async Task<ChatOrchestrationResult> GenerateAnswerAsync(RagRequest request,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _openAi.CreateEmbeddingAsync(request.Question, cancellationToken);
        var firstEmbedding = embedding is { Data.Count: > 0 } ? embedding.Data[0].Embedding : null;
        if (firstEmbedding is null)
        {
            _logger.LogWarning("Embedding generation failed");
            return new ChatOrchestrationResult("Embedding failed", embedding, Array.Empty<VectorDbSearchResult>(),
                null);
        }

        var vectorQuery = new VectorQuery(firstEmbedding.ToArray(), 3, "embeddings");
        var searchRequest = new VectorSearchRequest(
            Count: true,
            Select: "content,type",
            Top: 10,
            Filter: "projectName eq 'tesla_motors'",
            VectorQueries: [vectorQuery]
        );

        var retrieved = await _vectorDb.SearchAsync(searchRequest, cancellationToken);

        var history = request.History;
        var contextChunk = string.Join("\n\n", retrieved.Select(r => $"[{r.Type}] {r.Content}"));

        var system = new ChatMessage("system",
            "You are a helpful assistant. Use the provided context to answer succinctly.");
        var user = new ChatMessage("user", $"Question: {request.Question}\n\nContext:\n{contextChunk}");

        var messages = new List<ChatMessage> { system };
        messages.AddRange(history);
        messages.Add(user);

        var chat = await _openAi.CreateChatCompletionAsync(messages, cancellationToken);
        var firstChoice = chat is { Choices.Count: > 0 } ? chat.Choices[0] : null;

        var answer = firstChoice?.Message.Content ?? "No answer";

        return new ChatOrchestrationResult(answer, embedding, retrieved, chat);
    }
}