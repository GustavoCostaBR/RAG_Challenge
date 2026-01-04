using RAG_Challenge.Domain.Constants;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Infrastructure.Helpers;

public static class RagHeuristicsHelper
{
    private const int MaxClarifications = 2;
    private const string ClarificationTag = "[clarification]";

    public static bool ShouldClarify(IReadOnlyList<VectorDbSearchResult> retrieved)
    {
        // Heuristic: if the top score is low or the average of the top 3 is low, we should clarify.
        // This helps in cases where we have some matches, but they are not very relevant.
        if (retrieved.Count == 0)
        {
            return true;
        }

        var topScore = retrieved[0].Score ?? 0;
        var avgTop3 = retrieved.Take(Math.Min(3, retrieved.Count)).Average(r => r.Score ?? 0);

        // Hybrid heuristic: low top score OR low average => clarify
        return topScore < 0.35 || avgTop3 < 0.30;
    }

    public static bool IsAllRetrievedContextLabelledN2(IReadOnlyList<VectorDbSearchResult> context)
    {
        return context.Count > 0 &&
               context.All(r => string.Equals(r.Type, "N2", StringComparison.OrdinalIgnoreCase));
    }

    public static int GetHistoryClarificationsCount(IReadOnlyList<ChatMessage> history)
    {
        return history.Count(m =>
            m.Role == RoleConstants.AssistantRole && m.Content.Contains(ClarificationTag, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasExceededClarificationLimit(int clarificationsSoFar)
    {
        return clarificationsSoFar >= MaxClarifications;
    }
}