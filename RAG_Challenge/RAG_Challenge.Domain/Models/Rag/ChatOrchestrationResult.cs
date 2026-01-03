using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Domain.Models.Rag;

public record ChatOrchestrationResult(
    string Answer,
    EmbeddingResponse? Embedding,
    IReadOnlyList<VectorDbSearchResult> RetrievedChunks,
    ChatCompletionResponse? Completion);