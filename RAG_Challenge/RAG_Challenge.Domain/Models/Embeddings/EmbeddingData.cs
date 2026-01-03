namespace RAG_Challenge.Domain.Models.Embeddings;

public record EmbeddingData(string Object, int Index, IReadOnlyList<float> Embedding);