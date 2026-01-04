using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Domain.Contracts;

public interface IVectorDbClient
{
    Task<IReadOnlyList<VectorDbSearchResult>> SearchAsync(VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}