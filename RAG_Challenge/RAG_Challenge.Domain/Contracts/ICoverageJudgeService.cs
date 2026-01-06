using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Domain.Contracts;

public interface ICoverageJudgeService
{
    Task<Result<(bool NeedClarification, string? ClarificationPrompt)>> EvaluateCoverageAsync(
        string question,
        string context,
        CancellationToken cancellationToken = default);
}