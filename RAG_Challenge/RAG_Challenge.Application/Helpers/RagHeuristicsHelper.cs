using RAG_Challenge.Domain.Constants;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Application.Helpers;

public static class RagHeuristicsHelper
{
    public static bool ShouldClarify(IReadOnlyList<VectorDbSearchResult> retrieved)
    {
        // Heurística: se a pontuação máxima for baixa ou a média das 3 melhores for baixa, devemos esclarecer.
        // Isso ajuda nos casos em que temos algumas correspondências, mas elas não são muito relevantes.
        // Como comentado no readme, esses limiares podem ser ajustados conforme necessário.
        if (retrieved.Count == 0)
        {
            return true;
        }

        var topScore = retrieved[0].Score ?? 0;
        var avgTop3 = retrieved.Take(Math.Min(3, retrieved.Count)).Average(r => r.Score ?? 0);

        // Heurística híbrida: pontuação máxima baixa OU média baixa => esclarecer
        return topScore < RagOptions.TopScoreThreshold || avgTop3 < RagOptions.AvgTop3ScoreThreshold;
    }

    public static bool IsAllRetrievedContextLabelledN2(IReadOnlyList<VectorDbSearchResult> context)
    {
        return context.Count > 0 &&
               context.All(r => string.Equals(r.Type, FlowConstants.N2Label, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAnyRetrievedContextLabelledN2(IReadOnlyList<VectorDbSearchResult> context)
    {
        return context.Count > 0 &&
               context.Any(r => string.Equals(r.Type, FlowConstants.N2Label, StringComparison.OrdinalIgnoreCase));
    }

    public static int GetHistoryClarificationsCount(IReadOnlyList<ChatMessage> history)
    {
        return history.Count(m =>
            m.Role == RoleConstants.AssistantRole &&
            m.Content.Contains(FlowConstants.ClarificationTag, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasExceededClarificationLimit(int clarificationsSoFar)
    {
        return clarificationsSoFar >= RagOptions.MaxClarifications;
    }
}