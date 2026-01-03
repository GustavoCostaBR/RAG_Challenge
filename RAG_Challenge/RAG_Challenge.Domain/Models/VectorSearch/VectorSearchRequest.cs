namespace RAG_Challenge.Domain.Models.VectorSearch;

public record VectorSearchRequest(
    bool Count,
    string Select,
    int Top,
    string? Filter,
    IReadOnlyList<VectorQuery> VectorQueries);