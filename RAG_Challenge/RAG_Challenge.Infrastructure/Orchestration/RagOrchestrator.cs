using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Infrastructure.Orchestration;

internal sealed class RagOrchestrator(IOpenAiClient openAi, IVectorDbClient vectorDb, ILogger<RagOrchestrator> logger)
    : IRagOrchestrator
{
    private readonly IOpenAiClient _openAi = openAi;
    private readonly IVectorDbClient _vectorDb = vectorDb;
    private readonly ILogger<RagOrchestrator> _logger = logger;
    private const int MaxClarifications = 2;
    private const string ClarificationTag = "[clarification]";

    private const string CoverageJudgeSystemPrompt =
        "You are a coverage judge. Decide if the internal information is sufficient to answer the question. " +
        "Respond in one line. If sufficient, reply: YES. If insufficient, reply: NO: <Say to the user you did not find the info in your internal search and ask for clarification, pointing what is the info you could not find related to his question, ask if he can rephrase>. ";

    public async Task<ChatOrchestrationResult> GenerateAnswerAsync(RagRequest request,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _openAi.CreateEmbeddingAsync(request.Question, cancellationToken);
        var firstEmbedding = embedding is { Data.Count: > 0 } ? embedding.Data[0].Embedding : null;
        if (firstEmbedding is null)
        {
            _logger.LogWarning("Embedding generation failed");
            return new ChatOrchestrationResult("Embedding failed", embedding, [], null, [], false);
        }

        var vectorQuery = new VectorQuery(firstEmbedding.ToArray(), 3, "embeddings");
        var searchRequest = new VectorSearchRequest(
            Count: true,
            Select: "content,type",
            Top: 10,
            Filter: "projectName eq 'tesla_motors'",
            VectorQueries: [vectorQuery]
        );

        var retrieved = await _vectorDb.SearchAsync(searchRequest, cancellationToken);

        var history = request.History;
        var contextChunk = string.Join("\n\n", retrieved.Select(r => $"[{r.Type}] {r.Content}"));
        var allN2 = retrieved.Count > 0 &&
                    retrieved.All(r => string.Equals(r.Type, "N2", StringComparison.OrdinalIgnoreCase));
        var (judgeClarify, judgePrompt) =
            await EvaluateCoverageAsync(request.Question, contextChunk, cancellationToken);

        var system = new ChatMessage("system",
            "You are a helpful assistant. Use only the provided context (no external knowledge). Respond in JSON as {\"answer\":\"...\",\"handoverToHumanNeeded\":false}. If any content you rely on is labeled N2 (only if you used it for the answer), set handoverToHumanNeeded to true. Keep answer concise.");

        var messages = new List<ChatMessage> { system };
        messages.AddRange(history);
        var userWithContext = new ChatMessage("user", $"Question: {request.Question}\n\nContext:\n{contextChunk}");
        messages.Add(userWithContext);

        // Clarification bookkeeping
        var clarificationsSoFar = history.Count(m =>
            m.Role == "assistant" && m.Content.Contains(ClarificationTag, StringComparison.OrdinalIgnoreCase));
        var heuristicClarify = ShouldClarify(retrieved);
        var needClarification = heuristicClarify || judgeClarify;
        var handover = false;

        switch (needClarification)
        {
            case true when clarificationsSoFar >= MaxClarifications:
            {
                // Exceeded clarification budget
                handover = true;
                return ReturnEscalationAnswer(request, history, embedding, retrieved, handover);
            }

            case true:
            {
                var reasonText = string.IsNullOrWhiteSpace(judgePrompt)
                    ? "I couldn't find enough information in my internal search. Could you clarify what you'd like to know?"
                    : $"{judgePrompt}";
                var clarificationPrompt = ClarificationTag + " " + reasonText;

                var returnedHistoryClarify = new List<ChatMessage>(history)
                {
                    new("user", request.Question),
                    new("assistant", clarificationPrompt)
                };
                return new ChatOrchestrationResult(clarificationPrompt, embedding, retrieved, null,
                    returnedHistoryClarify,
                    handover);
            }
        }

        // Proceed to answer
        var chat = await _openAi.CreateChatCompletionAsync(messages, cancellationToken);
        var firstChoice = chat is { Choices.Count: > 0 } ? chat.Choices[0] : null;
        var rawContent = firstChoice?.Message.Content;
        var (parsedAnswer, parsedHandover) = TryParseModelResponse(rawContent);
        var answer = parsedAnswer ?? rawContent ?? "No answer";

        if (parsedHandover == true)
        {
            return ReturnEscalationAnswer(request, history, embedding, retrieved, true);
        }

        var returnedHistory = new List<ChatMessage>(history)
        {
            new("user", request.Question),
            new("assistant", answer)
        };

        var finalHandover = handover || allN2 || parsedHandover == true;
        return new ChatOrchestrationResult(answer, embedding, retrieved, chat, returnedHistory, finalHandover);
    }

    private static ChatOrchestrationResult ReturnEscalationAnswer(
        RagRequest request,
        IReadOnlyList<ChatMessage> history,
        EmbeddingResponse? embedding,
        IReadOnlyList<VectorDbSearchResult> retrieved,
        bool handover)
    {
        const string escalationAnswer = "I need to hand this over to a human specialist for further assistance.";
        var returnedHistoryEscalate = new List<ChatMessage>(history)
        {
            new("user", request.Question),
            new("assistant", escalationAnswer)
        };
        return new ChatOrchestrationResult(escalationAnswer, embedding, retrieved, null, returnedHistoryEscalate,
            handover);
    }

    private static bool ShouldClarify(IReadOnlyList<VectorDbSearchResult> retrieved)
    {
        if (retrieved.Count == 0)
        {
            return true;
        }

        var topScore = retrieved[0].Score ?? 0;
        var avgTop3 = retrieved.Take(Math.Min(3, retrieved.Count)).Average(r => r.Score ?? 0);

        // Hybrid heuristic: low top score OR low average => clarify
        return topScore < 0.35 || avgTop3 < 0.30;
    }

    private async Task<(bool needClarification, string? clarificationPrompt)> EvaluateCoverageAsync(
        string question,
        string context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return (true, null);
        }

        var judgeMessages = new List<ChatMessage>
        {
            new("system", CoverageJudgeSystemPrompt),
            new("user", $"Question: {question}\n\nContext:\n{context}")
        };

        var judgeResult = await _openAi.CreateChatCompletionAsync(judgeMessages, cancellationToken);

        var firstResult = judgeResult is { Choices.Count: > 0 } ? judgeResult.Choices[0] : null;

        var judgeText = firstResult?.Message.Content;
        if (string.IsNullOrWhiteSpace(judgeText))
        {
            return (false, null);
        }

        judgeText = judgeText.Trim();
        if (judgeText.StartsWith("YES", true, CultureInfo.InvariantCulture))
        {
            return (false, null);
        }

        if (judgeText.StartsWith("NO", true, CultureInfo.InvariantCulture))
        {
            var clarification = judgeText.Length > 2 ? judgeText[2..].TrimStart(':', ' ', '\t') : null;
            return (true, string.IsNullOrWhiteSpace(clarification) ? null : clarification);
        }

        // Default: do not force clarification if judge is unclear
        return (false, null);
    }

    private static (string? answer, bool? handoverToHumanNeeded) TryParseModelResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var ans = root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String
                ? a.GetString()
                : null;
            bool? handover = null;
            if (root.TryGetProperty("handoverToHumanNeeded", out var h))
            {
                handover = h.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => handover
                };
            }

            return (ans, handover);
        }
        catch
        {
            return (null, null);
        }
    }
}