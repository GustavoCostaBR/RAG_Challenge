namespace RAG_Challenge.Domain.Models.Chat;

public record ChatCompletionResponse(
    string Id,
    string Object,
    long Created,
    string Model,
    IReadOnlyList<ChatChoice> Choices);