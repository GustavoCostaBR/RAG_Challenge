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
    private const int MaxClarifications = 2;
    private const string ClarificationTag = "[clarification]";
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";
    private const string SystemRole = "system";

    private const string CoverageJudgeSystemPrompt =
        "You are a coverage judge. Decide if the internal information is sufficient to answer the question. " +
        "Respond in one line. If sufficient, reply: YES. " +
        "If insufficient, reply: NO: I can't find the answer in my internal search. Please clarify <state the missing detail>. Can you please rephrase?";

    private const string SystemPrompt =
        "You are a helpful assistant. Use only the provided context (no external knowledge). " +
        "Respond in JSON as {\"answer\":\"...\",\"handoverToHumanNeeded\":false}. " +
        "If any content you rely on is labeled N2 (only if you used it for the answer), set handoverToHumanNeeded to true. " +
        "Keep answer concise.";

    public async Task<ChatOrchestrationResult> GenerateAnswerAsync(RagRequest request,
        CancellationToken cancellationToken = default)
    {
        var projectId = request.ProjectId ?? Guid.Empty;
        var projectFilterResult = Projects.ToFilterValue(projectId);

        if (!projectFilterResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                "Invalid project ID.",
                null,
                [],
                null,
                [],
                false,
                projectFilterResult.Status);
        }

        var embeddingResult = await openAi.CreateEmbeddingAsync(request.Question, cancellationToken);
        if (!embeddingResult.IsSuccess)
        {
            logger.LogWarning("Embedding generation failed: {ErrorMessage}", embeddingResult.Status.ErrorMessage);
            return new ChatOrchestrationResult(
                "Embedding failed",
                null,
                [],
                null,
                [],
                false,
                embeddingResult.Status);
        }

        var embedding = embeddingResult.Value;
        var firstEmbedding = embedding is { Data.Count: > 0 } ? embedding.Data[0].Embedding : null;
        if (firstEmbedding is null)
        {
            logger.LogWarning("Embedding generation failed: No embedding data returned");
            return new ChatOrchestrationResult(
                "Embedding failed",
                embedding,
                [],
                null,
                [],
                false,
                Status.Error("No embedding data returned"));
        }

        var vectorQuery = new VectorQuery(firstEmbedding.ToArray(), 3, "embeddings");
        var searchRequestResult = BuildVectorSearchRequest(vectorQuery, projectFilterResult.Value!);

        if (!searchRequestResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "Invalid project ID.",
                Embedding: embedding,
                RetrievedChunks: [],
                Completion: null,
                History: request.History,
                HandoverToHumanNeeded: false,
                Status: searchRequestResult.Status);
        }

        var retrievedContext = await vectorDb.SearchAsync(searchRequestResult.Value!, cancellationToken);

        var chatHistory = request.History;

        var contextChunk = GetContextChunkString(retrievedContext);

        if (IsAllRetrievedContextLabelledN2(retrievedContext))
        {
            return ReturnEscalationAnswer(request, chatHistory, embedding, retrievedContext, true);
        }

        var coverageEvaluationResult =
            await EvaluateCoverageAsync(request.Question, contextChunk, cancellationToken);

        if (!coverageEvaluationResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "An internal error occurred.",
                Embedding: embedding,
                RetrievedChunks: retrievedContext,
                Completion: null,
                History: chatHistory,
                HandoverToHumanNeeded: true,
                Status: coverageEvaluationResult.Status);
        }

        var (judgeClarify, judgePrompt) = coverageEvaluationResult.Value;

        var system = new ChatMessage(SystemRole, SystemPrompt);

        var messages = new List<ChatMessage> { system };
        messages.AddRange(chatHistory);

        var userWithContext = new ChatMessage(UserRole, $"Question: {request.Question}\n\nContext:\n{contextChunk}");

        messages.Add(userWithContext);

        // Clarification bookkeeping
        var clarificationsSoFar = GetHistoryClarificationsCount(chatHistory);
        var heuristicClarify = ShouldClarify(retrievedContext);
        var needClarification = heuristicClarify || judgeClarify;
        var handoverToHuman = false;

        switch (needClarification)
        {
            case true when clarificationsSoFar >= MaxClarifications:
            {
                // Exceeded clarification budget
                handoverToHuman = true;
                return ReturnEscalationAnswer(request, chatHistory, embedding, retrievedContext, handoverToHuman);
            }

            case true:
            {
                var clarificationPrompt = ClarificationTag + " " + judgePrompt;

                var returnedHistoryClarify = new List<ChatMessage>(chatHistory)
                {
                    new(UserRole, request.Question),
                    new(AssistantRole, clarificationPrompt)
                };
                return new ChatOrchestrationResult(
                    clarificationPrompt,
                    embedding,
                    retrievedContext,
                    null,
                    returnedHistoryClarify,
                    handoverToHuman, Status.Ok());
            }
        }

        // Proceed to answer
        return await GetAnswer(
            request,
            messages,
            chatHistory,
            embedding,
            retrievedContext,
            cancellationToken);

        int GetHistoryClarificationsCount(IReadOnlyList<ChatMessage> history)
        {
            return history.Count(m =>
                m.Role == AssistantRole && m.Content.Contains(ClarificationTag, StringComparison.OrdinalIgnoreCase));
        }

        string GetContextChunkString(IReadOnlyList<VectorDbSearchResult> context)
        {
            return string.Join("\n\n", context.Select(r => $"[{r.Type}] {r.Content}"));
        }

        bool IsAllRetrievedContextLabelledN2(IReadOnlyList<VectorDbSearchResult> context)
        {
            return context.Count > 0 &&
                   context.All(r => string.Equals(r.Type, "N2", StringComparison.OrdinalIgnoreCase));
        }

        Result<VectorSearchRequest> BuildVectorSearchRequest(VectorQuery vectorQuery1, string projectFilter)
        {
            var vectorSearchRequest = new VectorSearchRequest(
                Count: true,
                Select: "content,type",
                Top: 10,
                Filter: $"projectName eq '{projectFilter}'",
                VectorQueries: [vectorQuery1]
            );
            return Result<VectorSearchRequest>.Success(vectorSearchRequest);
        }
    }

    private async Task<ChatOrchestrationResult> GetAnswer(
        RagRequest request,
        List<ChatMessage> messages,
        IReadOnlyList<ChatMessage> chatHistory,
        EmbeddingResponse? embedding,
        IReadOnlyList<VectorDbSearchResult> retrievedContext,
        CancellationToken cancellationToken)
    {
        var chatResult = await openAi.CreateChatCompletionAsync(messages, cancellationToken);
        if (!chatResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "Failed to get chat completion.",
                Embedding: embedding,
                RetrievedChunks: retrievedContext,
                Completion: null,
                History: chatHistory,
                HandoverToHumanNeeded: true,
                Status: chatResult.Status);
        }

        var chat = chatResult.Value;
        var firstChoice = chat is { Choices.Count: > 0 } ? chat.Choices[0] : null;
        var rawContent = firstChoice?.Message.Content;

        var parseResult = TryParseModelResponse(rawContent);

        if (!parseResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "Failed to parse model response.",
                Embedding: embedding,
                RetrievedChunks: retrievedContext,
                Completion: chat,
                History: chatHistory,
                HandoverToHumanNeeded: true,
                Status: parseResult.Status);
        }

        var (answer, parsedHandoverToHuman) = parseResult.Value;

        if (parsedHandoverToHuman)
        {
            return ReturnEscalationAnswer(request, chatHistory, embedding, retrievedContext, true);
        }

        var returnedHistory = new List<ChatMessage>(chatHistory)
        {
            new(UserRole, request.Question),
            new(AssistantRole, answer)
        };

        return new ChatOrchestrationResult(answer, embedding, retrievedContext, chat, returnedHistory,
            parsedHandoverToHuman, Status.Ok());
    }

    private static ChatOrchestrationResult ReturnEscalationAnswer(
        RagRequest request,
        IReadOnlyList<ChatMessage> history,
        EmbeddingResponse? embedding,
        IReadOnlyList<VectorDbSearchResult> retrieved,
        bool handoverToHuman)
    {
        const string escalationAnswer = "I need to hand this over to a human specialist for further assistance.";
        var returnedHistoryEscalate = new List<ChatMessage>(history)
        {
            new(UserRole, request.Question),
            new(AssistantRole, escalationAnswer)
        };
        return new ChatOrchestrationResult(escalationAnswer, embedding, retrieved, null, returnedHistoryEscalate,
            handoverToHuman, Status.Ok());
    }

    private static bool ShouldClarify(IReadOnlyList<VectorDbSearchResult> retrieved)
    {
        // Heuristic: if the top score is low or the average of the top 3 is low, we should clarify.
        // This helps in cases where we have some matches but they are not very relevant.
        if (retrieved.Count == 0)
        {
            return true;
        }

        var topScore = retrieved[0].Score ?? 0;
        var avgTop3 = retrieved.Take(Math.Min(3, retrieved.Count)).Average(r => r.Score ?? 0);

        // Hybrid heuristic: low top score OR low average => clarify
        return topScore < 0.35 || avgTop3 < 0.30;
    }

    private async Task<Result<(bool NeedClarification, string? ClarificationPrompt)>> EvaluateCoverageAsync(
        string question,
        string context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return Result<(bool, string?)>.Failure("No context retrieved from Vector DB");
        }

        var judgeMessages = new List<ChatMessage>
        {
            new(SystemRole, CoverageJudgeSystemPrompt),
            new(UserRole, $"Question: {question}\n\nContext:\n{context}")
        };

        var judgeResult = await openAi.CreateChatCompletionAsync(judgeMessages, cancellationToken);

        if (!judgeResult.IsSuccess)
        {
            return Result<(bool, string?)>.Failure(judgeResult.Status);
        }

        var chatResponse = judgeResult.Value;
        var firstResult = chatResponse is { Choices.Count: > 0 } ? chatResponse.Choices[0] : null;

        var judgeText = firstResult?.Message.Content;

        if (string.IsNullOrWhiteSpace(judgeText))
        {
            return Result<(bool, string?)>.Failure("No response from Coverage Judge");
        }

        judgeText = judgeText.Trim();

        if (judgeText.StartsWith("NO", true, CultureInfo.InvariantCulture))
        {
            var clarification = judgeText.Length > 2 ? judgeText[2..].TrimStart(':', ' ', '\t') : null;

            return string.IsNullOrWhiteSpace(clarification)
                ? Result<(bool, string?)>.Failure("Coverage Judge returned NO but provided no clarification")
                : Result<(bool, string?)>.Success((true, clarification));
        }

        if (judgeText.StartsWith("YES", true, CultureInfo.InvariantCulture))
        {
            return Result<(bool, string?)>.Success((false, null));
        }

        return Result<(bool, string?)>.Failure($"Unexpected response from Coverage Judge: {judgeText}");
    }

    private static Result<(string Answer, bool HandoverToHumanNeeded)> TryParseModelResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<(string, bool)>.Failure("Model response is empty");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<(string, bool)>.Failure("Model response is not a JSON object");
            }

            if (!root.TryGetProperty("answer", out var a) || a.ValueKind != JsonValueKind.String)
            {
                return Result<(string, bool)>.Failure("JSON response missing 'answer' property or it is not a string");
            }

            var ans = a.GetString() ?? string.Empty;

            var handoverToHuman = false;
            if (root.TryGetProperty("handoverToHumanNeeded", out var h))
            {
                if (h.ValueKind == JsonValueKind.True) handoverToHuman = true;
            }

            return Result<(string, bool)>.Success((ans, handoverToHuman));
        }
        catch (JsonException)
        {
            return Result<(string, bool)>.Failure("Failed to parse model response as JSON");
        }
        catch (Exception ex)
        {
            return Result<(string, bool)>.Failure($"Unexpected error parsing model response: {ex.Message}");
        }
    }
}