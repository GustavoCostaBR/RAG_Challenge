namespace RAG_Challenge.Domain.Models.Embeddings;

public record EmbeddingResponse(string Object, IReadOnlyList<EmbeddingData> Data, string Model, EmbeddingUsage Usage);