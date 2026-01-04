namespace RAG_Challenge.Domain.Models.VectorSearch;

public record VectorQuery(float[] Vector, int K, string Fields, string Kind = "vector");