using System.Globalization;
using Microsoft.Extensions.Logging;
using RAG_Challenge.Domain.Constants;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;
using RAG_Challenge.Infrastructure.Helpers;

namespace RAG_Challenge.Infrastructure.Orchestration;

internal sealed class RagOrchestrator(
    IOpenAiClient openAi,
    IVectorDbClient vectorDb,
    ILogger<RagOrchestrator> logger)
    : IRagOrchestrator
{
    private const string ClarificationTag = "[clarification]";

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
        var searchRequestResult = VectorSearchBuilder.Build(vectorQuery, request.ProjectId);

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

        if (RagHeuristicsHelper.IsAllRetrievedContextLabelledN2(retrievedContext))
        {
            return ReturnEscalationAnswer(
                request,
                chatHistory,
                embedding,
                retrievedContext,
                true);
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

        var system = new ChatMessage(RoleConstants.SystemRole, RagPrompts.SystemPrompt);

        var messages = new List<ChatMessage> { system };
        messages.AddRange(chatHistory);

        var userWithContext = new ChatMessage(RoleConstants.UserRole,
            $"Question: {request.Question}\n\nContext:\n{contextChunk}");

        messages.Add(userWithContext);

        // Clarification bookkeeping
        var clarificationsSoFar = RagHeuristicsHelper.GetHistoryClarificationsCount(chatHistory);
        var heuristicClarify = RagHeuristicsHelper.ShouldClarify(retrievedContext);
        var needClarification = heuristicClarify || judgeClarify;
        var handoverToHuman = false;

        if (!needClarification)
        {
            return await GetAnswer(
                request,
                messages,
                chatHistory,
                embedding,
                retrievedContext,
                cancellationToken);
        }

        if (RagHeuristicsHelper.HasExceededClarificationLimit(clarificationsSoFar))
        {
            // Exceeded clarification budget
            handoverToHuman = true;
            return ReturnEscalationAnswer(request, chatHistory, embedding, retrievedContext, handoverToHuman);
        }

        var clarificationPrompt = ClarificationTag + " " + judgePrompt;

        var returnedHistoryClarify = new List<ChatMessage>(chatHistory)
        {
            new(RoleConstants.UserRole, request.Question),
            new(RoleConstants.AssistantRole, clarificationPrompt)
        };
        return new ChatOrchestrationResult(
            clarificationPrompt,
            embedding,
            retrievedContext,
            null,
            returnedHistoryClarify,
            handoverToHuman, 
            Status.Ok());
    }

    private static string GetContextChunkString(IReadOnlyList<VectorDbSearchResult> context)
    {
        return string.Join("\n\n", context.Select(r => $"[{r.Type}] {r.Content}"));
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

        var parseResult = ModelResponseParser.ParseModelResponse(rawContent);

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
            new(RoleConstants.UserRole, request.Question),
            new(RoleConstants.AssistantRole, answer)
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
            new(RoleConstants.UserRole, request.Question),
            new(RoleConstants.AssistantRole, escalationAnswer)
        };
        return new ChatOrchestrationResult(escalationAnswer, embedding, retrieved, null, returnedHistoryEscalate,
            handoverToHuman, Status.Ok());
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
            new(RoleConstants.SystemRole, RagPrompts.CoverageJudgeSystemPrompt),
            new(RoleConstants.UserRole, $"Question: {question}\n\nContext:\n{context}")
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
}