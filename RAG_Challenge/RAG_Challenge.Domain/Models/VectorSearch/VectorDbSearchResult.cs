using System.Text.Json.Serialization;

namespace RAG_Challenge.Domain.Models.VectorSearch;

public record VectorDbSearchResult(
    string Content,
    string Type,
    [property: JsonPropertyName("@search.score")] double? Score);
