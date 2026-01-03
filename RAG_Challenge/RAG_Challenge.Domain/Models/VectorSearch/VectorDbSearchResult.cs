namespace RAG_Challenge.Domain.Models.VectorSearch;

public record VectorDbSearchResult(string Content, string Type, float? Score);