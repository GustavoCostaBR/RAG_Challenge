namespace RAG_Challenge.Domain.Constants;

public static class RagPrompts
{
    public const string CoverageJudgeSystemPrompt =
        "You are a coverage judge. Decide if the internal information is sufficient to answer the question. " +
        "Respond in one line. If sufficient, reply: YES. " +
        "If insufficient, reply: NO: I can't find the answer in my internal search. Please clarify <state the missing detail>. Can you please rephrase?";

    public const string SystemPrompt =
        "You are a helpful assistant. Use only the provided context (no external knowledge). " +
        "Respond in JSON as {\"answer\":\"...\",\"handoverToHumanNeeded\":false}. " +
        "If any content you rely on is labeled N2 (only if you used it for the answer), set handoverToHumanNeeded to true. " +
        "Keep answer concise.";
}