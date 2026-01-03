using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;

namespace RAG_Challenge.Domain.Contracts;

public interface IOpenAiClient
{
    Task<EmbeddingResponse?> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default);

    Task<ChatCompletionResponse?> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}