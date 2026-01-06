using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Domain.Contracts;

public interface IVectorDbClient
{
    Task<Result<IReadOnlyList<VectorDbSearchResult>>> SearchAsync(VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}