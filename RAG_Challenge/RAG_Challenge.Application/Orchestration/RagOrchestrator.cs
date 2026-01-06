using Microsoft.Extensions.Logging;
using RAG_Challenge.Application.Helpers;
using RAG_Challenge.Domain.Constants;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;

namespace RAG_Challenge.Application.Orchestration;

internal sealed class RagOrchestrator(
    IOpenAiClient openAi,
    IVectorDbClient vectorDb,
    ICoverageJudgeService judgeService,
    ILogger<RagOrchestrator> logger)
    : IRagOrchestrator
{
    // Nao se refere ao numero maximo de tentativas, mas sim ao numero maximo de tentativas para cada um dos problemas
    // No pior caso, no cenahrio que temos que escalar porque o content type é N2, o modelo fica um pouco mais lento (estamos falando de O(N²))
    // Por causa das retentativas, mas é aceitavel considerando que é um cenário que deve ser raro e buscamos estabilidade
    // em todos os cenarios
    private const int MaximumRetryCount = 2;

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

        var embeddingResult = await GetEmbeddingAsync(request.Question, cancellationToken);
        if (!embeddingResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                "Embedding failed",
                embeddingResult.Value,
                [],
                null,
                [],
                false,
                embeddingResult.Status);
        }

        var embedding = embeddingResult.Value!;

        var retrievalResult = await RetrieveContextAsync(embedding, request.ProjectId, cancellationToken);
        if (!retrievalResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "Context retrieval failed.",
                Embedding: embedding,
                RetrievedChunks: [],
                Completion: null,
                History: request.History,
                HandoverToHumanNeeded: false,
                Status: retrievalResult.Status);
        }

        var retrievedContext = retrievalResult.Value!;

        if (RagHeuristicsHelper.IsAllRetrievedContextLabelledN2(retrievedContext))
        {
            return ReturnEscalationAnswer(request, request.History, embedding, retrievedContext, true);
        }

        var contextChunk = GetContextChunkString(retrievedContext);
        var coverageEvaluationResult =
            await judgeService.EvaluateCoverageAsync(request.Question, contextChunk, cancellationToken);

        if (!coverageEvaluationResult.IsSuccess)
        {
            return new ChatOrchestrationResult(
                Answer: "An internal error occurred.",
                Embedding: embedding,
                RetrievedChunks: retrievedContext,
                Completion: null,
                History: request.History,
                HandoverToHumanNeeded: true,
                Status: coverageEvaluationResult.Status);
        }

        var (judgeClarify, judgePrompt) = coverageEvaluationResult.Value;
        var heuristicClarify = RagHeuristicsHelper.ShouldClarify(retrievedContext);

        if (heuristicClarify || judgeClarify)
        {
            return HandleClarification(request, embedding, retrievedContext, judgePrompt);
        }

        return await GenerateFinalAnswerAsync(request, embedding, retrievedContext, contextChunk, cancellationToken);
    }

    private async Task<Result<EmbeddingResponse>> GetEmbeddingAsync(string question,
        CancellationToken cancellationToken)
    {
        var embeddingResult = await openAi.CreateEmbeddingAsync(question, cancellationToken);
        if (!embeddingResult.IsSuccess)
        {
            logger.LogWarning("Embedding generation failed: {ErrorMessage}", embeddingResult.Status.ErrorMessage);
            return Result<EmbeddingResponse>.Failure(embeddingResult.Status);
        }

        var embedding = embeddingResult.Value;
        var firstEmbedding = embedding is { Data.Count: > 0 } ? embedding.Data[0].Embedding : null;
        if (firstEmbedding is null)
        {
            logger.LogWarning("Embedding generation failed: No embedding data returned");
            return Result<EmbeddingResponse>.Failure("No embedding data returned");
        }

        return Result<EmbeddingResponse>.Success(embedding!);
    }

    private async Task<Result<IReadOnlyList<VectorDbSearchResult>>> RetrieveContextAsync(
        EmbeddingResponse embedding,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var firstEmbedding = embedding.Data[0].Embedding;
        var vectorQuery = new VectorQuery(firstEmbedding.ToArray(), 3, "embeddings");
        var searchRequestResult = VectorSearchBuilder.Build(vectorQuery, projectId);

        if (!searchRequestResult.IsSuccess)
        {
            return Result<IReadOnlyList<VectorDbSearchResult>>.Failure(searchRequestResult.Status);
        }

        return await vectorDb.SearchAsync(searchRequestResult.Value!, cancellationToken);
    }

    private static ChatOrchestrationResult HandleClarification(
        RagRequest request,
        EmbeddingResponse embedding,
        IReadOnlyList<VectorDbSearchResult> retrievedContext,
        string? judgePrompt)
    {
        var clarificationsSoFar = RagHeuristicsHelper.GetHistoryClarificationsCount(request.History);

        if (RagHeuristicsHelper.HasExceededClarificationLimit(clarificationsSoFar))
        {
            return ReturnEscalationAnswer(request, request.History, embedding, retrievedContext, true);
        }

        var clarificationPrompt = FlowConstants.ClarificationTag + " " + judgePrompt;
        var returnedHistoryClarify = new List<ChatMessage>(request.History)
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
            false,
            Status.Ok());
    }

    private async Task<ChatOrchestrationResult> GenerateFinalAnswerAsync(
        RagRequest request,
        EmbeddingResponse embedding,
        IReadOnlyList<VectorDbSearchResult> retrievedContext,
        string contextChunk,
        CancellationToken cancellationToken)
    {
        var system = new ChatMessage(RoleConstants.SystemRole, RagPrompts.SystemPrompt);
        var messages = new List<ChatMessage> { system };
        messages.AddRange(request.History);

        var userWithContext = new ChatMessage(RoleConstants.UserRole,
            $"Question: {request.Question}\n\nContext:\n{contextChunk}");
        messages.Add(userWithContext);

        return await GetAnswer(
            request,
            messages,
            request.History,
            embedding,
            retrievedContext,
            cancellationToken);
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
        var attempt = 0;

        var isAnyContentLabelledN2 = RagHeuristicsHelper.IsAnyRetrievedContextLabelledN2(retrievedContext);

        while (true)
        {
            var chatResult = await RetryHelper.ExecuteWithRetryAsync(
                () => openAi.CreateChatCompletionAsync(messages, cancellationToken),
                maxRetries: MaximumRetryCount,
                cancellationToken);

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
                if (attempt >= MaximumRetryCount)
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

                attempt++;
                continue;
            }

            var (answer, parsedHandoverToHuman) = parseResult.Value;

            // Adicionei o check para ver se algum dos conteúdos recuperados é N2
            // Isso diminui o risco de alucinacao jah que iremos desconsiderar conteúdo usado como N2 se nenhum eh N2
            if (isAnyContentLabelledN2 && parsedHandoverToHuman)
            {
                if (attempt >= MaximumRetryCount)
                {
                    return ReturnEscalationAnswer(
                        request,
                        chatHistory,
                        embedding,
                        retrievedContext,
                        true);
                }

                attempt++;
                continue;
            }

            var returnedHistory = new List<ChatMessage>(chatHistory)
            {
                new(RoleConstants.UserRole, request.Question),
                new(RoleConstants.AssistantRole, answer)
            };

            return new ChatOrchestrationResult(
                answer,
                embedding,
                retrievedContext,
                chat,
                returnedHistory,
                parsedHandoverToHuman,
                Status.Ok());
        }
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
}