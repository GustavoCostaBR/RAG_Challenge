using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Infrastructure.Helpers;

public static class VectorSearchBuilder
{
    public static Result<VectorSearchRequest> Build(VectorQuery vectorQuery, Guid? projectId)
    {
        var id = projectId ?? Projects.TeslaMotorsId;
        var projectFilterResult = Projects.ToFilterValue(id);

        if (!projectFilterResult.IsSuccess)
        {
            return Result<VectorSearchRequest>.Failure(projectFilterResult.Status);
        }

        var vectorSearchRequest = new VectorSearchRequest(
            Count: true,
            Select: "content,type",
            Top: 10,
            Filter: $"projectName eq '{projectFilterResult.Value}'",
            VectorQueries: [vectorQuery]
        );
        return Result<VectorSearchRequest>.Success(vectorSearchRequest);
    }
}