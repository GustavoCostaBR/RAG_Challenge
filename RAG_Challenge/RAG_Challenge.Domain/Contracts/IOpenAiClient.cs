using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Domain.Contracts;

public interface IOpenAiClient
{
    Task<Result<EmbeddingResponse>> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default);

    Task<Result<ChatCompletionResponse>> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}