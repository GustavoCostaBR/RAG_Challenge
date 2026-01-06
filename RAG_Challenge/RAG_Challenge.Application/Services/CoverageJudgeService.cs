using System.Globalization;
using Microsoft.Extensions.Logging;
using RAG_Challenge.Application.Helpers;
using RAG_Challenge.Domain.Constants;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Application.Services;

public class CoverageJudgeService(IOpenAiClient openAi, ILogger<CoverageJudgeService> logger) : ICoverageJudgeService
{
    private const int MaximumRetryCount = 2;

    public async Task<Result<(bool NeedClarification, string? ClarificationPrompt)>> EvaluateCoverageAsync(
        string question,
        string context,
        CancellationToken cancellationToken = default)
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

        var judgeResult = await RetryHelper.ExecuteWithRetryAsync(
            () => openAi.CreateChatCompletionAsync(judgeMessages, cancellationToken),
            maxRetries: MaximumRetryCount,
            cancellationToken);

        if (!judgeResult.IsSuccess)
        {
            return Result<(bool, string?)>.Failure(judgeResult.Status);
        }

        var chatResponse = judgeResult.Value;
        var firstResult = chatResponse is { Choices.Count: > 0 } ? chatResponse.Choices[0] : null;

        var judgeText = firstResult?.Message.Content;

        if (string.IsNullOrWhiteSpace(judgeText))
        {
            logger.LogWarning("Coverage Judge returned empty response.");
            return Result<(bool, string?)>.Failure("No response from Coverage Judge");
        }

        judgeText = judgeText.Trim();
        logger.LogInformation("Coverage Judge response: {JudgeText}", judgeText);

        if (judgeText.StartsWith("NO", true, CultureInfo.InvariantCulture))
        {
            var clarification = judgeText.Length > 2 ? judgeText[2..].TrimStart(':', ' ', '\t') : null;

            return string.IsNullOrWhiteSpace(clarification)
                ? Result<(bool, string?)>.Failure("Coverage Judge returned NO but provided no clarification")
                : Result<(bool, string?)>.Success((true, clarification));
        }

        return judgeText.StartsWith("YES", true, CultureInfo.InvariantCulture)
            ? Result<(bool, string?)>.Success((false, null))
            : Result<(bool, string?)>.Failure($"Unexpected response from Coverage Judge: {judgeText}");
    }
}