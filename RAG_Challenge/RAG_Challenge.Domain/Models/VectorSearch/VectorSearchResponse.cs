namespace RAG_Challenge.Domain.Models.VectorSearch;

public record VectorSearchResponse(IReadOnlyList<VectorDbSearchResult> Value, long? Count);