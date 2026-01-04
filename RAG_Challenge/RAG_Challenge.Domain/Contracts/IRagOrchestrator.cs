using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Domain.Contracts;

public interface IRagOrchestrator
{
    Task<ChatOrchestrationResult>
        GenerateAnswerAsync(RagRequest request, CancellationToken cancellationToken = default);
}